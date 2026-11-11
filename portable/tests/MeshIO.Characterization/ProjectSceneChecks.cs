using PrePoMax.Application;
using PrePoMax.Persistence;
using PrePoMax.Render.Contracts;
using PrePoMax.Render.Server;

internal static class ProjectSceneChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "scene bridge " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string project = Path.Combine(root, "model.pma"), output = Path.Combine(root, "scene.bin");
            StaticProjectArchive.Save(project, MultiPartProjectChecks.Create());
            var original = File.ReadAllBytes(project);
            var sceneModel = StaticProjectArchive.OpenModelForScene(project);
            var ordinaryModel = StaticProjectArchive.Open(project).Model;
            check(sceneModel.Mesh.Nodes.Count == ordinaryModel.Mesh.Nodes.Count &&
                  sceneModel.Mesh.Elements.Count == ordinaryModel.Mesh.Elements.Count &&
                  sceneModel.Mesh.Parts.Keys.SequenceEqual(ordinaryModel.Mesh.Parts.Keys),
                  "streamed scene-model open preserves mesh and ordered part identities");
            ProjectScene.Export(project, output);
            using var stream = File.OpenRead(output);
            var scene = HostSceneUploadReader.ReadAsync(stream, stream.Length,
                HostSceneUploadReader.DefaultMaximumBytes, default).GetAwaiter().GetResult();
            ContractValidator.Validate(scene.Manifest);
            check(scene.Manifest.Actors.Count == 2, "scene bridge accepts two actors in production reader");
            check(scene.Manifest.Actors[1].Name == "second_block", "scene bridge preserves part names");
            check(scene.Manifest.Bounds.SequenceEqual(new double[] { 0, 4, 0, 1, 0, 1 }), "scene bridge preserves model bounds");
            check(scene.Manifest.Resources.Where(r => r.Semantic == "sourceElementIds").Sum(r => r.ElementCount) == 24,
                "scene bridge exports twelve exterior triangles per cube");
            check(scene.Manifest.Resources.Where(r => r.Semantic == "sourcePointIds").Sum(r => r.ElementCount) == 16,
                "scene bridge retains node identity resources");
            string preview = Path.Combine(root, "preview.bin");
            ProjectScene.Export(project, preview, selectedNodes: new[] { 2, 3, 2 });
            using var previewStream = File.OpenRead(preview);
            var highlighted = HostSceneUploadReader.ReadAsync(previewStream, previewStream.Length,
                HostSceneUploadReader.DefaultMaximumBytes, default).GetAwaiter().GetResult();
            ContractValidator.Validate(highlighted.Manifest);
            check(highlighted.Manifest.Actors.Count == 3 && highlighted.Manifest.Actors.Last().Name == "Selected nodes",
                "selection marker actor accepted by production scene reader");
            check(highlighted.Manifest.Resources.Where(r => r.Semantic == "sourcePointIds").Sum(r => r.ElementCount) == 28,
                "selection markers deduplicate node IDs and add six vertices each");
            check(highlighted.Manifest.Resources.Where(r => r.Semantic == "sourceElementIds").Sum(r => r.ElementCount) == 40,
                "selection markers add eight triangles per selected node");
            string facePreview = Path.Combine(root, "face-preview.bin");
            ProjectScene.Export(project, facePreview, selectedFaces:
                [new ProjectElementFace(1, "S1"), new ProjectElementFace(1, "S2"), new ProjectElementFace(1, "S1")]);
            using var facePreviewStream = File.OpenRead(facePreview);
            var shaded = HostSceneUploadReader.ReadAsync(facePreviewStream, facePreviewStream.Length,
                HostSceneUploadReader.DefaultMaximumBytes, default).GetAwaiter().GetResult();
            ContractValidator.Validate(shaded.Manifest);
            check(shaded.Manifest.Actors.Count == 3 && shaded.Manifest.Actors.Last().Name == "Selected faces",
                "selected element faces add one shaded preview actor");
            check(shaded.Manifest.Resources.Where(r => r.Semantic == "sourceElementIds").Sum(r => r.ElementCount) == 28,
                "face preview deduplicates pairs and triangulates their topology");
            string empty = Path.Combine(root, "empty-preview.bin");
            ProjectScene.Export(project, empty, selectedNodes: Array.Empty<int>());
            check(File.ReadAllBytes(empty).SequenceEqual(File.ReadAllBytes(output)), "empty selection restores exact mesh scene");
            bool invalid = false;
            try { ProjectScene.Export(project, Path.Combine(root, "invalid.bin"), selectedNodes: new[] { 999 }); }
            catch (ArgumentException) { invalid = true; }
            check(invalid && !File.Exists(Path.Combine(root, "invalid.bin")), "unknown preview node rejected before publication");
            var before = File.ReadAllBytes(output);
            bool rejected = false;
            try { ProjectScene.Export(project, output); } catch (IOException) { rejected = true; }
            check(rejected && before.SequenceEqual(File.ReadAllBytes(output)), "scene export refuses overwrite");
            check(original.SequenceEqual(File.ReadAllBytes(project)), "scene export preserves project archive");
            check(Directory.GetFiles(root, "*.tmp").Length == 0, "scene export cleans temporary files");
        }
        finally { Directory.Delete(root, true); }
    }
}
