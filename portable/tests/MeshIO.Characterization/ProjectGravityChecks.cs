using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectGravityChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "project gravity " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(2).Model;
            string withoutDensity = Path.Combine(root, "without-density.pma");
            StaticProjectArchive.Save(withoutDensity, model);
            check(ProjectGravity.CreationTargets(withoutDensity).Length == 0,
                "gravity creation excludes solid regions without density");
            model.Materials["ELASTIC"].AddProperty(new Density(new[] { new[] { 1d, 0d } }));
            string source = Path.Combine(root, "source.pma");
            StaticProjectArchive.Save(source, model, new byte[] { 1 }, new byte[] { 2 });
            var targets = ProjectGravity.CreationTargets(source);
            check(targets.Length == 2 && targets.All(target => target.Region == "SOLID" && target.Kind == "Element set"),
                "gravity creation lists density-backed solid region for each step");
            string edited = Path.Combine(root, "gravity.pma");
            ProjectGravity.AddCopy(source, edited, "load-1", "self-weight", "SOLID", 1, 0, 0);
            var gravity = ProjectGravity.List(edited).Single();
            check(gravity.Step == "load-1" && gravity.Name == "self-weight" && gravity.Region == "SOLID" &&
                gravity.A1 == 1 && gravity.A2 == 0 && gravity.A3 == 0,
                "gravity creation persists identity region and acceleration");
            var reopened = StaticProjectArchive.Open(edited);
            check(reopened.Frd == null && reopened.Dat == null &&
                FileInOut.Output.CalculixFileWriter.GetInputText(reopened.Model,
                    FileInOut.Output.Calculix.ConvertPyramidsToEnum.Wedges).Contains(", Grav,"),
                "gravity creation drops stale results and reaches production input");
            string changed = Path.Combine(root, "changed.pma");
            ProjectGravity.SaveCopy(edited, changed, "load-1", "self-weight", 2, 0, 0);
            check(ProjectGravity.List(changed).Single().A1 == 2,
                "gravity edit persists acceleration and preserves identity");
            string removed = Path.Combine(root, "removed.pma");
            ProjectGravity.RemoveCopy(changed, removed, "load-1", "self-weight");
            check(ProjectGravity.List(removed).Length == 0, "gravity removal persists");
            string rejected = Path.Combine(root, "rejected.pma");
            foreach (var values in new[] { (0d, 0d, 0d), (double.NaN, 0d, 0d) })
            {
                try { ProjectGravity.AddCopy(source, rejected, "load-1", "bad", "SOLID", values.Item1, values.Item2, values.Item3); check(false, "invalid gravity vector accepted"); }
                catch (ArgumentOutOfRangeException) { check(!File.Exists(rejected), "invalid gravity vector publishes no archive"); }
            }
            foreach (var identity in new[] { ("missing", "gravity", "SOLID"), ("load-1", "gravity", "missing") })
            {
                try { ProjectGravity.AddCopy(source, rejected, identity.Item1, identity.Item2, identity.Item3, 1, 0, 0); check(false, "invalid gravity target accepted"); }
                catch (NotSupportedException) { check(!File.Exists(rejected), "invalid gravity target publishes no archive"); }
            }
            try { ProjectGravity.AddCopy(edited, rejected, "load-1", "SELF-WEIGHT", "SOLID", 1, 0, 0); check(false, "duplicate gravity name accepted"); }
            catch (NotSupportedException) { check(!File.Exists(rejected), "duplicate gravity name publishes no archive"); }
            try { ProjectGravity.SaveCopy(edited, rejected, "load-1", "self-weight", 0, 0, 0); check(false, "zero gravity edit accepted"); }
            catch (ArgumentOutOfRangeException) { check(!File.Exists(rejected), "invalid gravity edit publishes no archive"); }
            try { ProjectGravity.SaveCopy(source, rejected, "load-1", "force", 1, 0, 0); check(false, "non-gravity edit accepted"); }
            catch (NotSupportedException) { check(!File.Exists(rejected), "non-gravity edit publishes no archive"); }
            try { ProjectGravity.RemoveCopy(source, rejected, "load-1", "force"); check(false, "non-gravity removal accepted"); }
            catch (NotSupportedException) { check(!File.Exists(rejected), "non-gravity removal publishes no archive"); }
        }
        finally { Directory.Delete(root, true); }
    }
}
