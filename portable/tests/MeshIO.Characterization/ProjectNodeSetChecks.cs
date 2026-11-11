using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectNodeSetChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "node sets é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.pma"), edited = Path.Combine(root, "edited.pma");
            StaticProjectArchive.Save(source, new StaticModelWorkflow(2).Model, new byte[] { 1 }, new byte[] { 2 });
            byte[] original = File.ReadAllBytes(source);
            check(ProjectNodeSets.Nodes(source).SequenceEqual(Enumerable.Range(1, 8)), "node inventory uses actual mesh IDs");
            ProjectNodeSets.AddCopy(source, edited, "selected", new[] { 7, 2, 2, 6, 3 });
            var sets = ProjectNodeSets.List(edited);
            check(sets.Single(s => s.Name == "selected").Nodes.SequenceEqual(new[] { 2, 3, 6, 7 }), "node set deduplicates and sorts membership");
            check(ProjectNodeSets.List(source).All(s => sets.Any(other => other.Name == s.Name && other.Nodes.SequenceEqual(s.Nodes))), "node-set creation preserves existing sets");
            var reopened = StaticProjectArchive.Open(edited);
            check(reopened.Frd == null && reopened.Dat == null && File.ReadAllBytes(source).SequenceEqual(original), "node-set creation preserves source and clears results");
            check(ProjectLoads.List(source).SequenceEqual(ProjectLoads.List(edited)) && ProjectBoundaries.List(source).SequenceEqual(ProjectBoundaries.List(edited)), "node-set creation preserves loads and boundaries");
            var target = ProjectBoundaries.CreationTargets(edited).First(t => t.Region == "selected");
            string withBoundary = Path.Combine(root, "boundary.pma");
            ProjectBoundaries.AddCopy(edited, withBoundary, target.Step, "new", target.Region, 1, .01);
            check(ProjectBoundaries.List(withBoundary).Any(b => b.Name == "new" && b.Region == "selected" && b.Value == .01), "new node set is usable by boundary creation");
            foreach (var invalid in new[] { Array.Empty<int>(), new[] { 0 }, new[] { 9 }, new[] { 1, -1 } })
            {
                bool rejected = false;
                string output = Path.Combine(root, "invalid.pma");
                try { ProjectNodeSets.AddCopy(source, output, "invalid", invalid); }
                catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(output), "invalid node membership publishes nothing");
            }
            foreach (string name in new[] { " ", "LEFT" })
            {
                bool rejected = false;
                try { ProjectNodeSets.AddCopy(source, edited, name, new[] { 1 }); }
                catch (ArgumentException) { rejected = true; }
                check(rejected, "node-set creation rejects blank or duplicate name");
            }
            var box = ProjectNodeSets.Bounds(source);
            check(box == new NodeSelectionBox(0, 1, 0, 1, 0, 1), "selection bounds match mesh coordinates");
            check(ProjectNodeSets.SelectBox(source, box).SequenceEqual(Enumerable.Range(1, 8)), "box includes nodes on all boundaries");
            var face = ProjectNodeSets.SelectBox(source, box with { XMin = 1 });
            check(face.SequenceEqual(new[] { 2, 3, 6, 7 }), "zero-width box selects exact face");
            check(ProjectNodeSets.SelectBox(source, box with { XMin = 1, YMin = 1 }).SequenceEqual(new[] { 3, 7 }), "box selects edge");
            check(ProjectNodeSets.SelectBox(source, box with { XMin = 1, YMin = 1, ZMin = 1 }).SequenceEqual(new[] { 7 }), "box selects point");
            check(ProjectNodeSets.SelectBox(source, box with { XMin = .25, XMax = .75 }).Length == 0, "empty coordinate selection remains empty");
            foreach (var invalid in new[] { box with { XMin = 2 }, box with { YMax = -1 }, box with { ZMin = 2 },
                box with { XMin = double.NaN }, box with { YMax = double.PositiveInfinity }, box with { ZMin = double.NegativeInfinity } })
            {
                bool rejected = false;
                try { ProjectNodeSets.SelectBox(source, invalid); }
                catch (ArgumentException) { rejected = true; }
                check(rejected, "box rejects reversed and nonfinite limits");
            }
            string selectedPath = Path.Combine(root, "box-selected.pma");
            ProjectNodeSets.AddCopy(source, selectedPath, "box-face", face);
            check(ProjectNodeSets.List(selectedPath).Single(s => s.Name == "box-face").Nodes.SequenceEqual(face)
                && File.ReadAllBytes(source).SequenceEqual(original), "box selection persists without changing source");
            bool overwrite = false;
            try { ProjectNodeSets.AddCopy(source, source, "new", new[] { 1 }); }
            catch (IOException) { overwrite = true; }
            check(overwrite && File.ReadAllBytes(source).SequenceEqual(original), "node-set creation refuses overwrite");
            check(!Directory.GetFiles(root, ".edit-*").Any(), "node-set staging cleaned on failure");
        }
        finally { Directory.Delete(root, true); }
    }
}
