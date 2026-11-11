using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;
using PrePoMax.Render.Server;
using System.Text;
using System.Text.Json.Nodes;

internal static class ProjectMeshImportChecks
{
    public const string Mesh = "MeshVersionFormatted 2\nDimension 3\nVertices\n4\n0 0 0 1\n1 0 0 1\n0 1 0 1\n0 0 1 1\nTetrahedra\n1\n1 2 3 4 1\nEnd\n";
    private const string NamedMsh = """
$MeshFormat
2.2 0 8
$EndMeshFormat
$PhysicalNames
2
2 11 "INLET"
3 21 "BODY"
$EndPhysicalNames
$Nodes
4
1 0 0 0
2 1 0 0
3 0 1 0
4 0 0 1
$EndNodes
$Elements
5
1 2 2 11 1 1 2 3
2 2 2 0 2 1 4 2
3 2 2 0 3 2 4 3
4 2 2 0 4 3 4 1
5 4 2 21 1 1 2 3 4
$EndElements
""";
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "mesh import é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "tetra.mesh"), project = Path.Combine(root, "imported.pma");
            File.WriteAllText(source, Mesh);
            ProjectMeshImport.Import(source, project, 1000, 0);
            var imported = StaticProjectArchive.Open(project);
            check(imported.Model.Mesh.Elements.Values.Single() is CaeMesh.LinearTetraElement, "mesh import persists tetrahedral element");
            check(imported.Model.Materials.Values.Single().Properties.OfType<Elastic>().Single().YoungsPoissonsTemp[0][0].Value == 1000,
                "import uses explicitly supplied material");
            check(imported.Model.StepCollection.StepsList.Single().BoundaryConditions.Count == 0 && imported.Model.StepCollection.StepsList.Single().Loads.Count == 0,
                "import does not invent supports or loads");
            check(imported.Frd == null && imported.Dat == null && File.ReadAllText(source) == Mesh, "import preserves source without stale results");
            string namedSource = Path.Combine(root, "named.msh"), namedProject = Path.Combine(root, "named.pma");
            File.WriteAllText(namedSource, NamedMsh);
            ProjectMeshImport.Import(namedSource, namedProject, 1000, .25);
            var named = StaticProjectArchive.Open(namedProject).Model.Mesh;
            check(named.Parts.Keys.SequenceEqual(new[] { "BODY" }) && named.ElementSets["BODY"].Labels.SequenceEqual(new[] { 5 }),
                "Gmsh MSH2 preserves a named physical volume as a part and element set");
            check(named.Surfaces.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(
                    new[] { "INLET", "CAD_FACE_2", "CAD_FACE_3", "CAD_FACE_4" }),
                "Gmsh MSH2 preserves physical surface names and names remaining CAD faces");
            var inlet = named.Surfaces["INLET"];
            check(inlet.ElementFaces.Single().Key == CaeMesh.FeFaceName.S1 &&
                  named.ElementSets[inlet.ElementFaces.Single().Value].Labels.SequenceEqual(new[] { 5 }),
                "Gmsh surface triangles resolve to editable solid element faces");
            var summary = ProjectService.Inspect(namedProject);
            check(summary.Surfaces.Contains("INLET") && summary.ElementSets.Contains("BODY"),
                "project inspection exposes imported Gmsh regions");
            check(ProjectPressures.CreationTargets(namedProject).Any(target => target.Region == "INLET"),
                "imported Gmsh surface is immediately available to model authoring");
            string loadedProject = Path.Combine(root, "named-loaded.pma");
            ProjectPressures.AddCopy(namedProject, loadedProject, "load-1", "cad-pressure", "INLET", 2.5);
            string loadedInput = Path.Combine(root, "named-loaded.inp");
            ProjectService.Export(loadedProject, loadedInput);
            string deck = File.ReadAllText(loadedInput);
            check(ProjectPressures.List(loadedProject).Single().Region == "INLET" &&
                  deck.Contains("** Name: cad-pressure") && deck.Contains(", P1, 2.5"),
                "Gmsh physical surface reaches production CalculiX pressure output");
            string quadraticMsh = NamedMsh.Replace("$Nodes\n4\n", "$Nodes\n10\n")
                .Replace("4 0 0 1\n$EndNodes", "4 0 0 1\n5 .5 0 0\n6 .5 .5 0\n7 0 .5 0\n8 0 0 .5\n9 0 .5 .5\n10 .5 0 .5\n$EndNodes")
                .Replace("1 2 2 11 1 1 2 3", "1 9 2 11 1 1 2 3 5 6 7")
                .Replace("2 2 2 0 2 1 4 2", "2 9 2 0 2 1 4 2 8 10 5")
                .Replace("3 2 2 0 3 2 4 3", "3 9 2 0 3 2 4 3 10 9 6")
                .Replace("4 2 2 0 4 3 4 1", "4 9 2 0 4 3 4 1 9 8 7")
                .Replace("5 4 2 21 1 1 2 3 4", "5 11 2 21 1 1 2 3 4 5 6 7 8 9 10");
            string quadraticSource = Path.Combine(root, "quadratic.msh");
            string quadraticProject = Path.Combine(root, "quadratic.pma");
            File.WriteAllText(quadraticSource, quadraticMsh);
            ProjectMeshImport.Import(quadraticSource, quadraticProject, 1000, .25);
            var quadratic = StaticProjectArchive.Open(quadraticProject).Model.Mesh;
            check(quadratic.Elements[5] is CaeMesh.ParabolicTetraElement &&
                  quadratic.Elements[5].NodeIds.SequenceEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 10, 9 }),
                "quadratic Gmsh import converts midside ordering into CalculiX ordering");
            check(quadratic.Surfaces.Count == 4 && quadratic.Elements[5].GetNodeIdsFromFaceName(CaeMesh.FeFaceName.S1)
                    .Order().SequenceEqual(new[] { 1, 2, 3, 5, 6, 7 }),
                "quadratic CAD regions retain all six face nodes");
            string quadraticInput = Path.Combine(root, "quadratic.inp");
            ProjectService.Export(quadraticProject, quadraticInput);
            check(File.ReadAllText(quadraticInput).Contains("C3D10"), "quadratic imported project exports C3D10 solver elements");
            string quadraticScene = Path.Combine(root, "quadratic-scene.bin");
            ProjectScene.Export(quadraticProject, quadraticScene);
            using (var quadraticStream = File.OpenRead(quadraticScene))
            {
                var quadraticUpload = HostSceneUploadReader.ReadAsync(quadraticStream, quadraticStream.Length,
                    HostSceneUploadReader.DefaultMaximumBytes, default).GetAwaiter().GetResult();
                check(quadraticUpload.Manifest.Actors.Count == 1, "quadratic imported mesh reaches production renderer reader");
            }
            foreach (string invalidQuadratic in new[]
            {
                quadraticMsh.Replace("9 0 .5 .5", "9 0 .4 .5"),
                quadraticMsh.Replace("1 9 2 11 1 1 2 3 5 6 7", "1 9 2 11 1 1 2 3 5 6 8"),
                quadraticMsh.Replace("5 11 2 21 1 1 2 3 4 5 6 7 8 9 10", "5 11 2 21 1 1 3 2 4 7 6 5 8 10 9")
            })
            {
                string rejectedProject = Path.Combine(root, "invalid-quadratic.pma");
                File.WriteAllText(quadraticSource, invalidQuadratic);
                bool rejected = false;
                try { ProjectMeshImport.Import(quadraticSource, rejectedProject, 1000, .25); }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException) { rejected = true; }
                check(rejected && !File.Exists(rejectedProject), "curved, mismatched or inverted quadratic MSH fails before publication");
            }
            foreach (string invalidMsh in new[]
            {
                NamedMsh.Replace("2.2 0 8", "2.2 1 8"),
                NamedMsh.Replace("1 2 2 11 1 1 2 3", "1 2 2 99 1 1 2 3"),
                NamedMsh.Replace("1 2 2 11 1 1 2 3", "1 2 2 11 1 1 2 99")
            })
            {
                string badMsh = Path.Combine(root, "bad.msh"), badProject = Path.Combine(root, "bad-msh.pma");
                File.WriteAllText(badMsh, invalidMsh);
                bool rejected = false;
                try { ProjectMeshImport.Import(badMsh, badProject, 1000, .25); }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException) { rejected = true; }
                check(rejected && !File.Exists(badProject), "malformed or unsupported Gmsh MSH2 produces no project");
            }
            string scenePath = Path.Combine(root, "scene.bin");
            ProjectScene.Export(project, scenePath);
            using var stream = File.OpenRead(scenePath);
            var scene = HostSceneUploadReader.ReadAsync(stream, stream.Length, HostSceneUploadReader.DefaultMaximumBytes, default).GetAwaiter().GetResult();
            check(scene.Manifest.Actors.Count == 1 && scene.Manifest.Resources.Where(r => r.Semantic == "sourceElementIds").Sum(r => r.ElementCount) == 4,
                "tetra scene reaches production reader with four triangles");
            var metadata = JsonNode.Parse(StaticModelMetadata.Serialize(imported.Model))!;
            check(metadata["Version"]!.GetValue<int>() == 27, "tetra persistence uses metadata version 27");
            MetadataCompatibility.UseLegacyMaterials(metadata);
            metadata["Version"] = 3;
            bool oldRejected = false;
            try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(metadata.ToJsonString())); }
            catch (InvalidDataException) { oldRejected = true; }
            check(oldRejected, "legacy metadata cannot silently reinterpret tetrahedra");
            foreach (string invalid in new[] { Mesh.Replace("1 2 3 4 1", "1 2 3 99 1"), Mesh.Replace("1 2 3 4 1", "1 1 3 4 1"),
                Mesh.Replace("End", "Hexahedra\n0\nEnd"), Mesh.Replace("0 0 1 1", "0 0 NaN 1"), Mesh.Replace("Tetrahedra\n1", "Tetrahedra\n2"),
                Mesh.Replace("End", ""), Mesh.Replace("Dimension 3", "Dimension 2"), Mesh.Replace("0 0 1 1", "0 0 0 1") })
            {
                string bad = Path.Combine(root, "bad.mesh"), output = Path.Combine(root, "bad.pma");
                File.WriteAllText(bad, invalid);
                bool rejected = false;
                try { ProjectMeshImport.Import(bad, output, 1000, 0); }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(output), "malformed or unsupported mesh produces no project");
            }
            byte[] original = File.ReadAllBytes(project);
            bool overwrite = false;
            try { ProjectMeshImport.Import(source, project, 1000, 0); }
            catch (IOException) { overwrite = true; }
            check(overwrite && File.ReadAllBytes(project).SequenceEqual(original), "import refuses output overwrite");
            bool materialRejected = false;
            try { ProjectMeshImport.Import(source, Path.Combine(root, "bad.pma"), -1, .5); }
            catch (ArgumentException) { materialRejected = true; }
            check(materialRejected, "import rejects invalid material");
            var legacy = JsonNode.Parse(StaticModelMetadata.Serialize(new StaticModelWorkflow(1).Model))!;
            MetadataCompatibility.UseLegacyMaterials(legacy);
            legacy["Version"] = 3;
            foreach (var element in legacy["Elements"]!.AsArray()) element!.AsObject().Remove("Kind");
            foreach (var part in legacy["Parts"]!.AsArray())
            {
                part!.AsObject().Remove("TetraType");
                part.AsObject().Remove("ParabolicTetraType");
                part.AsObject().Remove("ParabolicWedgeType");
                part.AsObject().Remove("ParabolicHexaType");
            }
            check(StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacy.ToJsonString())).Mesh.Elements.Values.Single() is CaeMesh.LinearHexaElement,
                "version 3 hexahedral projects remain readable");
        }
        finally { Directory.Delete(root, true); }
    }
}
