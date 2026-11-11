using CaeGlobals;
using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectRemovalChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "remove definitions é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.pma");
            var model = new StaticModelWorkflow(2).Model;
            model.StepCollection.StepsList[0].AddBoundaryCondition(new FixedBC("fixed", "LEFT", RegionTypeEnum.NodeSetName, false));
            StaticProjectArchive.Save(source, model, new byte[] { 1 }, new byte[] { 2 });
            var original = File.ReadAllBytes(source);
            string step = model.StepCollection.StepsList[0].Name;
            foreach (bool boundary in new[] { false, true })
            {
                string name = boundary ? "left" : "force";
                string output = Path.Combine(root, boundary ? "boundary.pma" : "load.pma");
                ProjectDefinitions.RemoveCopy(source, output, step, name, boundary);
                var reopened = StaticProjectArchive.Open(output);
                var first = reopened.Model.StepCollection.StepsList[0];
                var second = reopened.Model.StepCollection.StepsList[1];
                check(boundary ? !first.BoundaryConditions.ContainsKey(name) && second.BoundaryConditions.ContainsKey(name)
                    : !first.Loads.ContainsKey(name) && second.Loads.ContainsKey(name), "removal isolates step despite repeated names");
                check(boundary ? ProjectLoads.List(source).SequenceEqual(ProjectLoads.List(output))
                    : ProjectBoundaries.List(source).SequenceEqual(ProjectBoundaries.List(output)), "removal preserves other definition category");
                check(first.BoundaryConditions.ContainsKey("fixed"), "removal preserves fixed support");
                check(reopened.Frd == null && reopened.Dat == null && File.ReadAllBytes(source).SequenceEqual(original), "removal invalidates results and preserves source");
                foreach (var invalid in new[] { ("missing", name), (step, "missing") })
                {
                    string invalidOutput = Path.Combine(root, "invalid.pma");
                    bool rejected = false;
                    try { ProjectDefinitions.RemoveCopy(source, invalidOutput, invalid.Item1, invalid.Item2, boundary); }
                    catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { rejected = true; }
                    check(rejected && !File.Exists(invalidOutput), "missing removal target publishes nothing");
                }
                bool overwrite = false;
                try { ProjectDefinitions.RemoveCopy(source, source, step, name, boundary); }
                catch (IOException) { overwrite = true; }
                check(overwrite && File.ReadAllBytes(source).SequenceEqual(original), "removal refuses source overwrite");
            }
            bool unsupported = false;
            try { ProjectDefinitions.RemoveCopy(source, Path.Combine(root, "unsupported.pma"), step, "fixed", true); }
            catch (NotSupportedException) { unsupported = true; }
            check(unsupported && !File.Exists(Path.Combine(root, "unsupported.pma")), "fixed-support removal stays outside editor scope");
            check(!Directory.GetFiles(root, ".edit-*").Any(), "removal cleans failed staging files");
        }
        finally { Directory.Delete(root, true); }
    }
}
