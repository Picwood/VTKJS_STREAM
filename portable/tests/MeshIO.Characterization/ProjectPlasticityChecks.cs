using System.Text;
using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectPlasticityChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "project plasticity é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            string source = Path.Combine(root, "source.pma");
            var geometry = new ProjectGeometrySource("plastic.step", [1, 2, 3, 4]);
            StaticProjectArchive.Save(source, model, frd: [1], dat: [2], geometrySource: geometry);
            byte[] original = File.ReadAllBytes(source);
            var initial = ProjectMaterials.ListPlasticity(source).Single();
            check(initial.Name == "ELASTIC" && initial.Hardening == null && initial.Points.Length == 0,
                "plasticity catalog exposes an eligible material without a plastic property");

            string added = Path.Combine(root, "added.pma");
            PlasticPoint[] points = [new(100, 0), new(120, .01), new(150, .05)];
            ProjectMaterials.SavePlasticityCopy(source, added, "ELASTIC", PlasticHardening.Kinematic, points);
            var listed = ProjectMaterials.ListPlasticity(added).Single();
            var reopened = StaticProjectArchive.Open(added);
            var plastic = reopened.Model.Materials["ELASTIC"].Properties.OfType<Plastic>().Single();
            check(listed.Hardening == "Kinematic" && listed.Points.SequenceEqual(points) &&
                  plastic.Hardening == PlasticHardening.Kinematic &&
                  reopened.Model.Materials["ELASTIC"].Properties.IndexOf(plastic) == 1,
                "plasticity add persists hardening and an ordered multiline table after elastic data");
            string deck = Encoding.UTF8.GetString(AnalysisArchive.Open(added).Input);
            check(deck.Contains("*Plastic, Hardening=Kinematic") && deck.Contains("100, 0") &&
                  deck.Contains("150, 0.05"),
                "plasticity edit uses the production CalculiX keyword writer");
            check(reopened.Frd == null && reopened.Dat == null && reopened.GeometrySource!.Data.SequenceEqual(geometry.Data),
                "plasticity edit invalidates stale results and preserves retained geometry");

            string replaced = Path.Combine(root, "replaced.pma");
            PlasticPoint[] replacement = [new(90, 0), new(130, .02)];
            ProjectMaterials.SavePlasticityCopy(added, replaced, "ELASTIC", PlasticHardening.Combined, replacement);
            listed = ProjectMaterials.ListPlasticity(replaced).Single();
            check(listed.Hardening == "Combined" && listed.Points.SequenceEqual(replacement) &&
                  StaticProjectArchive.Open(replaced).Model.Materials["ELASTIC"].Properties.OfType<Plastic>().Count() == 1,
                "plasticity replacement changes the hardening/table without duplicating the property");

            string removed = Path.Combine(root, "removed.pma");
            ProjectMaterials.RemovePlasticityCopy(replaced, removed, "ELASTIC");
            check(ProjectMaterials.ListPlasticity(removed).Single().Points.Length == 0 &&
                  !Encoding.UTF8.GetString(AnalysisArchive.Open(removed).Input).Contains("*Plastic"),
                "plasticity removal preserves the material and removes its production keyword");

            bool overwrite = false;
            try { ProjectMaterials.SavePlasticityCopy(source, source, "ELASTIC", PlasticHardening.Isotropic, points); }
            catch (IOException) { overwrite = true; }
            check(overwrite && File.ReadAllBytes(source).SequenceEqual(original),
                "plasticity editing refuses source overwrite and preserves source bytes");

            PlasticPoint[][] invalidTables =
            [
                [],
                [new(100, .1)],
                [new(100, 0), new(120, 0)],
                [new(100, 0), new(120, -.1)],
                [new(0, 0)],
                [new(double.NaN, 0)],
                [new(100, 0), new(120, double.PositiveInfinity)]
            ];
            foreach (var invalid in invalidTables)
            {
                string rejected = Path.Combine(root, "invalid.pma");
                bool failed = false;
                try { ProjectMaterials.SavePlasticityCopy(source, rejected, "ELASTIC", PlasticHardening.Isotropic, invalid); }
                catch (ArgumentException) { failed = true; }
                check(failed && !File.Exists(rejected),
                    "plasticity rejects empty, non-finite, nonpositive and non-increasing tables before publication");
            }
            string bounded = Path.Combine(root, "bounded.pma");
            bool tooMany = false;
            try
            {
                ProjectMaterials.SavePlasticityCopy(source, bounded, "ELASTIC", PlasticHardening.Isotropic,
                    Enumerable.Range(0, 1001).Select(i => new PlasticPoint(100 + i, i / 1000d)).ToArray());
            }
            catch (ArgumentException) { tooMany = true; }
            check(tooMany && !File.Exists(bounded), "plasticity tables are bounded to 1,000 points");
            bool unknownHardening = false;
            try { ProjectMaterials.SavePlasticityCopy(source, bounded, "ELASTIC", (PlasticHardening)99, points); }
            catch (ArgumentOutOfRangeException) { unknownHardening = true; }
            check(unknownHardening && !File.Exists(bounded), "plasticity rejects an unknown hardening rule");
            bool unknownMaterial = false;
            try { ProjectMaterials.SavePlasticityCopy(source, bounded, "missing", PlasticHardening.Isotropic, points); }
            catch (NotSupportedException) { unknownMaterial = true; }
            check(unknownMaterial && !File.Exists(bounded), "plasticity rejects an unknown material");
            bool missingPlasticity = false;
            try { ProjectMaterials.RemovePlasticityCopy(source, bounded, "ELASTIC"); }
            catch (NotSupportedException) { missingPlasticity = true; }
            check(missingPlasticity && !File.Exists(bounded), "plasticity removal requires an existing property");

            var temperatureModel = new StaticModelWorkflow(1).Model;
            temperatureModel.Materials["ELASTIC"].TemperatureDependent = true;
            string temperature = Path.Combine(root, "temperature.pma");
            StaticProjectArchive.Save(temperature, temperatureModel);
            bool temperatureRejected = false;
            try { ProjectMaterials.SavePlasticityCopy(temperature, bounded, "ELASTIC", PlasticHardening.Isotropic, points); }
            catch (NotSupportedException) { temperatureRejected = true; }
            check(temperatureRejected && !File.Exists(bounded),
                "non-temperature plasticity editor refuses a temperature-dependent material");
            check(!Directory.EnumerateFiles(root, ".edit-*.pma").Any(),
                "plasticity success and failure paths leave no edit staging files");
        }
        finally { Directory.Delete(root, true); }
    }
}
