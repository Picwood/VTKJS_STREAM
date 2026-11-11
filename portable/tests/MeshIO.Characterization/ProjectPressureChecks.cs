using System.Text;
using System.Text.Json.Nodes;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectPressureChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "pressure edits é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.pma");
            byte[] geometryBytes = Encoding.UTF8.GetBytes("SetFactory(\"OpenCASCADE\"); Box(1)={0,0,0,1,1,1};\n");
            StaticProjectArchive.Save(source, new StaticModelWorkflow(2).Model, new byte[] { 1 }, new byte[] { 2 },
                geometrySource: new ProjectGeometrySource("fixture cube.geo", geometryBytes));
            byte[] original = File.ReadAllBytes(source);
            var targets = ProjectPressures.CreationTargets(source);
            check(targets.Length == 2 && targets.All(target => target.Region == "LOADED") &&
                targets.Select(target => target.Step).Distinct().Count() == 2,
                "pressure creation offers valid element surfaces in each step");

            string added = Path.Combine(root, "added.pma");
            ProjectPressures.AddCopy(source, added, "load-1", "pressure", "LOADED", 2.5);
            var pressure = ProjectPressures.List(added).Single();
            check(pressure == new PressureLoad("load-1", "pressure", "LOADED", 2.5),
                "pressure creation preserves step, name, surface and magnitude");
            var reopened = StaticProjectArchive.Open(added);
            check(reopened.Frd == null && reopened.Dat == null &&
                reopened.Model.StepCollection.StepsList[0].Loads["pressure"] is DLoad,
                "pressure creation persists DLoad and removes stale results");
            check(reopened.GeometrySource is { } retained && retained.FileName == "fixture cube.geo" && retained.Data.SequenceEqual(geometryBytes),
                "project edit preserves retained geometry while removing stale results");
            check(File.ReadAllBytes(source).SequenceEqual(original), "pressure creation preserves source bytes");
            string input = CalculixFileWriter.GetInputText(reopened.Model, ConvertPyramidsToEnum.Wedges);
            check(input.Contains("** Name: pressure") && input.Contains("SOLID, P4, 2.5"),
                "created pressure reaches production CalculiX Dload output");

            var archive = AnalysisArchive.Open(added);
            var metadata = JsonNode.Parse(archive.ModelMetadata)!;
            check(metadata["Version"]!.GetValue<int>() == 28 && archive.ModelMesh is { Length: > 8 },
                "pressure project uses external-mesh metadata v28");
            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(reopened.Model))!;
            check(inline["Version"]!.GetValue<int>() == 27 && inline["Steps"]![0]!["Loads"]!.AsArray()
                .Any(load => load!["Kind"]!.GetValue<string>() == nameof(DLoad)),
                "inline metadata v27 identifies pressure loads");

            string edited = Path.Combine(root, "edited.pma");
            ProjectPressures.SaveMagnitudeCopy(added, edited, "load-1", "pressure", -1.25);
            check(ProjectPressures.List(edited).Single().Magnitude == -1.25,
                "pressure editor preserves assignment and changes signed magnitude");
            string removed = Path.Combine(root, "removed.pma");
            ProjectPressures.RemoveCopy(edited, removed, "load-1", "pressure");
            check(ProjectPressures.List(removed).Length == 0, "pressure removal preserves a valid project");
            var summary = ProjectService.Inspect(removed);
            check(summary.GeometrySource is { FileName: "fixture cube.geo", Bytes: > 0 } &&
                summary.GeometrySource.Sha256.Length == 64,
                "project inspection reports retained geometry identity, size and checksum");
            string extracted = Path.Combine(root, "restored.geo");
            ProjectService.ExportGeometry(removed, extracted);
            check(File.ReadAllBytes(extracted).SequenceEqual(geometryBytes),
                "geometry extraction restores the exact retained source bytes");
            bool extractionOverwrite = false;
            try { ProjectService.ExportGeometry(removed, extracted); }
            catch (IOException) { extractionOverwrite = true; }
            check(extractionOverwrite && File.ReadAllBytes(extracted).SequenceEqual(geometryBytes),
                "geometry extraction refuses to overwrite an existing file");

            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                bool rejected = false;
                try { ProjectPressures.AddCopy(source, Path.Combine(root, "invalid.pma"), "load-1", "new", "LOADED", invalid); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "invalid.pma")), "pressure creation rejects nonfinite magnitude");
            }
            foreach (var invalid in new[] { ("missing", "new", "LOADED"), ("load-1", "force", "LOADED"),
                ("load-1", "new", "missing"), ("load-1", " ", "LOADED") })
            {
                bool rejected = false;
                string output = Path.Combine(root, "invalid-selection.pma");
                try { ProjectPressures.AddCopy(source, output, invalid.Item1, invalid.Item2, invalid.Item3, 1); }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { rejected = true; }
                check(rejected && !File.Exists(output), "pressure creation rejects invalid selection or name");
            }
            bool overwrite = false;
            try { ProjectPressures.AddCopy(source, source, "load-1", "new", "LOADED", 1); }
            catch (IOException) { overwrite = true; }
            check(overwrite && File.ReadAllBytes(source).SequenceEqual(original), "pressure creation refuses overwrite");

            inline["Version"] = 23;
            bool oldRejected = false;
            try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(inline.ToJsonString())); }
            catch (InvalidDataException) { oldRejected = true; }
            check(oldRejected, "pre-v25 metadata rejects pressure loads");
        }
        finally { Directory.Delete(root, true); }
    }
}
