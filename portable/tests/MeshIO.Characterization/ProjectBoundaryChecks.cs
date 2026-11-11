using CaeModel;
using CaeGlobals;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectBoundaryChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "boundary edit é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.pma"), edited = Path.Combine(root, "edited.pma");
            var model = new StaticModelWorkflow(2).Model;
            model.StepCollection.StepsList[1].AddBoundaryCondition(new FixedBC("fixed", "LEFT", RegionTypeEnum.NodeSetName, false));
            StaticProjectArchive.Save(source, model, new byte[] { 1 }, new byte[] { 2 });
            byte[] original = File.ReadAllBytes(source);
            var boundaries = ProjectBoundaries.List(source);
            check(boundaries.Length == 6 && boundaries.All(b => double.IsFinite(b.Value)), "boundary list excludes free directions");
            var selected = boundaries.First(b => b.Step == boundaries.Last().Step && b.Direction == 1);
            ProjectBoundaries.SaveDisplacementCopy(source, edited, selected.Step, selected.Name, selected.Direction, -0.01);
            check(ProjectBoundaries.List(edited).SequenceEqual(boundaries.Select(b => b == selected ? b with { Value = -0.01 } : b)),
                "boundary edit isolates step name and direction");
            var reopened = StaticProjectArchive.Open(edited);
            var boundary = (DisplacementRotation)reopened.Model.StepCollection.StepsList[1].BoundaryConditions[selected.Name];
            check(double.IsNaN(boundary.U2.Value) && double.IsNaN(boundary.UR1.Value), "boundary edit preserves free translations and rotations");
            check(reopened.Frd == null && reopened.Dat == null, "boundary edit invalidates both result payloads");
            check(ProjectLoads.List(source).SequenceEqual(ProjectLoads.List(edited)), "boundary edit preserves loads");
            check(File.ReadAllBytes(source).SequenceEqual(original), "boundary edit preserves source");
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                bool rejected = false;
                try { ProjectBoundaries.SaveDisplacementCopy(source, edited, selected.Step, selected.Name, 1, invalid); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                check(rejected, "boundary rejects nonfinite displacement");
            }
            foreach (int invalid in new[] { 0, 4 })
            {
                bool rejected = false;
                try { ProjectBoundaries.SaveDisplacementCopy(source, edited, selected.Step, selected.Name, invalid, 0); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                check(rejected, "boundary rejects unsupported direction");
            }
            foreach (var target in new[] { (selected.Step, selected.Name, 2), ("missing", selected.Name, 1), (selected.Step, "missing", 1), (selected.Step, "fixed", 1) })
            {
                bool rejected = false;
                try { ProjectBoundaries.SaveDisplacementCopy(source, Path.Combine(root, "invalid.pma"), target.Item1, target.Item2, target.Item3, 0); }
                catch (NotSupportedException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "invalid.pma")), "boundary refuses free direction or missing selection");
            }
            var directions = ProjectBoundaries.ListDirections(source);
            check(directions.Length == 12 && directions.Count(d => d.Value == null) == 6,
                "direction editor represents free translations as null and excludes fixed supports");
            string constrained = Path.Combine(root, "constrained.pma");
            ProjectBoundaries.SaveDirectionCopy(source, constrained, selected.Step, selected.Name, 2, 0);
            check(ProjectBoundaries.ListDirections(constrained).SequenceEqual(directions.Select(d =>
                d.Step == selected.Step && d.Name == selected.Name && d.Direction == 2 ? d with { Value = 0 } : d)),
                "direction editor constrains only selected free translation");
            string released = Path.Combine(root, "released.pma");
            ProjectBoundaries.SaveDirectionCopy(constrained, released, selected.Step, selected.Name, 2, null);
            check(ProjectBoundaries.ListDirections(released).SequenceEqual(directions), "direction editor releases prescribed translation");
            bool emptyRejected = false;
            try { ProjectBoundaries.SaveDirectionCopy(source, Path.Combine(root, "empty.pma"), selected.Step, selected.Name, 1, null); }
            catch (NotSupportedException) { emptyRejected = true; }
            check(emptyRejected && !File.Exists(Path.Combine(root, "empty.pma")), "cannot release last constrained direction");
            var cleared = StaticProjectArchive.Open(constrained);
            check(cleared.Frd == null && cleared.Dat == null && File.ReadAllBytes(source).SequenceEqual(original),
                "direction edit invalidates results and preserves source");
            var targets = ProjectBoundaries.CreationTargets(source);
            check(targets.Length == 18 && targets.All(t => t.Direction >= 1 && t.Direction <= 3), "creation lists step node-set direction targets");
            string added = Path.Combine(root, "added.pma");
            ProjectBoundaries.AddCopy(source, added, selected.Step, "new-support", "RIGHT", 3, -0.02);
            var addedModel = StaticProjectArchive.Open(added);
            var addedBoundary = (DisplacementRotation)addedModel.Model.StepCollection.StepsList[1].BoundaryConditions["new-support"];
            check(addedBoundary.U3.Value == -0.02 && double.IsNaN(addedBoundary.U1.Value) && double.IsNaN(addedBoundary.UR3.Value)
                && addedBoundary.RegionName == "RIGHT", "new boundary preserves selected region and starts other directions free");
            check(ProjectBoundaries.List(added).Where(b => b.Name != "new-support").SequenceEqual(boundaries), "adding boundary preserves previous definitions");
            check(addedModel.Frd == null && addedModel.Dat == null && File.ReadAllBytes(source).SequenceEqual(original), "adding boundary clears results and preserves source");
            var fixedTargets = ProjectBoundaries.FixedCreationTargets(source);
            check(fixedTargets.Length == 6 && fixedTargets.Any(target => target.Step == selected.Step && target.Region == "RIGHT"),
                "fixed-support creation lists nonempty node sets in each step");
            check(ProjectBoundaries.ListFixed(source).SequenceEqual([new FixedSupport(model.StepCollection.StepsList[1].Name, "fixed", "LEFT")]),
                "fixed-support listing retains step, name and region identity");
            string fixedAdded = Path.Combine(root, "fixed-added.pma");
            ProjectBoundaries.AddFixedCopy(source, fixedAdded, selected.Step, "new-fixed", "RIGHT");
            var fixedAddedProject = StaticProjectArchive.Open(fixedAdded);
            check(fixedAddedProject.Model.StepCollection.StepsList.Single(step => step.Name == selected.Step)
                    .BoundaryConditions["new-fixed"] is FixedBC fixedBoundary && fixedBoundary.RegionName == "RIGHT" &&
                  fixedAddedProject.Frd == null && fixedAddedProject.Dat == null && File.ReadAllBytes(source).SequenceEqual(original),
                "fixed-support creation persists on the selected node set and invalidates stale results");
            string fixedInput = FileInOut.Output.CalculixFileWriter.GetInputText(fixedAddedProject.Model,
                FileInOut.Output.Calculix.ConvertPyramidsToEnum.Wedges);
            check(fixedInput.Contains("** Name: new-fixed") && fixedInput.Contains("RIGHT, 1, 6, 0"),
                "new fixed support reaches production CalculiX output");
            foreach (var invalid in new[] { (selected.Step, "FIXED", "RIGHT"), ("missing", "new-fixed", "RIGHT"),
                (selected.Step, "new-fixed", "missing"), (selected.Step, " ", "RIGHT") })
            {
                bool rejected = false;
                string output = Path.Combine(root, "invalid-fixed.pma");
                try { ProjectBoundaries.AddFixedCopy(source, output, invalid.Item1, invalid.Item2, invalid.Item3); }
                catch (Exception ex) when (ex is NotSupportedException or ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(output), "invalid fixed-support creation publishes nothing");
            }
            foreach (var invalid in new[] { (selected.Step, selected.Name, "RIGHT"), ("missing", "new", "RIGHT"),
                (selected.Step, "new", "missing"), (selected.Step, " ", "RIGHT") })
            {
                bool rejected = false;
                string output = Path.Combine(root, "invalid-add.pma");
                try { ProjectBoundaries.AddCopy(source, output, invalid.Item1, invalid.Item2, invalid.Item3, 1, 0); }
                catch (Exception ex) when (ex is NotSupportedException or ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(output), "invalid boundary creation publishes nothing");
            }
            foreach (var invalid in new[] { (0, 0d), (4, 0d), (1, double.NaN), (1, double.PositiveInfinity) })
            {
                bool rejected = false;
                try { ProjectBoundaries.AddCopy(source, added, selected.Step, "new", "RIGHT", invalid.Item1, invalid.Item2); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                check(rejected, "new boundary rejects invalid direction or value");
            }
            bool addOverwrite = false;
            try { ProjectBoundaries.AddCopy(source, source, selected.Step, "new", "RIGHT", 1, 0); }
            catch (IOException) { addOverwrite = true; }
            check(addOverwrite && File.ReadAllBytes(source).SequenceEqual(original), "new boundary cannot overwrite source");
            bool overwrite = false;
            try { ProjectBoundaries.SaveDisplacementCopy(source, source, selected.Step, selected.Name, 1, 0); }
            catch (IOException) { overwrite = true; }
            check(overwrite && File.ReadAllBytes(source).SequenceEqual(original), "boundary edit refuses overwrite");
            check(!Directory.GetFiles(root, ".edit-*").Any(), "boundary edit cleans staging files");
        }
        finally { Directory.Delete(root, true); }
    }
}
