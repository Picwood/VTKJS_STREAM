using CaeMesh;
using CaeMesh.Meshing;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class GmshImportChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "gmsh application boundary " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "solid.geo");
            string output = Path.Combine(root, "solid.pma");
            File.WriteAllText(source, "SetFactory(\"OpenCASCADE\"); Box(1) = {0,0,0,1,1,1};\n");
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing process path.");
            var cancelled = ProjectGmshImport.Prepare(source, output, 1000, .25, .5, executable);
            using var stop = new CancellationTokenSource();
            stop.Cancel();
            var result = await cancelled.RunAsync(stop.Token);
            bool stagedProjectRemains = Directory.EnumerateFiles(root, "*.prepomax-*.pma").Any();
            check(result.Status == ProjectGmshStatus.Cancelled && !File.Exists(output) && !stagedProjectRemains,
                "pre-cancelled Gmsh job launches no tool and leaves no published or staged project");

            string retainedProject = Path.Combine(root, "retained.pma");
            string remeshed = Path.Combine(root, "remeshed.pma");
            byte[] retainedBytes = File.ReadAllBytes(source);
            var retainedModel = new StaticModelWorkflow(1).Model;
            retainedModel.Mesh.MeshSetupItems.Add("GmshSizing", new MeshingParameters("GmshSizing")
            {
                MaxH = .5,
                MinH = .1,
                SecondOrder = false,
                RelativeSize = false
            });
            retainedModel.Mesh.MeshSetupItems.Add("TetrahedralGmsh", new TetrahedralGmsh("TetrahedralGmsh")
            {
                AlgorithmMesh3D = GmshAlgorithmMesh3DEnum.Frontal,
                OptimizeFirstOrderSolid = GmshOptimizeFirstOrderSolidEnum.Gmsh,
                NumberOfThreads = 1
            });
            StaticProjectArchive.Save(retainedProject, retainedModel,
                geometrySource: new ProjectGeometrySource("retained.geo", retainedBytes));
            var retainedSetup = ProjectService.Inspect(retainedProject).MeshingSetup;
            check(retainedSetup is { MaximumElementSize: .5, MinimumElementSize: .1, Algorithm: "Frontal", Optimize: true },
                "project archive round-trips bounded global Gmsh setup into inspection");
            var sizing = (MeshingParameters)retainedModel.Mesh.MeshSetupItems["GmshSizing"];
            sizing.SecondOrder = true;
            string quadraticProject = Path.Combine(root, "quadratic-setup.pma");
            StaticProjectArchive.Save(quadraticProject, retainedModel,
                geometrySource: new ProjectGeometrySource("retained.geo", retainedBytes));
            check(ProjectService.Inspect(quadraticProject).MeshingSetup is { SecondOrder: true } &&
                  System.Text.Json.Nodes.JsonNode.Parse(StaticModelMetadata.Serialize(retainedModel))!["Version"]!.GetValue<int>() == 29,
                "quadratic Gmsh setup survives archive reopen and uses versioned metadata");
            var quadraticMetadata = System.Text.Json.Nodes.JsonNode.Parse(StaticModelMetadata.Serialize(retainedModel))!;
            quadraticMetadata["Version"] = 27;
            bool oldQuadraticRejected = false;
            try { StaticModelMetadata.Deserialize(System.Text.Encoding.UTF8.GetBytes(quadraticMetadata.ToJsonString())); }
            catch (InvalidDataException) { oldQuadraticRejected = true; }
            check(oldQuadraticRejected, "older metadata cannot silently enable quadratic Gmsh settings");
            var localBox = new BoxMeshSize("GmshLocalBox")
            {
                MeshSize = .15, XMin = -.1, YMin = -.2, ZMin = -.3,
                XMax = .4, YMax = 1.1, ZMax = 1.2
            };
            retainedModel.Mesh.MeshSetupItems.Add(localBox.Name, localBox);
            string localProject = Path.Combine(root, "local-sizing.pma");
            StaticProjectArchive.Save(localProject, retainedModel,
                geometrySource: new ProjectGeometrySource("retained.geo", retainedBytes));
            var localSummary = ProjectService.Inspect(localProject);
            check(localSummary.MeshingSetup?.SizeBox == new ProjectGmshSizeBox(.15, -.1, -.2, -.3, .4, 1.1, 1.2),
                "local mesh box round-trips coordinates and size through external-mesh PMA inspection");
            var localMetadata = System.Text.Json.Nodes.JsonNode.Parse(StaticModelMetadata.Serialize(retainedModel))!;
            check(localMetadata["Version"]!.GetValue<int>() == 31 &&
                  StaticModelMetadata.Deserialize(StaticModelMetadata.Serialize(retainedModel)).Mesh.MeshSetupItems.Count == 3,
                "local sizing uses versioned metadata and round-trips with quadratic controls");
            var copiedBox = localBox.DeepCopy();
            copiedBox.MeshSize = .2; copiedBox.XMin = -1;
            check(localBox.MeshSize == .15 && localBox.XMin == -.1 && copiedBox.Name == localBox.Name,
                "coordinate sizing copy retains its type and independent values");
            string editedLocal = Path.Combine(root, "edited-local.pma");
            ProjectNodeSets.AddCopy(localProject, editedLocal, "BOX_TEST_NODES", new[] { 1 });
            check(ProjectService.Inspect(editedLocal).MeshingSetup == localSummary.MeshingSetup &&
                  ProjectService.Inspect(editedLocal).GeometrySource == localSummary.GeometrySource,
                "model editing preserves local sizing and exact retained CAD identity");
            var localJob = ProjectGmshRemesh.Prepare(localProject, Path.Combine(root, "local-remesh.pma"), executable);
            var localResult = await localJob.RunAsync(stop.Token);
            check(localResult.Status == ProjectGmshStatus.Cancelled,
                "stored local sizing prepares a remesh and observes pre-cancellation");
            foreach (int oldVersion in new[] { 27, 29 })
            {
                localMetadata["Version"] = oldVersion;
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(System.Text.Encoding.UTF8.GetBytes(localMetadata.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, "legacy metadata cannot silently enable local box sizing");
            }
            localMetadata["Version"] = 31;
            var boxValues = localMetadata["MeshSetupItems"]!.AsArray().Single(item =>
                item!["Kind"]!.GetValue<string>() == nameof(BoxMeshSize))!["Values"]!;
            foreach (var invalid in new[] { ("MeshSize", .05), ("MeshSize", .6), ("XMax", -.1), ("ZMin", double.NaN) })
            {
                var original = boxValues[invalid.Item1]!.DeepClone();
                boxValues[invalid.Item1] = double.IsNaN(invalid.Item2)
                    ? System.Text.Json.Nodes.JsonValue.Create("NaN") : System.Text.Json.Nodes.JsonValue.Create(invalid.Item2);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(System.Text.Encoding.UTF8.GetBytes(localMetadata.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, "local box metadata rejects nonfinite, inverted and globally inconsistent values");
                boxValues[invalid.Item1] = original;
            }
            var duplicateBox = localBox.DeepCopy();
            duplicateBox.Name = "SecondBox";
            retainedModel.Mesh.MeshSetupItems.Add(duplicateBox.Name, duplicateBox);
            bool duplicateRejected = false;
            try { StaticModelMetadata.Serialize(retainedModel); }
            catch (NotSupportedException) { duplicateRejected = true; }
            check(duplicateRejected, "bounded setup rejects multiple refinement boxes");
            retainedModel.Mesh.MeshSetupItems.Remove(duplicateBox.Name);
            retainedModel.Mesh.MeshSetupItems.Remove(localBox.Name);
            sizing.SecondOrder = false;
            string storedRemesh = Path.Combine(root, "stored-remesh.pma");
            var storedJob = ProjectGmshRemesh.Prepare(retainedProject, storedRemesh, executable);
            using var storedStop = new CancellationTokenSource();
            storedStop.Cancel();
            var storedResult = await storedJob.RunAsync(storedStop.Token);
            check(storedResult.Status == ProjectGmshStatus.Cancelled && !File.Exists(storedRemesh),
                "retained CAD remesh reuses stored Gmsh controls without explicit values");
            var remeshJob = ProjectGmshRemesh.Prepare(retainedProject, remeshed,
                new ProjectGmshOptions(.5, .1, ProjectGmshAlgorithm.Frontal, true), executable);
            using var remeshStop = new CancellationTokenSource();
            remeshStop.Cancel();
            var remeshResult = await remeshJob.RunAsync(remeshStop.Token);
            check(remeshResult.Status == ProjectGmshStatus.Cancelled && remeshJob.SourcePath == Path.GetFullPath(retainedProject) &&
                !File.Exists(remeshed), "retained CAD remesh preparation owns source data and observes cancellation");

            string projectWithoutGeometry = Path.Combine(root, "without-geometry.pma");
            StaticProjectArchive.Save(projectWithoutGeometry, new StaticModelWorkflow(1).Model);
            bool missingGeometryRejected = false;
            try { ProjectGmshRemesh.Prepare(projectWithoutGeometry, remeshed, new ProjectGmshOptions(.5), executable); }
            catch (NotSupportedException) { missingGeometryRejected = true; }
            check(missingGeometryRejected, "remesh rejects projects without retained CAD geometry");

            string mixedMaterialProject = Path.Combine(root, "mixed-materials.pma");
            StaticProjectArchive.Save(mixedMaterialProject, MultiPartProjectChecks.Create(),
                geometrySource: new ProjectGeometrySource("retained.geo", retainedBytes));
            bool mixedMaterialsRejected = false;
            try { ProjectGmshRemesh.Prepare(mixedMaterialProject, remeshed, new ProjectGmshOptions(.5), executable); }
            catch (NotSupportedException) { mixedMaterialsRejected = true; }
            check(mixedMaterialsRejected, "remesh rejects ambiguous solid-section material transfer");

            var unsupportedSetup = new StaticModelWorkflow(1).Model;
            unsupportedSetup.Mesh.MeshSetupItems.Add("bad", new MeshingParameters("bad")
                { MaxH = .5, SecondOrder = true });
            bool unsupportedSetupRejected = false;
            try { StaticModelMetadata.Serialize(unsupportedSetup); }
            catch (NotSupportedException) { unsupportedSetupRejected = true; }
            check(unsupportedSetupRejected, "metadata rejects mesh setup outside the bounded Gmsh pair");

            foreach (var options in new[]
            {
                new ProjectGmshOptions(.5, .6),
                new ProjectGmshOptions(.5, 0),
                new ProjectGmshOptions(.5, .1, (ProjectGmshAlgorithm)99),
                new ProjectGmshOptions(.5, SizeBox: new(.1, 0, 0, 0, 0, 1, 1)),
                new ProjectGmshOptions(.5, SizeBox: new(.1, 0, 0, 0, 1, double.PositiveInfinity, 1)),
                new ProjectGmshOptions(.5, SizeBox: new(0, 0, 0, 0, 1, 1, 1)),
                new ProjectGmshOptions(.5, SizeBox: new(.6, 0, 0, 0, 1, 1, 1)),
                new ProjectGmshOptions(.5, .2, SizeBox: new(.1, 0, 0, 0, 1, 1, 1))
            })
            {
                bool optionsRejected = false;
                try { ProjectGmshRemesh.Prepare(retainedProject, remeshed, options, executable); }
                catch (ArgumentException) { optionsRejected = true; }
                check(optionsRejected, "remesh rejects invalid bounded Gmsh controls");
            }

            foreach (var invalid in new (string source, string destination, double modulus, double ratio, double size)[]
            {
                (Path.Combine(root, "missing.step"), output, 1000, .25, .5),
                (source, Path.Combine(root, "solid.txt"), 1000, .25, .5),
                (source, output, 0, .25, .5),
                (source, output, 1000, .5, .5),
                (source, output, 1000, .25, double.NaN)
            })
            {
                bool rejected = false;
                try { ProjectGmshImport.Prepare(invalid.source, invalid.destination, invalid.modulus, invalid.ratio, invalid.size, executable); }
                catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException or ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(output), "Gmsh job rejects invalid source, destination or numeric input before publication");
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
