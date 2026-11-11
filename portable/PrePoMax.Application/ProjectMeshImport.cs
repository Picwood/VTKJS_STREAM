using System.Globalization;
using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Input;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

/// <summary>Bounded INP, ASCII Medit and ASCII Gmsh MSH 2.2 volume-mesh import into a new static project.</summary>
public static class ProjectMeshImport
{
    public static void Import(string source, string destination, double youngsModulus, double poissonsRatio)
        => Import(source, destination, youngsModulus, poissonsRatio, null, null);

    internal static void Import(string source, string destination, double youngsModulus, double poissonsRatio,
                                ProjectGeometrySource? geometrySource, ProjectGmshOptions? gmshOptions)
    {
        if (!double.IsFinite(youngsModulus) || youngsModulus <= 0 || !double.IsFinite(poissonsRatio) || poissonsRatio <= -1 || poissonsRatio >= .5)
            throw new ArgumentException("Require finite E > 0 and -1 < nu < 0.5.");
        if (new FileInfo(source).Length > 32 * 1024 * 1024) throw new NotSupportedException("Mesh exceeds 32 MiB.");
        if (Path.GetExtension(source).Equals(".inp", StringComparison.OrdinalIgnoreCase))
        {
            SaveModel(InpMeshImport.Read(source), destination, youngsModulus, poissonsRatio, geometrySource, gmshOptions);
            return;
        }
        if (Path.GetExtension(source).Equals(".msh", StringComparison.OrdinalIgnoreCase))
        {
            SaveModel(GmshMsh2Reader.Read(source, geometrySource?.Topology), destination, youngsModulus, poissonsRatio, geometrySource, gmshOptions);
            return;
        }
        if (!Path.GetExtension(source).Equals(".mesh", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Select an INP, ASCII Medit mesh or ASCII Gmsh MSH 2.2 mesh.");
        var lines = File.ReadAllLines(source).Select(l => l.Split('#')[0].Trim()).Where(l => l.Length != 0).ToArray();
        var normalized = new List<string>();
        var seen = new HashSet<string>();
        int vertices = 0, tetrahedra = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var header = lines[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string kind = header[0].ToUpperInvariant();
            if (!seen.Add(kind)) throw new InvalidDataException("Repeated mesh section.");
            if (kind == "END")
            {
                if (header.Length != 1 || i != lines.Length - 1) throw new InvalidDataException("Unexpected trailing mesh data.");
                normalized.Add("End");
                continue;
            }
            string countText = header.Length == 2 ? header[1] : header.Length == 1 && ++i < lines.Length ? lines[i] : throw new InvalidDataException("Missing section count.");
            if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out int count)) throw new InvalidDataException("Invalid section count.");
            if (kind is "MESHVERSIONFORMATTED" or "DIMENSION")
            {
                if (kind == "DIMENSION" ? count != 3 : count is not (1 or 2)) throw new NotSupportedException("Require ASCII version 1/2 and dimension 3.");
                normalized.Add(kind + " " + count);
                continue;
            }
            int width = kind switch { "VERTICES" => 4, "TETRAHEDRA" => 5, "TRIANGLES" => 4, "EDGES" => 3,
                _ => throw new NotSupportedException("Unsupported mesh section: " + kind) };
            if (count < 0 || count > 100000 || i + count >= lines.Length) throw new InvalidDataException("Invalid or truncated mesh section.");
            if (kind == "VERTICES") vertices = count;
            else if (vertices == 0) throw new InvalidDataException("Vertices must precede elements.");
            if (kind == "TETRAHEDRA") tetrahedra = count;
            normalized.Add(kind); normalized.Add(count.ToString(CultureInfo.InvariantCulture));
            for (int row = 0; row < count; row++)
            {
                string line = lines[++i];
                var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length != width) throw new InvalidDataException("Invalid mesh record width.");
                for (int c = 0; c < width; c++)
                {
                    if (kind == "VERTICES" && c < 3)
                    {
                        if (!double.TryParse(fields[c], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                            throw new InvalidDataException("Invalid vertex coordinate.");
                    }
                    else if (!int.TryParse(fields[c], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ||
                             (c < width - 1 && (id < 1 || id > vertices)) || (c == width - 1 && (id < 0 || id > 1000000)))
                        throw new InvalidDataException("Invalid mesh index or region reference.");
                }
                if (kind != "VERTICES" && fields.Take(width - 1).Distinct().Count() != width - 1)
                    throw new InvalidDataException("Repeated element node.");
                normalized.Add(line);
            }
        }
        if (!seen.Contains("END") || !seen.Contains("DIMENSION") || !seen.Contains("MESHVERSIONFORMATTED") || vertices == 0 || tetrahedra == 0)
            throw new InvalidDataException("Require a complete tetrahedral volume mesh.");
        string temporary = Path.Combine(Path.GetTempPath(), "prepomax-import-" + Guid.NewGuid().ToString("N") + ".mesh");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            File.WriteAllLines(temporary, normalized);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var mesh = MmgFileReader.Read3D(temporary, ElementsToImport.Solid, MeshRepresentation.Mesh);
            if (mesh.Nodes.Count != vertices || mesh.Elements.Count != tetrahedra || mesh.Elements.Values.Any(e =>
                e is not LinearTetraElement tetra || !double.IsFinite(tetra.GetVolume(mesh.Nodes)) || tetra.GetVolume(mesh.Nodes) <= 0))
                throw new InvalidDataException("Imported mesh has missing or degenerate volume elements.");
            SaveModel(mesh, destination, youngsModulus, poissonsRatio, geometrySource, gmshOptions);
        }
        finally { CultureInfo.CurrentCulture = culture; if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void SaveModel(FeMesh mesh, string destination, double youngsModulus, double poissonsRatio,
                                  ProjectGeometrySource? geometrySource, ProjectGmshOptions? gmshOptions)
    {
        var model = new FeModel("Imported mesh", null, false);
        model.Properties.ModelSpace = ModelSpaceEnum.ThreeD;
        model.SetMesh(mesh);
        if (gmshOptions != null) ProjectGmshSettings.Apply(model, gmshOptions, geometrySource);
        string allNodes = "ALLNODES";
        while (mesh.NodeSets.Keys.Any(name => name.Equals(allNodes, StringComparison.OrdinalIgnoreCase))) allNodes += "_";
        mesh.NodeSets.Add(allNodes, new FeNodeSet(allNodes, mesh.Nodes.Keys.Order().ToArray()));
        var material = new Material("ELASTIC");
        material.AddProperty(new Elastic(new[] { new[] { youngsModulus, poissonsRatio, 0d } }));
        model.Materials.Add(material.Name, material);
        int index = 0;
        foreach (var part in mesh.Parts.Values)
        {
            string name = "solid-" + ++index;
            model.Sections.Add(name, new SolidSection(name, material.Name, part.Name, RegionTypeEnum.PartName, 1, false));
        }
        var step = new StaticStep("load-1", false);
        step.AddFieldOutput(new NodalFieldOutput("displacement", NodalFieldVariable.U));
        step.AddFieldOutput(new ElementFieldOutput("stress", ElementFieldVariable.S));
        step.AddHistoryOutput(new NodalHistoryOutput("displacements", NodalHistoryVariable.U, allNodes, RegionTypeEnum.NodeSetName));
        model.StepCollection.AddStep(step);
        ProjectEdits.SaveNew(destination, model, geometrySource);
    }

}
