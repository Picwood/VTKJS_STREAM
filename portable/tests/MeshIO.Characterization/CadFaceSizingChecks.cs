using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CaeMesh;
using CaeMesh.Meshing;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class CadFaceSizingChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "CAD face sizing é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes("retained CAD test source");
            var topology = CadTopologyCodec.ReadGmsh(Encoding.UTF8.GetBytes(CadTopologyChecks.Msh), bytes);
            var geometry = new ProjectGeometrySource("solid.step", bytes, topology);
            var model = new StaticModelWorkflow(1).Model;
            model.Mesh.MeshSetupItems.Add("Sizing", new MeshingParameters("Sizing")
                { MaxH = .5, MinH = .1, RelativeSize = false, SecondOrder = true });
            model.Mesh.MeshSetupItems.Add("Tetra", new TetrahedralGmsh("Tetra")
                { NumberOfThreads = 1, AlgorithmMesh3D = GmshAlgorithmMesh3DEnum.Delaunay });
            var face = new CadFaceMeshSize("FaceSizing")
            {
                FaceTag = 1, MeshSize = .15, TransitionDistance = .4,
                SourceSha256 = topology.SourceSha256, TopologySha256 = CadFaceSizingBinding.Fingerprint(topology, bytes)
            };
            model.Mesh.MeshSetupItems.Add(face.Name, face);
            string project = Path.Combine(root, "face.pma");
            StaticProjectArchive.Save(project, model, geometrySource: geometry);
            check(ProjectService.Inspect(project).MeshingSetup?.FaceSize == new ProjectGmshFaceSize(1, .15, .4),
                "CAD face refinement survives external-mesh archive reopen and project inspection");
            var snapshot = AnalysisArchive.Open(project);
            check(JsonNode.Parse(snapshot.ModelMetadata!)!["Version"]!.GetValue<int>() == 34 &&
                  JsonNode.Parse(StaticModelMetadata.Serialize(model))!["Version"]!.GetValue<int>() == 33,
                "CAD face refinement uses metadata v33/v34");
            check(ProjectService.CadSizingFaces(project).Select(entity => entity.Tag).SequenceEqual(new[] { 1, 2, 3, 4 }),
                "face sizing targets come from retained dimension-two topology entities");
            var copy = face.DeepCopy(); copy.FaceTag = 2; copy.MeshSize = .2;
            check(face.FaceTag == 1 && face.MeshSize == .15 && copy.SourceSha256 == face.SourceSha256,
                "CAD face setup copies preserve independent values and source identity");
            string edited = Path.Combine(root, "edited.pma");
            ProjectNodeSets.AddCopy(project, edited, "FACE_TEST", new[] { 1 });
            var editedSetup = StaticProjectArchive.Open(edited).Model.Mesh.MeshSetupItems.Values.OfType<CadFaceMeshSize>().Single();
            check(editedSetup.TopologySha256 == face.TopologySha256 && editedSetup.SourceSha256 == face.SourceSha256,
                "model editing preserves CAD face sizing and its topology binding");
            var executable = Environment.ProcessPath!;
            using var stop = new CancellationTokenSource(); stop.Cancel();
            string destination = Path.Combine(root, "cancelled.pma");
            var job = ProjectGmshRemesh.Prepare(project, destination, executable);
            check((await job.RunAsync(stop.Token)).Status == ProjectGmshStatus.Cancelled && !File.Exists(destination),
                "stored CAD face controls prepare a cancellable remesh without launching a tool");
            foreach (var invalid in new[]
            {
                new ProjectGmshFaceSize(0, .15, .4), new ProjectGmshFaceSize(999, .15, .4),
                new ProjectGmshFaceSize(1, .05, .4), new ProjectGmshFaceSize(1, .6, .4),
                new ProjectGmshFaceSize(1, double.NaN, .4), new ProjectGmshFaceSize(1, .15, 0),
                new ProjectGmshFaceSize(1, .15, double.PositiveInfinity)
            })
            {
                bool rejected = false;
                try { ProjectGmshRemesh.Prepare(project, destination, new(.5, .1, FaceSize: invalid), executable); }
                catch (ArgumentException) { rejected = true; }
                check(rejected, "face sizing rejects missing face tags and invalid global/transition limits before meshing");
            }
            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(model))!;
            inline["Version"] = 31;
            bool oldRejected = false;
            try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(inline.ToJsonString())); }
            catch (InvalidDataException) { oldRejected = true; }
            check(oldRejected, "older metadata cannot silently enable topology-specific face sizing");
            foreach (var incompatible in new[]
            {
                geometry with { Topology = null }, geometry with { FileName = "solid.geo" },
                geometry with { Topology = topology with { Entities = topology.Entities.Select(entity =>
                    entity.Dimension == 2 && entity.Tag == 1 ? entity with { Bounds = new[] { -.1, 0d, 0d, 1d, 1d, 0d } } : entity).ToArray() } }
            })
            {
                bool rejected = false;
                try { StaticProjectArchive.Save(Path.Combine(root, "bad.pma"), model, geometrySource: incompatible); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "bad.pma")),
                    "face refinement cannot be published with absent, unsupported or changed CAD topology");
            }
            face.SourceSha256 = new string('0', 64);
            bool bindingRejected = false;
            try { StaticProjectArchive.Save(Path.Combine(root, "bad.pma"), model, geometrySource: geometry); }
            catch (InvalidDataException) { bindingRejected = true; }
            check(bindingRejected, "face sizing cannot be rebound to another source hash");
            face.SourceSha256 = topology.SourceSha256;
            var duplicate = face.DeepCopy(); duplicate.Name = "DuplicateFace";
            model.Mesh.MeshSetupItems.Add(duplicate.Name, duplicate);
            bool duplicateRejected = false;
            try { StaticModelMetadata.Serialize(model); }
            catch (NotSupportedException) { duplicateRejected = true; }
            check(duplicateRejected, "bounded setup rejects multiple face refinements");
            model.Mesh.MeshSetupItems.Remove(duplicate.Name);
            model.Mesh.MeshSetupItems.Add("Box", new BoxMeshSize("Box") { MeshSize = .2 });
            string combined = Path.Combine(root, "combined.pma");
            StaticProjectArchive.Save(combined, model, geometrySource: geometry);
            check(ProjectService.Inspect(combined).MeshingSetup is { SizeBox: not null, FaceSize: not null },
                "global setup, quadratic order, coordinate box and CAD face refinement coexist");
            model.Mesh.MeshSetupItems.Remove("Box");
            // A rehashed metadata payload must still pass the cross-payload identity guard.
            using (var zip = ZipFile.Open(project, ZipArchiveMode.Update))
            {
                JsonNode metadata, manifest;
                using (var reader = new StreamReader(zip.GetEntry("model.json")!.Open())) metadata = JsonNode.Parse(reader.ReadToEnd())!;
                using (var reader = new StreamReader(zip.GetEntry("manifest.json")!.Open())) manifest = JsonNode.Parse(reader.ReadToEnd())!;
                var values = metadata["MeshSetupItems"]!.AsArray().Single(item => item!["Kind"]!.GetValue<string>() == nameof(CadFaceMeshSize))!["Values"]!;
                values["TopologySha256"] = new string('1', 64);
                byte[] changed = Encoding.UTF8.GetBytes(metadata.ToJsonString());
                var payload = manifest["Payloads"]!.AsArray().Single(item => item!["Name"]!.GetValue<string>() == "model.json")!;
                payload["Length"] = changed.Length; payload["Sha256"] = Convert.ToHexString(SHA256.HashData(changed));
                zip.GetEntry("model.json")!.Delete();
                using (var output = zip.CreateEntry("model.json").Open()) output.Write(changed);
                zip.GetEntry("manifest.json")!.Delete();
                using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open()); writer.Write(manifest.ToJsonString());
            }
            bool changedRejected = false;
            try { StaticProjectArchive.Open(project); }
            catch (InvalidDataException) { changedRejected = true; }
            check(changedRejected, "archive opening rejects a rehashed face identity that disagrees with retained topology");
            model.Mesh.MeshSetupItems.Remove(face.Name);
            foreach (var olderGeometry in new[] { geometry with { Topology = null }, geometry with { FileName = "solid.geo" } })
            {
                string old = Path.Combine(root, Guid.NewGuid() + ".pma");
                StaticProjectArchive.Save(old, model, geometrySource: olderGeometry);
                check(ProjectService.CadSizingFaces(old).Length == 0, "unsupported CAD sources expose no face sizing targets");
                bool refused = false;
                try { ProjectGmshRemesh.Prepare(old, destination, new(.5, FaceSize: new(1, .15, .4)), executable); }
                catch (NotSupportedException) { refused = true; }
                check(refused, "face sizing refuses sources without supported retained CAD topology");
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
