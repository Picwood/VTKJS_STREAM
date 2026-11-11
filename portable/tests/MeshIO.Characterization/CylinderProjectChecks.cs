using System.Text;
using System.Text.Json.Nodes;
using CaeModel;
using PrePoMax.Persistence;

internal static class CylinderProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "cylinder project " + Guid.NewGuid() + ".pma");
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var cylinder = new FeCylinder("tool-cylinder")
            {
                Center = new[] { 1d, 2d, 3d },
                Radius = 4,
                Direction = new[] { 0d, 0d, 2d },
                Height = 10,
                ExtraRadius = .5,
                Chamfer = new[] { 1d, 2d },
                TargetRadius = 3.5,
                NumberOfLayers = 4,
                Tolerance = .01,
                Active = false
            };
            model.Cylinders.Add(cylinder.Name, cylinder);

            StaticProjectArchive.Save(path, model);
            var archive = AnalysisArchive.Open(path);
            var metadata = JsonNode.Parse(archive.ModelMetadata!)!;
            check(metadata["Version"]!.GetValue<int>() == 28, "cylinder project uses metadata version 28");
            var restored = StaticProjectArchive.Open(path).Model.Cylinders[cylinder.Name];
            check(restored.Center.SequenceEqual(new[] { 1d, 2d, 3d }) &&
                restored.Direction.SequenceEqual(new[] { 0d, 0d, 2d }) && restored.Chamfer.SequenceEqual(new[] { 1d, 2d }),
                "cylinder metadata preserves center direction and chamfers");
            check(restored.Radius == 4 && restored.Height == 10 && restored.ExtraRadius == .5 &&
                restored.TargetRadius == 3.5 && restored.NumberOfLayers == 4 && restored.Tolerance == .01 && !restored.Active,
                "cylinder metadata preserves dimensions layers tolerance and active state");

            var legacyTwelve = metadata.DeepClone();
            legacyTwelve.AsObject().Remove("Cylinders");
            MetadataCompatibility.RemoveUserKeywords(legacyTwelve);
            legacyTwelve.AsObject().Remove("SurfaceInteractions");
            legacyTwelve.AsObject().Remove("ContactPairs");
            legacyTwelve.AsObject().Remove("Constraints");
            legacyTwelve["Version"] = 12;
            check(StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacyTwelve.ToJsonString()), archive.ModelMesh)
                    .Cylinders.Count == 0,
                "metadata v12 external-mesh projects remain readable");

            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(model))!;
            check(inline["Version"]!.GetValue<int>() == 27, "inline cylinder metadata uses version 27");
            void Reject(Action<JsonNode> mutate, string message)
            {
                var invalid = inline.DeepClone();
                mutate(invalid);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(json => json.AsObject().Remove("Cylinders"), "current metadata requires cylinder definitions");
            Reject(json => json["Cylinders"]![0]!["Kind"] = "UnknownCylinder",
                "cylinder metadata rejects unknown definitions");
            Reject(json => json["Cylinders"]![0]!["Values"]!["Center"]!.AsArray().RemoveAt(2),
                "cylinder metadata rejects malformed vectors");
            Reject(json => json["Cylinders"]![0]!["Values"]!["Direction"] = new JsonArray(0, 0, 0),
                "cylinder metadata rejects zero directions");
            Reject(json => json["Cylinders"]![0]!["Values"]!["Radius"] = "NaN",
                "cylinder metadata rejects nonfinite dimensions");
            Reject(json => { MetadataCompatibility.RemoveUserKeywords(json); json["Version"] = 11; }, "metadata v11 rejects cylinder definitions");
        }
        finally { File.Delete(path); }
    }
}
