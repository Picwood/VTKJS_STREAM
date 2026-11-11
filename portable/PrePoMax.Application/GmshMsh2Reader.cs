using System.Globalization;
using System.Text.RegularExpressions;
using CaeMesh;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

/// <summary>Bounded ASCII Gmsh MSH 2.2 volume reader with CAD-region reconstruction.</summary>
internal static class GmshMsh2Reader
{
    private sealed record ElementRecord(int Id, int Physical, int Entity, int[] Nodes);

    internal static FeMesh Read(string path, ProjectCadTopology? topology = null)
    {
        var cadEntities = topology?.Entities.ToDictionary(entity => (entity.Dimension, entity.Tag));
        string[] lines = File.ReadAllLines(path);
        var physicalNames = new Dictionary<(int Dimension, int Tag), string>();
        var nodes = new Dictionary<int, FeNode>();
        var triangles = new List<ElementRecord>();
        var tetrahedra = new List<ElementRecord>();
        var elementIds = new HashSet<int>();
        bool meshFormat = false, nodeSection = false, elementSection = false;
        int index = 0;

        string Next(string message)
        {
            if (index >= lines.Length) throw new InvalidDataException(message);
            return lines[index++].Trim();
        }
        int Count(string value, int maximum, string message) =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count >= 0 && count <= maximum
                ? count : throw new InvalidDataException(message);
        int Positive(string value, string message) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0
                ? result : throw new InvalidDataException(message);

        while (index < lines.Length)
        {
            string section = Next("Missing MSH section.");
            if (section.Length == 0) continue;
            if (section == "$MeshFormat")
            {
                if (meshFormat) throw new InvalidDataException("Repeated MSH mesh-format section.");
                string[] format = Next("Missing MSH format record.").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (format.Length != 3 || format[0] != "2.2" || format[1] != "0" || format[2] != "8" ||
                    Next("Missing MSH mesh-format terminator.") != "$EndMeshFormat")
                    throw new NotSupportedException("Require ASCII Gmsh MSH 2.2 with eight-byte floating-point values.");
                meshFormat = true;
            }
            else if (section == "$PhysicalNames")
            {
                int count = Count(Next("Missing physical-name count."), 2000, "Invalid physical-name count.");
                var expression = new Regex("^([0-3])\\s+([1-9][0-9]*)\\s+\"(.*)\"$", RegexOptions.CultureInvariant);
                for (int row = 0; row < count; row++)
                {
                    Match match = expression.Match(Next("Truncated physical-name section."));
                    if (!match.Success) throw new InvalidDataException("Invalid Gmsh physical-name record.");
                    int dimension = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    int tag = Positive(match.Groups[2].Value, "Invalid physical-name tag.");
                    string name = match.Groups[3].Value.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
                    if (name.Length == 0 || !physicalNames.TryAdd((dimension, tag), name))
                        throw new InvalidDataException("Empty or duplicate Gmsh physical name.");
                }
                if (Next("Missing physical-name terminator.") != "$EndPhysicalNames")
                    throw new InvalidDataException("Invalid physical-name terminator.");
            }
            else if (section == "$Nodes")
            {
                if (nodeSection) throw new InvalidDataException("Repeated MSH node section.");
                int count = Count(Next("Missing node count."), 100000, "Invalid MSH node count.");
                if (count == 0) throw new InvalidDataException("MSH contains no nodes.");
                for (int row = 0; row < count; row++)
                {
                    string[] fields = Next("Truncated MSH node section.").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length != 4) throw new InvalidDataException("Invalid MSH node record.");
                    int id = Positive(fields[0], "Invalid MSH node ID.");
                    var xyz = new double[3];
                    for (int component = 0; component < 3; component++)
                        if (!double.TryParse(fields[component + 1], NumberStyles.Float, CultureInfo.InvariantCulture,
                                             out xyz[component]) || !double.IsFinite(xyz[component]))
                            throw new InvalidDataException("Invalid MSH node coordinate.");
                    if (!nodes.TryAdd(id, new FeNode(id, xyz))) throw new InvalidDataException("Duplicate MSH node ID.");
                }
                if (Next("Missing node-section terminator.") != "$EndNodes")
                    throw new InvalidDataException("Invalid node-section terminator.");
                nodeSection = true;
            }
            else if (section == "$Elements")
            {
                if (elementSection || !nodeSection) throw new InvalidDataException("Repeated MSH element section or elements before nodes.");
                int count = Count(Next("Missing element count."), 300000, "Invalid MSH element count.");
                for (int row = 0; row < count; row++)
                {
                    string[] fields = Next("Truncated MSH element section.").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length < 5) throw new InvalidDataException("Invalid MSH element record.");
                    int id = Positive(fields[0], "Invalid MSH element ID.");
                    int type = Positive(fields[1], "Invalid MSH element type.");
                    int tagCount = Count(fields[2], 16, "Invalid MSH element tag count.");
                    int width = type switch { 1 => 2, 2 => 3, 4 => 4, 8 => 3, 9 => 6, 11 => 10, 15 => 1,
                        _ => throw new NotSupportedException("Supported MSH element types are point, line, triangle and linear/quadratic tetrahedron.") };
                    if (tagCount < 2 || fields.Length != 3 + tagCount + width)
                        throw new InvalidDataException("MSH elements require physical and geometrical entity tags.");
                    if (!elementIds.Add(id)) throw new InvalidDataException("Duplicate MSH element ID.");
                    int physical = int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) && p >= 0
                        ? p : throw new InvalidDataException("Invalid MSH physical tag.");
                    int entity = Positive(fields[4], "Invalid MSH geometrical entity tag.");
                    if (cadEntities != null)
                    {
                        int dimension = type switch { 15 => 0, 1 or 8 => 1, 2 or 9 => 2, _ => 3 };
                        if (!cadEntities.TryGetValue((dimension, entity), out var cadEntity) ||
                            physical != 0 && !cadEntity.PhysicalTags.Contains(physical) ||
                            physical == 0 && cadEntity.PhysicalTags.Length != 0)
                            throw new InvalidDataException("Mesh entity or physical membership does not match the CAD topology snapshot.");
                    }
                    int[] connectivity = fields.Skip(3 + tagCount).Select(value => Positive(value, "Invalid MSH connectivity.")).ToArray();
                    if (connectivity.Distinct().Count() != connectivity.Length || connectivity.Any(node => !nodes.ContainsKey(node)))
                        throw new InvalidDataException("MSH element contains a repeated or unknown node.");
                    if (type is 2 or 9) triangles.Add(new(id, physical, entity, connectivity));
                    else if (type is 4 or 11) tetrahedra.Add(new(id, physical, entity, connectivity));
                }
                if (Next("Missing element-section terminator.") != "$EndElements")
                    throw new InvalidDataException("Invalid element-section terminator.");
                elementSection = true;
            }
            else throw new NotSupportedException("Unsupported MSH section: " + section);
        }

        if (!meshFormat || !nodeSection || !elementSection || tetrahedra.Count == 0 || tetrahedra.Count > 100000 || triangles.Count > 100000)
            throw new InvalidDataException("Require a complete bounded tetrahedral MSH 2.2 mesh.");

        var entityPartIds = tetrahedra.Select(element => element.Entity).Distinct().Order().Select((entity, part) => (entity, part: part + 1))
            .ToDictionary(item => item.entity, item => item.part);
        var elements = new Dictionary<int, FeElement>();
        foreach (var record in tetrahedra)
        {
            int[] ids = record.Nodes.ToArray();
            var corners = new LinearTetraElement(record.Id, ids.Take(4).ToArray());
            double volume = corners.GetVolume(nodes);
            if (!double.IsFinite(volume) || volume <= 0)
                throw new InvalidDataException("MSH contains a degenerate or inverted tetrahedron.");
            if (ids.Length == 10)
            {
                // Gmsh and CalculiX differ in their last two midside nodes.
                (ids[8], ids[9]) = (ids[9], ids[8]);
                (int A, int B)[] edges = [(0, 1), (1, 2), (2, 0), (0, 3), (1, 3), (2, 3)];
                for (int edge = 0; edge < edges.Length; edge++)
                {
                    var a = nodes[ids[edges[edge].A]].Coor;
                    var b = nodes[ids[edges[edge].B]].Coor;
                    var middle = nodes[ids[edge + 4]].Coor;
                    double scale = a.Zip(b, (x, y) => Math.Abs(x - y)).Max();
                    for (int axis = 0; axis < 3; axis++)
                        if (Math.Abs(middle[axis] - (a[axis] / 2 + b[axis] / 2)) >
                            1e-8 * scale + 1e-14 * Math.Max(Math.Abs(a[axis]), Math.Abs(b[axis])))
                            throw new NotSupportedException("Require straight-sided quadratic tetrahedra with midpoint edge nodes.");
                }
            }
            elements.Add(record.Id, ids.Length == 10
                ? new ParabolicTetraElement(record.Id, entityPartIds[record.Entity], ids)
                : new LinearTetraElement(record.Id, entityPartIds[record.Entity], ids));
        }

        var mesh = new FeMesh(nodes, elements, MeshRepresentation.Mesh);
        AddVolumeRegions(mesh, tetrahedra, entityPartIds, physicalNames);
        AddSurfaceRegions(mesh, triangles, physicalNames);
        return mesh;
    }

    private static void AddVolumeRegions(FeMesh mesh, List<ElementRecord> tetrahedra,
                                         Dictionary<int, int> entityPartIds,
                                         Dictionary<(int Dimension, int Tag), string> physicalNames)
    {
        var usedPartNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entityPartIds.OrderBy(item => item.Value))
        {
            var records = tetrahedra.Where(element => element.Entity == entity.Key).ToArray();
            string candidate = RegionName(3, records.Select(element => element.Physical).FirstOrDefault(tag => tag > 0),
                                          entity.Key, "CAD_VOLUME", physicalNames);
            string name = Unique(candidate, usedPartNames);
            BasePart part = mesh.Parts.Values.Single(value => value.PartId == entity.Value);
            mesh.Parts.Remove(part.Name);
            part.Name = name;
            mesh.Parts.Add(name, part);
        }

        var usedSetNames = new HashSet<string>(mesh.ElementSets.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var group in tetrahedra.GroupBy(element => RegionKey(element.Physical, element.Entity)).OrderBy(group => group.Key))
        {
            ElementRecord first = group.First();
            string name = Unique(RegionName(3, first.Physical, first.Entity, "CAD_VOLUME", physicalNames), usedSetNames);
            mesh.ElementSets.Add(name, new FeElementSet(name, group.Select(element => element.Id).Order().ToArray()));
        }
    }

    private static void AddSurfaceRegions(FeMesh mesh, List<ElementRecord> triangles,
                                          Dictionary<(int Dimension, int Tag), string> physicalNames)
    {
        var owners = new Dictionary<string, List<(int Element, FeFaceName Face)>>();
        foreach (FeElement element in mesh.Elements.Values)
        {
            foreach (FeFaceName face in new[] { FeFaceName.S1, FeFaceName.S2, FeFaceName.S3, FeFaceName.S4 })
            {
                int[] ids = element.GetNodeIdsFromFaceName(face).Order().ToArray();
                string key = string.Join(",", ids);
                if (!owners.TryGetValue(key, out var faces)) owners.Add(key, faces = new());
                faces.Add((element.Id, face));
            }
        }

        var usedSurfaceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedSetNames = new HashSet<string>(mesh.ElementSets.Keys, StringComparer.OrdinalIgnoreCase);
        int surfaceIndex = 0;
        foreach (var group in triangles.GroupBy(element => RegionKey(element.Physical, element.Entity)).OrderBy(group => group.Key))
        {
            ElementRecord first = group.First();
            string surfaceName = Unique(RegionName(2, first.Physical, first.Entity, "CAD_FACE", physicalNames), usedSurfaceNames);
            var resolved = new HashSet<(int Element, FeFaceName Face)>();
            foreach (ElementRecord triangle in group)
            {
                int[] ids = triangle.Nodes.Order().ToArray();
                if (!owners.TryGetValue(string.Join(",", ids), out var faces) || faces.Count is < 1 or > 2)
                    throw new InvalidDataException("A Gmsh surface triangle does not match the tetrahedral boundary or interface.");
                resolved.UnionWith(faces);
            }
            var surface = new FeSurface(surfaceName) { SurfaceFaceTypes = FeSurfaceFaceTypes.SolidFaces };
            foreach (var faceGroup in resolved.GroupBy(item => item.Face).OrderBy(item => item.Key))
            {
                string setName = Unique($"__gmsh_surface_{++surfaceIndex}_{faceGroup.Key}", usedSetNames);
                mesh.ElementSets.Add(setName, new FeElementSet(setName, faceGroup.Select(item => item.Element).Distinct().Order().ToArray()));
                surface.AddElementFace(faceGroup.Key, setName);
            }
            mesh.Surfaces.Add(surfaceName, surface);
        }
    }

    private static int RegionKey(int physical, int entity) => physical > 0 ? physical : -entity;

    private static string RegionName(int dimension, int physical, int entity, string prefix,
                                     Dictionary<(int Dimension, int Tag), string> physicalNames)
    {
        string fallback = $"{prefix}_{entity}";
        string candidate = physical == 0 ? fallback : physicalNames.TryGetValue((dimension, physical), out string? name)
            ? name : throw new InvalidDataException("MSH element references an undefined physical name.");
        candidate = new string(candidate.Select(character =>
            char.IsLetterOrDigit(character) || character is '_' or '-' or '(' or ')' ? character : '_').ToArray());
        if (!candidate.Any(char.IsLetter)) candidate = fallback + "_" + candidate;
        if (candidate.Length > 64) candidate = candidate[..64];
        return candidate;
    }

    private static string Unique(string candidate, HashSet<string> used)
    {
        string name = candidate;
        int suffix = 2;
        while (!used.Add(name)) name = candidate + "_" + suffix++;
        return name;
    }
}
