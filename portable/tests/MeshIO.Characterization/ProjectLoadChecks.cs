using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectLoadChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "load edits é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.pma");
            var model = new StaticModelWorkflow(2).Model;
            StaticProjectArchive.Save(source, model, new byte[] { 1 }, new byte[] { 2 });
            byte[] original = File.ReadAllBytes(source);
            var loads = ProjectLoads.List(source);
            check(loads.Length == 2 && loads[0].Name == loads[1].Name && loads[0].Step != loads[1].Step,
                "force editor distinguishes repeated load names by step");
            string edited = Path.Combine(root, "edited.pma");
            ProjectLoads.SaveForceCopy(source, edited, loads[1].Step, loads[1].Name, -3, 4, 5);
            var changed = ProjectLoads.List(edited);
            check(changed[0] == loads[0] && changed[1] == loads[1] with { F1 = -3, F2 = 4, F3 = 5 },
                "force editor changes only selected step and preserves region");
            var reopened = StaticProjectArchive.Open(edited);
            check(reopened.Frd == null && reopened.Dat == null, "force editor removes stale FRD and DAT");
            check(File.ReadAllBytes(source).SequenceEqual(original), "force editor preserves source bytes");
            var force = (STLoad)reopened.Model.StepCollection.StepsList[1].Loads[loads[1].Name];
            check(Math.Abs(force.FMagnitude.Value - Math.Sqrt(50)) < 1e-12, "surface force magnitude stays consistent");
            foreach (var values in new[] { new[] { double.NaN, 0d, 0d }, new[] { 0d, double.PositiveInfinity, 0d }, new[] { 0d, 0d, double.NegativeInfinity } })
            {
                bool rejected = false;
                try { ProjectLoads.SaveForceCopy(source, Path.Combine(root, "invalid.pma"), loads[0].Step, loads[0].Name, values[0], values[1], values[2]); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "invalid.pma")), "nonfinite force component rejected before save");
            }
            foreach (var selection in new[] { ("missing", loads[0].Name), (loads[0].Step, "missing") })
            {
                bool rejected = false;
                try { ProjectLoads.SaveForceCopy(source, Path.Combine(root, "invalid.pma"), selection.Item1, selection.Item2, 1, 0, 0); }
                catch (NotSupportedException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "invalid.pma")), "missing force selection rejected");
            }
            string added = Path.Combine(root, "added.pma");
            string regionSource = Path.Combine(root, "with-region.pma");
            ProjectNodeSets.AddCopy(source, regionSource, "new-region", new[] { 2, 3, 6, 7 });
            check(ProjectLoads.CreationTargets(regionSource).Count(t => t.Region == "new-region") == 2,
                "load creation offers new node sets in each step");
            check(ProjectLoads.CreationTargets(regionSource).Count(t => t.Region == "LOADED" && t.Kind == "Surface traction (total force)") == 2,
                "load creation offers valid element surfaces in each step");
            ProjectLoads.AddCopy(regionSource, added, loads[0].Step, "added", "new-region", 1, -2, 3);
            var addedLoads = ProjectLoads.List(added);
            check(addedLoads.Where(l => l.Name != "added").SequenceEqual(loads), "load creation preserves existing step loads");
            var addedLoad = addedLoads.Single(l => l.Name == "added");
            check(addedLoad.Step == loads[0].Step && addedLoad.Region == "new-region" && addedLoad.F1 == 1 && addedLoad.F2 == -2 && addedLoad.F3 == 3,
                "load creation preserves signed components and region");
            var addedProject = StaticProjectArchive.Open(added);
            check(addedProject.Frd == null && addedProject.Dat == null && File.ReadAllBytes(source).SequenceEqual(original),
                "load creation clears results and preserves source");
            string traction = Path.Combine(root, "traction.pma");
            ProjectLoads.AddCopy(regionSource, traction, loads[0].Step, "added-traction", "LOADED",
                ForceLoadCreationKind.SurfaceTraction, -4, 5, 6);
            var addedTraction = ProjectLoads.List(traction).Single(load => load.Name == "added-traction");
            check(addedTraction.Kind == "Surface traction (total force)" && addedTraction.Region == "LOADED" &&
                addedTraction.F1 == -4 && addedTraction.F2 == 5 && addedTraction.F3 == 6,
                "surface traction creation preserves total-force components and surface assignment");
            check(StaticProjectArchive.Open(traction).Model.StepCollection.StepsList.Single(step => step.Name == loads[0].Step)
                .Loads["added-traction"] is STLoad, "surface traction creation persists the production load type");
            foreach (var invalid in new[] { (loads[0].Step, loads[0].Name, "RIGHT"), ("missing", "new", "RIGHT"),
                (loads[0].Step, "new", "missing"), (loads[0].Step, " ", "RIGHT") })
            {
                bool rejected = false;
                string output = Path.Combine(root, "invalid-add.pma");
                try { ProjectLoads.AddCopy(source, output, invalid.Item1, invalid.Item2, invalid.Item3, 1, 0, 0); }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { rejected = true; }
                check(rejected && !File.Exists(output), "invalid force creation publishes nothing");
            }
            foreach (var invalid in new[] { ("RIGHT", ForceLoadCreationKind.SurfaceTraction), ("LOADED", ForceLoadCreationKind.ConcentratedForce) })
            {
                bool rejected = false;
                string invalidOutput = Path.Combine(root, "invalid-region-kind.pma");
                try { ProjectLoads.AddCopy(source, invalidOutput, loads[0].Step, "new", invalid.Item1, invalid.Item2, 1, 0, 0); }
                catch (NotSupportedException) { rejected = true; }
                check(rejected && !File.Exists(invalidOutput), "load creation rejects region and load-kind mismatch");
            }
            foreach (var invalid in new[] { new[] { double.NaN, 0d, 0d }, new[] { 0d, double.PositiveInfinity, 0d }, new[] { 0d, 0d, double.NegativeInfinity } })
            {
                bool rejected = false;
                try { ProjectLoads.AddCopy(source, added, loads[0].Step, "new", "RIGHT", invalid[0], invalid[1], invalid[2]); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                check(rejected, "force creation rejects each nonfinite component");
            }
            bool addOverwrite = false;
            try { ProjectLoads.AddCopy(source, source, loads[0].Step, "new", "RIGHT", 0, 0, 0); }
            catch (IOException) { addOverwrite = true; }
            check(addOverwrite && File.ReadAllBytes(source).SequenceEqual(original), "force creation refuses overwrite");
            bool overwriteRejected = false;
            try { ProjectLoads.SaveForceCopy(source, source, loads[0].Step, loads[0].Name, 1, 0, 0); }
            catch (IOException) { overwriteRejected = true; }
            check(overwriteRejected && File.ReadAllBytes(source).SequenceEqual(original), "force edit refuses overwrite");
            check(!Directory.GetFiles(root, ".edit-*").Any(), "force edit cleans failed staging files");
            string concentrated = Path.Combine(root, "concentrated.pma");
            StaticProjectArchive.Save(concentrated, MultiPartProjectChecks.Create());
            var nodal = ProjectLoads.List(concentrated).Single(load => load.Kind == "Concentrated force");
            string nodalEdited = Path.Combine(root, "nodal-edited.pma");
            ProjectLoads.SaveForceCopy(concentrated, nodalEdited, nodal.Step, nodal.Name, 0, -2, 3);
            check(ProjectLoads.List(nodalEdited).Single(load => load.Name == nodal.Name) == nodal with { F1 = 0, F2 = -2, F3 = 3 },
                "concentrated force edit preserves assignment and signed components");
        }
        finally { Directory.Delete(root, true); }
    }
}
