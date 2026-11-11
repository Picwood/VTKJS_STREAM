using System.Text;
using System.Text.Json.Nodes;
using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

internal static class ReferenceAmplitudeProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "reference amplitude " + Guid.NewGuid() + ".pma");
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var point = new FeReferencePoint("load-point", 1.25, 2.5, 3.75) { Active = true };
            model.Mesh.ReferencePoints.Add(point.Name, point);
            var amplitude = new AmplitudeTabular("ramp", new[] { new[] { 0d, 0d }, new[] { .5, .25 }, new[] { 1d, 1d } })
            {
                TimeSpan = AmplitudeTimeSpanEnum.TotalTime,
                ShiftX = .125,
                ShiftY = 2
            };
            model.Amplitudes.Add(amplitude.Name, amplitude);
            var load = new CLoad("point-force", point.Name, RegionTypeEnum.ReferencePointName, 4, 5, 6, false, false, 0)
            {
                AmplitudeName = amplitude.Name
            };
            model.StepCollection.StepsList[0].AddLoad(load);

            StaticProjectArchive.Save(path, model);
            var archive = AnalysisArchive.Open(path);
            var metadata = JsonNode.Parse(archive.ModelMetadata!)!;
            check(metadata["Version"]!.GetValue<int>() == 28, "reference-point project uses metadata version 28");
            var reopened = StaticProjectArchive.Open(path).Model;
            var restoredPoint = reopened.Mesh.ReferencePoints[point.Name];
            check(restoredPoint.X.Value == 1.25 && restoredPoint.Y.Value == 2.5 && restoredPoint.Z.Value == 3.75,
                "reference-point metadata preserves numeric coordinates");
            var restoredAmplitude = (AmplitudeTabular)reopened.Amplitudes[amplitude.Name];
            check(restoredAmplitude.TimeSpan == AmplitudeTimeSpanEnum.TotalTime && restoredAmplitude.ShiftX == .125 &&
                restoredAmplitude.ShiftY == 2 && restoredAmplitude.TimeAmplitude[1].SequenceEqual(new[] { .5, .25 }),
                "amplitude metadata preserves time span shifts and tabular values");
            var restoredLoad = (CLoad)reopened.StepCollection.StepsList[0].Loads[load.Name];
            check(restoredLoad.RegionType == RegionTypeEnum.ReferencePointName && restoredLoad.RegionName == point.Name &&
                restoredLoad.AmplitudeName == amplitude.Name,
                "loads retain reference-point and amplitude links");

            var legacyEight = metadata.DeepClone();
            legacyEight.AsObject().Remove("ReferencePoints");
            legacyEight.AsObject().Remove("Amplitudes");
            MetadataCompatibility.RemoveUserKeywords(legacyEight);
            legacyEight.AsObject().Remove("Cylinders");
            legacyEight.AsObject().Remove("SurfaceInteractions");
            legacyEight.AsObject().Remove("ContactPairs");
            legacyEight.AsObject().Remove("Constraints");
            legacyEight["Version"] = 8;
            var legacy = StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacyEight.ToJsonString()), archive.ModelMesh);
            check(legacy.Mesh.ReferencePoints.Count == 0 && legacy.Amplitudes.Count == 0,
                "metadata v8 external-mesh projects remain readable");

            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(model))!;
            check(inline["Version"]!.GetValue<int>() == 27, "inline reference-point metadata uses version 27");
            void Reject(Action<JsonNode> mutate, string message)
            {
                var invalid = inline.DeepClone();
                mutate(invalid);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(json => json.AsObject().Remove("ReferencePoints"),
                "current metadata requires reference-point definitions");
            Reject(json => json["ReferencePoints"]![0]!["Values"]!["X"] = "NaN",
                "reference-point metadata rejects nonfinite coordinates");
            Reject(json => json["Amplitudes"]![0]!["Kind"] = "UnknownAmplitude",
                "amplitude metadata rejects unknown definitions");
            Reject(json => json["Amplitudes"]![0]!["Values"]!["TimeSpan"] = 99,
                "amplitude metadata rejects unknown time spans");
            Reject(json => json["Amplitudes"]![0]!["Values"]!["TimeAmplitude"]![0]!.AsArray().RemoveAt(1),
                "amplitude metadata rejects malformed tabular rows");
            Reject(json => { MetadataCompatibility.RemoveUserKeywords(json); json["Version"] = 7; },
                "metadata v7 rejects version-9 definitions");
        }
        finally { File.Delete(path); }
    }
}
