using System.Text;
using System.Text.Json.Nodes;
using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

internal static class ConstraintProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "constraint project " + Guid.NewGuid() + ".pma");
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var fixedSurface = new FeSurface("FIXED", "LEFT");
            fixedSurface.AddElementFace(FeFaceName.S6, "SOLID");
            model.Mesh.Surfaces.Add(fixedSurface.Name, fixedSurface);
            var point = new FeReferencePoint("constraint-point", .5, .5, .5);
            model.Mesh.ReferencePoints.Add(point.Name, point);

            var tie = new Tie("bond", .05, false, "LOADED", RegionTypeEnum.SurfaceName,
                fixedSurface.Name, RegionTypeEnum.SurfaceName, false) { Active = false };
            var body = new RigidBody("rigid", point.Name, "LEFT", RegionTypeEnum.NodeSetName, false);
            var spring = new SurfaceSpring("support", "LOADED", RegionTypeEnum.SurfaceName, false, false)
            {
                StiffnessPerArea = true
            };
            spring.K1.SetEquationFromValue(100);
            spring.K2.SetEquationFromValue(-20);
            spring.K3.SetEquationFromValue(30);
            model.Constraints.Add(tie.Name, tie);
            model.Constraints.Add(body.Name, body);
            model.Constraints.Add(spring.Name, spring);

            StaticProjectArchive.Save(path, model);
            var archive = AnalysisArchive.Open(path);
            var metadata = JsonNode.Parse(archive.ModelMetadata!)!;
            check(metadata["Version"]!.GetValue<int>() == 28, "constraint project uses metadata version 28");
            var reopened = StaticProjectArchive.Open(path).Model;
            check(reopened.Constraints.Values.Select(value => value.GetType()).SequenceEqual(
                    new[] { typeof(Tie), typeof(RigidBody), typeof(SurfaceSpring) }),
                "constraint metadata preserves definition order and types");
            var restoredTie = (Tie)reopened.Constraints[tie.Name];
            check(!restoredTie.Active && restoredTie.PositionTolerance.Value == .05 && !restoredTie.Adjust &&
                restoredTie.MasterRegionName == "LOADED" && restoredTie.SlaveRegionName == fixedSurface.Name,
                "constraint metadata preserves tie settings and surface links");
            var restoredBody = (RigidBody)reopened.Constraints[body.Name];
            check(restoredBody.ReferencePointName == point.Name && restoredBody.RegionName == "LEFT" &&
                restoredBody.RegionType == RegionTypeEnum.NodeSetName,
                "constraint metadata preserves rigid-body links");
            var restoredSpring = (SurfaceSpring)reopened.Constraints[spring.Name];
            check(restoredSpring.RegionName == "LOADED" && restoredSpring.StiffnessPerArea &&
                restoredSpring.K1.Value == 100 && restoredSpring.K2.Value == -20 && restoredSpring.K3.Value == 30,
                "constraint metadata preserves surface-spring settings and stiffness");

            var legacySixteen = metadata.DeepClone();
            legacySixteen.AsObject().Remove("Constraints");
            MetadataCompatibility.RemoveUserKeywords(legacySixteen);
            legacySixteen["Version"] = 16;
            var legacy = StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacySixteen.ToJsonString()), archive.ModelMesh);
            check(legacy.Constraints.Count == 0, "metadata v16 external-mesh projects remain readable");

            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(model))!;
            check(inline["Version"]!.GetValue<int>() == 27, "inline constraint metadata uses version 27");
            void Reject(Action<JsonNode> mutate, string message)
            {
                var invalid = inline.DeepClone();
                mutate(invalid);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(json => json.AsObject().Remove("Constraints"), "current metadata requires constraint definitions");
            Reject(json => json["Constraints"]![0]!["Kind"] = "UnknownConstraint",
                "constraint metadata rejects unknown definitions");
            Reject(json => json["Constraints"]![0]!["Values"]!["MasterRegionName"] = "missing",
                "tie metadata rejects missing surface links");
            Reject(json => json["Constraints"]![0]!["Values"]!["SlaveRegionName"] = "LOADED",
                "tie metadata rejects identical surfaces");
            Reject(json => json["Constraints"]![0]!["Values"]!["PositionTolerance"] = -1,
                "tie metadata rejects negative tolerances");
            Reject(json => json["Constraints"]![1]!["Values"]!["ReferencePointName"] = "missing",
                "rigid-body metadata rejects missing reference points");
            Reject(json => json["Constraints"]![1]!["Values"]!["RegionType"] = 99,
                "rigid-body metadata rejects unknown region types");
            Reject(json => json["Constraints"]![2]!["Values"]!["K1"] = "NaN",
                "surface-spring metadata rejects nonfinite stiffness");
            Reject(json => { MetadataCompatibility.RemoveUserKeywords(json); json["Version"] = 15; }, "metadata v15 rejects constraint definitions");
        }
        finally { File.Delete(path); }
    }
}
