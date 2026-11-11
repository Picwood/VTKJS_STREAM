using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectCentrifugalChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "project centrifugal " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(2).Model;
            string withoutDensity = Path.Combine(root, "without-density.pma");
            StaticProjectArchive.Save(withoutDensity, model);
            check(ProjectCentrifugal.CreationTargets(withoutDensity).Length == 0,
                "centrifugal creation excludes solid regions without density");
            model.Materials["ELASTIC"].AddProperty(new Density(new[] { new[] { 1d, 0d } }));
            string source = Path.Combine(root, "source.pma");
            StaticProjectArchive.Save(source, model, new byte[] { 1 }, new byte[] { 2 });
            var targets = ProjectCentrifugal.CreationTargets(source);
            check(targets.Length == 2 && targets.All(target => target.Region == "SOLID" && target.N3 == 1),
                "centrifugal creation lists density-backed 3D solid region for each step");
            string edited = Path.Combine(root, "centrifugal.pma");
            ProjectCentrifugal.AddCopy(source, edited, "load-1", "spin", "SOLID", .1, .2, .3, 0, 0, 2, 4);
            var spin = ProjectCentrifugal.List(edited).Single();
            check(spin.Step == "load-1" && spin.Name == "spin" && spin.Region == "SOLID" && spin.X == .1 &&
                spin.Y == .2 && spin.Z == .3 && spin.N3 == 2 && spin.RotationalSpeed == 4,
                "centrifugal creation persists identity axis and speed");
            var reopened = StaticProjectArchive.Open(edited);
            string input = FileInOut.Output.CalculixFileWriter.GetInputText(reopened.Model,
                FileInOut.Output.Calculix.ConvertPyramidsToEnum.Wedges);
            check(reopened.Frd == null && reopened.Dat == null && input.Contains(", CENTRIF, 16,") && input.Contains(", 0, 0, 1"),
                "centrifugal creation drops stale results and reaches normalized production input");
            string changed = Path.Combine(root, "changed.pma");
            ProjectCentrifugal.SaveCopy(edited, changed, "load-1", "spin", 1, 2, 3, 0, 1, 0, 5);
            var changedSpin = ProjectCentrifugal.List(changed).Single();
            check(changedSpin.X == 1 && changedSpin.N2 == 1 && changedSpin.RotationalSpeed == 5,
                "centrifugal edit persists point direction and speed");
            string removed = Path.Combine(root, "removed.pma");
            ProjectCentrifugal.RemoveCopy(changed, removed, "load-1", "spin");
            check(ProjectCentrifugal.List(removed).Length == 0, "centrifugal removal persists");
            string rejected = Path.Combine(root, "rejected.pma");
            foreach (var values in new[] { (0d, 0d, 0d, 1d), (double.NaN, 0d, 1d, 1d), (0d, 0d, 1d, 0d) })
            {
                try { ProjectCentrifugal.AddCopy(source, rejected, "load-1", "bad", "SOLID", 0, 0, 0, values.Item1, values.Item2, values.Item3, values.Item4); check(false, "invalid centrifugal values accepted"); }
                catch (ArgumentOutOfRangeException) { check(!File.Exists(rejected), "invalid centrifugal values publish no archive"); }
            }
            foreach (var identity in new[] { ("missing", "spin", "SOLID"), ("load-1", "spin", "missing") })
            {
                try { ProjectCentrifugal.AddCopy(source, rejected, identity.Item1, identity.Item2, identity.Item3, 0, 0, 0, 0, 0, 1, 1); check(false, "invalid centrifugal target accepted"); }
                catch (NotSupportedException) { check(!File.Exists(rejected), "invalid centrifugal target publishes no archive"); }
            }
            try { ProjectCentrifugal.AddCopy(edited, rejected, "load-1", "SPIN", "SOLID", 0, 0, 0, 0, 0, 1, 1); check(false, "duplicate centrifugal name accepted"); }
            catch (NotSupportedException) { check(!File.Exists(rejected), "duplicate centrifugal name publishes no archive"); }
            try { ProjectCentrifugal.SaveCopy(source, rejected, "load-1", "force", 0, 0, 0, 0, 0, 1, 1); check(false, "non-centrifugal edit accepted"); }
            catch (NotSupportedException) { check(!File.Exists(rejected), "non-centrifugal edit publishes no archive"); }
            try { ProjectCentrifugal.RemoveCopy(source, rejected, "load-1", "force"); check(false, "non-centrifugal removal accepted"); }
            catch (NotSupportedException) { check(!File.Exists(rejected), "non-centrifugal removal publishes no archive"); }
        }
        finally { Directory.Delete(root, true); }
    }
}
