using CaeMesh;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectSurfaceChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "surface edits é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.pma"), added = Path.Combine(root, "added.pma");
            StaticProjectArchive.Save(source, new StaticModelWorkflow(2).Model, new byte[] { 1 }, new byte[] { 2 });
            byte[] original = File.ReadAllBytes(source);
            var inventory = ProjectSurfaces.Inventory(source);
            check(inventory.Total == 1 && inventory.Elements.Single().Id == 1 &&
                inventory.Elements.Single().Faces.SequenceEqual(new[] { "S1", "S2", "S3", "S4", "S5", "S6" }),
                "surface inventory lists valid faces for each solid element");

            var selection = new[] { new ProjectElementFace(1, "s4"), new ProjectElementFace(1, "S4") };
            int[] expectedNodes = StaticProjectArchive.Open(source).Model.Mesh.Elements[1]
                .GetNodeIdsFromFaceName(FeFaceName.S4).Order().ToArray();
            check(ProjectSurfaces.FaceNodes(source, selection).SequenceEqual(expectedNodes),
                "surface preview resolves distinct face nodes");
            check(ProjectSurfaces.ResolveFace(source, 1, expectedNodes.Take(3).Reverse().ToArray()) ==
                new ProjectElementFace(1, "S4"), "validated render triangle resolves to its unique solid face");
            int[] firstFace = StaticProjectArchive.Open(source).Model.Mesh.Elements[1]
                .GetNodeIdsFromFaceName(FeFaceName.S1).Take(3).ToArray();
            var areaFaces = ProjectSurfaces.ResolveFaces(source,
                [new(1, expectedNodes.Take(3).ToArray()), new(1, expectedNodes.Skip(1).Take(3).ToArray()), new(1, firstFace)]);
            check(areaFaces.SequenceEqual(new[] { new ProjectElementFace(1, "S1"), new ProjectElementFace(1, "S4") }),
                "validated rendered triangles deduplicate into unique solid faces");
            check(ProjectSurfaces.GrowFace(source, 1, firstFace, 89).SequenceEqual([new ProjectElementFace(1, "S1")]),
                "face-angle growth stops at a sharper connected edge");
            check(ProjectSurfaces.GrowFace(source, 1, firstFace, 90).Length == 6,
                "face-angle growth traverses the connected solid boundary at its threshold");
            foreach (double angle in new[] { double.NaN, -1, 91 })
            {
                bool angleRejected = false;
                try { ProjectSurfaces.GrowFace(source, 1, firstFace, angle); }
                catch (ArgumentOutOfRangeException) { angleRejected = true; }
                check(angleRejected, "face-angle growth rejects nonfinite or out-of-range thresholds");
            }
            foreach (int[] invalidPick in new[] { Array.Empty<int>(), new[] { 2, 3, 3 }, new[] { 2, 3, 99 } })
            {
                bool rejected = false;
                try { ProjectSurfaces.ResolveFace(source, 1, invalidPick); }
                catch (ArgumentException) { rejected = true; }
                check(rejected, "face picking rejects malformed or foreign triangle nodes");
            }
            bool missingElement = false;
            try { ProjectSurfaces.ResolveFace(source, 99, expectedNodes.Take(3).ToArray()); }
            catch (ArgumentException) { missingElement = true; }
            check(missingElement, "face picking rejects unknown source elements");
            ProjectSurfaces.AddCopy(source, added, "CREATED", selection);
            var summary = ProjectSurfaces.List(added).Single(surface => surface.Name == "CREATED");
            check(summary.FaceCount == 1 && summary.ElementCount == 1,
                "surface creation groups duplicate element-face pairs");
            var reopened = StaticProjectArchive.Open(added);
            var surface = reopened.Model.Mesh.Surfaces["CREATED"];
            string backingSet = surface.ElementFaces[FeFaceName.S4];
            check(surface.Type == FeSurfaceType.Element && reopened.Model.Mesh.ElementSets[backingSet].Labels.SequenceEqual(new[] { 1 }),
                "surface creation persists a named face and backing element set");
            check(reopened.Frd == null && reopened.Dat == null && File.ReadAllBytes(source).SequenceEqual(original),
                "surface creation preserves source and removes stale results");
            check(ProjectPressures.CreationTargets(added).Any(target => target.Region == "CREATED") &&
                ProjectLoads.CreationTargets(added).Any(target => target.Region == "CREATED" && target.Kind == "Surface traction (total force)"),
                "created surface is immediately available to pressure and traction authoring");

            string pressured = Path.Combine(root, "pressured.pma");
            ProjectPressures.AddCopy(added, pressured, "load-1", "created-pressure", "CREATED", 3);
            string input = CalculixFileWriter.GetInputText(
                StaticProjectArchive.Open(pressured).Model, ConvertPyramidsToEnum.Wedges);
            check(input.Contains("** Name: created-pressure") && input.Contains(", P4, 3"),
                "pressure on a created surface reaches production CalculiX output");

            foreach (var invalid in new ProjectElementFace[][] {
                [], [new(99, "S1")], [new(1, "Empty")], [new(1, "S7")], [new(1, "")],
                Enumerable.Range(0, 101).Select(_ => new ProjectElementFace(1, "S1")).ToArray() })
            {
                string output = Path.Combine(root, "invalid-" + Guid.NewGuid() + ".pma");
                bool rejected = false;
                try { ProjectSurfaces.AddCopy(source, output, "invalid", invalid); }
                catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(output), "surface creation rejects invalid or excessive face selections");
            }
            foreach (string name in new[] { " ", "loaded" })
            {
                bool rejected = false;
                try { ProjectSurfaces.AddCopy(source, Path.Combine(root, "bad-name.pma"), name,
                    [new ProjectElementFace(1, "S1")]); }
                catch (ArgumentException) { rejected = true; }
                check(rejected, "surface creation rejects blank or duplicate names");
            }
            bool overwrite = false;
            try { ProjectSurfaces.AddCopy(source, source, "new", [new ProjectElementFace(1, "S1")]); }
            catch (IOException) { overwrite = true; }
            check(overwrite && File.ReadAllBytes(source).SequenceEqual(original), "surface creation refuses overwrite");
            check(!Directory.GetFiles(root, ".edit-*").Any(), "surface staging is cleaned after failure");
        }
        finally { Directory.Delete(root, true); }
    }
}
