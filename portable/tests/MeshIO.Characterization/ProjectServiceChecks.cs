using PrePoMax.Application;
using PrePoMax.Persistence;
using CaeModel;

internal static class ProjectServiceChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "project service é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.pma");
            var geometry = new ProjectGeometrySource("service-source.step", [1, 2, 3, 4]);
            StaticProjectArchive.Save(source, MultiPartProjectChecks.Create(), geometrySource: geometry);
            byte[] original = File.ReadAllBytes(source);
            check(ProjectService.Inspect(source).Parts.Length == 2, "service inspects multipart project");
            var materials = ProjectMaterials.List(source);
            string edited = Path.Combine(root, "edited.pma");
            ProjectMaterials.SaveElasticCopy(source, edited, materials[0].Name, materials[0].YoungsModulus * 2, 0.25);
            var changed = ProjectMaterials.List(edited);
            check(changed[0].YoungsModulus == materials[0].YoungsModulus * 2 && changed[0].PoissonsRatio == 0.25,
                "material edit persists elastic values");
            check(changed.Skip(1).SequenceEqual(materials.Skip(1)), "material edit preserves other materials");
            check(File.ReadAllBytes(source).SequenceEqual(original), "material edit preserves source bytes");
            check(ProjectService.Inspect(edited).Parts.SequenceEqual(ProjectService.Inspect(source).Parts),
                "material edit preserves part names");
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, 0, -1 })
            {
                try { ProjectMaterials.SaveElasticCopy(source, Path.Combine(root, "invalid.pma"), materials[0].Name, invalid, 0.3); check(false, "invalid modulus accepted"); }
                catch (ArgumentOutOfRangeException) { check(!File.Exists(Path.Combine(root, "invalid.pma")), "invalid modulus creates no archive"); }
            }
            foreach (var invalid in new[] { double.NaN, -1, 0.5 })
            {
                try { ProjectMaterials.SaveElasticCopy(source, Path.Combine(root, "invalid.pma"), materials[0].Name, 1000, invalid); check(false, "invalid ratio accepted"); }
                catch (ArgumentOutOfRangeException) { check(!File.Exists(Path.Combine(root, "invalid.pma")), "invalid ratio creates no archive"); }
            }
            try { ProjectMaterials.SaveElasticCopy(source, source, materials[0].Name, 1000, 0.3); check(false, "source overwrite accepted"); }
            catch (IOException) { check(File.ReadAllBytes(source).SequenceEqual(original), "material edit refuses overwrite"); }
            check(!Directory.GetFiles(root, ".edit-*").Any(), "material edit removes failed staging files");
            string withResults = Path.Combine(root, "with-results.pma");
            // Opaque payloads exercise archive invalidation independently of the result reader.
            StaticProjectArchive.Save(withResults, MultiPartProjectChecks.Create(), new byte[] { 1 }, new byte[] { 2 });
            string withoutResults = Path.Combine(root, "without-results.pma");
            ProjectMaterials.SaveElasticCopy(withResults, withoutResults, materials[0].Name, 1000, 0.3);
            var reopened = StaticProjectArchive.Open(withoutResults);
            check(reopened.Frd == null && reopened.Dat == null, "material edit drops both stale result payloads");
            string densityEdited = Path.Combine(root, "density-edited.pma");
            ProjectMaterials.SaveElasticDensityCopy(withResults, densityEdited, materials[0].Name,
                materials[0].YoungsModulus, materials[0].PoissonsRatio, 1.25);
            var densityMaterial = ProjectMaterials.List(densityEdited).Single(material => material.Name == materials[0].Name);
            check(densityMaterial.Density == 1.25 && StaticProjectArchive.Open(densityEdited).Frd == null,
                "material density edit persists and drops stale results");
            string densityRemoved = Path.Combine(root, "density-removed.pma");
            ProjectMaterials.SaveElasticDensityCopy(densityEdited, densityRemoved, materials[0].Name,
                materials[0].YoungsModulus, materials[0].PoissonsRatio, null);
            check(ProjectMaterials.List(densityRemoved).Single(material => material.Name == materials[0].Name).Density == null,
                "material density can be removed independently");
            foreach (double invalidDensity in new[] { 0d, double.NaN })
            {
                try { ProjectMaterials.SaveElasticDensityCopy(source, Path.Combine(root, "invalid-density.pma"), materials[0].Name, 1000, 0.3, invalidDensity); check(false, "invalid density accepted"); }
                catch (ArgumentOutOfRangeException) { check(!File.Exists(Path.Combine(root, "invalid-density.pma")), "invalid density publishes no archive"); }
            }
            var sectionTargets = ProjectMaterials.ListSolidSections(source);
            check(sectionTargets.Length == 2 && sectionTargets.Any(target => target.Section == "solid" && target.Material == "ELASTIC") &&
                sectionTargets.Any(target => target.Section == "section2" && target.Material == "stiffer"),
                "material creation lists solid-section assignments");
            string createdMaterial = Path.Combine(root, "created-material.pma");
            ProjectMaterials.AddElasticAndAssignCopy(withResults, createdMaterial, "solid", "soft-alloy", 750, 0.22);
            var created = ProjectMaterials.List(createdMaterial);
            var assignments = ProjectMaterials.ListSolidSections(createdMaterial);
            check(created.Any(material => material.Name == "soft-alloy" && material.YoungsModulus == 750 && material.PoissonsRatio == 0.22) &&
                assignments.Single(target => target.Section == "solid").Material == "soft-alloy",
                "material creation persists elastic values and solid-section assignment");
            reopened = StaticProjectArchive.Open(createdMaterial);
            check(reopened.Frd == null && reopened.Dat == null &&
                reopened.Model.Sections["section2"].MaterialName == "stiffer",
                "material creation preserves other assignments and drops stale results");
            string rejectedMaterial = Path.Combine(root, "rejected-material.pma");
            foreach (var (section, material) in new[] { ("missing", "new"), ("solid", "elastic") })
            {
                try { ProjectMaterials.AddElasticAndAssignCopy(source, rejectedMaterial, section, material, 1000, 0.3); check(false, "invalid material creation accepted"); }
                catch (NotSupportedException) { check(!File.Exists(rejectedMaterial), "invalid material creation publishes no archive"); }
            }
            string reassignedMaterial = Path.Combine(root, "reassigned-material.pma");
            ProjectMaterials.AssignSolidSectionCopy(withResults, reassignedMaterial, "solid", "stiffer");
            reopened = StaticProjectArchive.Open(reassignedMaterial);
            check(reopened.Model.Sections["solid"].MaterialName == "stiffer" &&
                reopened.Model.Materials.Count == StaticProjectArchive.Open(source).Model.Materials.Count,
                "material reassignment persists without duplicating materials");
            check(reopened.Frd == null && reopened.Dat == null, "material reassignment drops stale results");
            string rejectedAssignment = Path.Combine(root, "rejected-assignment.pma");
            foreach (var (section, material) in new[] { ("missing", "stiffer"), ("solid", "missing"), ("solid", "ELASTIC") })
            {
                try { ProjectMaterials.AssignSolidSectionCopy(source, rejectedAssignment, section, material); check(false, "invalid material reassignment accepted"); }
                catch (NotSupportedException) { check(!File.Exists(rejectedAssignment), "invalid material reassignment publishes no archive"); }
            }
            var removalModel = MultiPartProjectChecks.Create();
            var unused = new Material("unused-elastic");
            unused.AddProperty(new Elastic(new[] { new[] { 3000d, 0.2, 0d } }));
            removalModel.Materials.Add(unused.Name, unused);
            string removalSource = Path.Combine(root, "removal-source.pma");
            StaticProjectArchive.Save(removalSource, removalModel, new byte[] { 1 }, new byte[] { 2 });
            string removedMaterial = Path.Combine(root, "removed-material.pma");
            ProjectMaterials.RemoveUnusedCopy(removalSource, removedMaterial, unused.Name);
            reopened = StaticProjectArchive.Open(removedMaterial);
            check(!reopened.Model.Materials.ContainsKey(unused.Name) &&
                reopened.Model.Sections.Values.Select(section => section.MaterialName).SequenceEqual(new[] { "ELASTIC", "stiffer" }),
                "unused material removal persists and preserves section assignments");
            check(reopened.Frd == null && reopened.Dat == null, "material removal drops stale results");
            string rejectedRemoval = Path.Combine(root, "rejected-removal.pma");
            foreach (string material in new[] { "missing", "ELASTIC" })
            {
                try { ProjectMaterials.RemoveUnusedCopy(removalSource, rejectedRemoval, material); check(false, "invalid material removal accepted"); }
                catch (NotSupportedException) { check(!File.Exists(rejectedRemoval), "invalid material removal publishes no archive"); }
            }
            var cancelled = ProjectService.PrepareSolve(source, root, Environment.ProcessPath);
            check(cancelled.Status == ProjectSolveStatus.Prepared, "job prepared without solver execution");
            using var stop = new CancellationTokenSource();
            stop.Cancel();
            var result = await cancelled.RunAsync(stop.Token);
            check(result.Status == ProjectSolveStatus.Cancelled && cancelled.Status == result.Status && result.Solver == null,
                "pre-cancelled job does not launch solver");
            check(result.ArchivePath == null && !File.Exists(Path.Combine(cancelled.JobDirectory, "result.pma")),
                "cancelled job does not publish archive");
            bool rejected = false;
            try { await cancelled.RunAsync(); } catch (InvalidOperationException) { rejected = true; }
            check(rejected, "project jobs reject repeat execution");
            var timeout = ProjectService.PrepareSolve(source, root, Environment.ProcessPath, TimeSpan.FromMilliseconds(250));
            File.WriteAllText(Path.Combine(timeout.JobDirectory, "test-sleep"), "");
            result = await timeout.RunAsync();
            check(result.Status == ProjectSolveStatus.TimedOut && timeout.Status == result.Status && result.ArchivePath == null,
                "service preserves timeout terminal status");
            var interrupted = ProjectService.PrepareSolve(source, root, Environment.ProcessPath);
            File.WriteAllText(Path.Combine(interrupted.JobDirectory, "test-sleep"), "");
            using var interrupt = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            result = await interrupted.RunAsync(interrupt.Token);
            check(result.Status == ProjectSolveStatus.Cancelled && result.Solver?.Status == CaeJob.JobStatus.Killed && result.ArchivePath == null,
                "service cancellation stops running solver");
            var failed = ProjectService.PrepareSolve(source, root, Environment.ProcessPath);
            result = await failed.RunAsync();
            check(result.Status == ProjectSolveStatus.Failed && result.Solver?.Process?.ExitCode == 17 && result.ArchivePath == null,
                "service preserves failed solver diagnostics");
            var empty = ProjectService.PrepareSolve(source, root, Environment.ProcessPath);
            File.WriteAllText(Path.Combine(empty.JobDirectory, "test-success"), "");
            result = await empty.RunAsync();
            check(result.Status == ProjectSolveStatus.Failed && result.Solver?.Process?.ExitCode == 0 && result.Error != null,
                "missing results turn successful solver into failed project job");
            var successful = ProjectService.PrepareSolve(source, root, Environment.ProcessPath);
            File.WriteAllText(Path.Combine(successful.JobDirectory, "test-success"), "");
            File.WriteAllText(Path.Combine(successful.JobDirectory, "test-results"), "");
            result = await successful.RunAsync();
            var solved = StaticProjectArchive.Open(result.ArchivePath!);
            check(result.Status == ProjectSolveStatus.Succeeded && solved.Frd is { Length: > 0 } && solved.Dat is { Length: > 0 } &&
                solved.GeometrySource is { } retained && retained.FileName == geometry.FileName && retained.Data.SequenceEqual(geometry.Data),
                "successful solve publication preserves retained geometry with new results");
            check(File.ReadAllBytes(source).SequenceEqual(original), "service jobs preserve source archive");
            check(Directory.GetDirectories(root).Length == 6, "jobs own separate artifact directories");
        }
        finally { Directory.Delete(root, true); }
    }
}
