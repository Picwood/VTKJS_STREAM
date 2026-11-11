using System.Text;
using System.Text.Json.Nodes;
using CaeGlobals;
using CaeModel;
using PrePoMax.Persistence;

internal static class ContactProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "contact project " + Guid.NewGuid() + ".pma");
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var interaction = new SurfaceInteraction("contact-law");
            interaction.AddProperty(new Friction(.2, 5000));
            interaction.AddProperty(new SurfaceBehavior
            {
                PressureOverclosureType = PressureOverclosureEnum.Tabular,
                C0 = .01,
                P0 = 10,
                K = 1000,
                Sinf = .1,
                PressureOverclosure = new[] { new[] { 0d, 0d }, new[] { 100d, .2 } }
            });
            interaction.AddProperty(new GapConductance(new[] {
                new[] { 10d, 0d, 0d }, new[] { 20d, 100d, 50d }
            }));
            model.SurfaceInteractions.Add(interaction.Name, interaction);
            var pair = new ContactPair("contact", interaction.Name, ContactPairMethod.NodeToSurface,
                smallSliding: true, adjust: true, adjustmentSize: .02,
                "LOADED", RegionTypeEnum.SurfaceName, "LOADED", RegionTypeEnum.SurfaceName);
            model.ContactPairs.Add(pair.Name, pair);
            var output = new ContactFieldOutput("contact-output", ContactFieldVariable.CDIS | ContactFieldVariable.CSTR)
            {
                Active = false,
                LastIterations = true,
                ContactElements = true,
                Global = false
            };
            model.StepCollection.StepsList[0].AddFieldOutput(output);

            StaticProjectArchive.Save(path, model);
            var archive = AnalysisArchive.Open(path);
            var metadata = JsonNode.Parse(archive.ModelMetadata!)!;
            check(metadata["Version"]!.GetValue<int>() == 28, "contact project uses metadata version 28");
            var reopened = StaticProjectArchive.Open(path).Model;
            var restoredInteraction = reopened.SurfaceInteractions[interaction.Name];
            check(restoredInteraction.Properties.Select(property => property.GetType()).SequenceEqual(
                    new[] { typeof(Friction), typeof(SurfaceBehavior), typeof(GapConductance) }),
                "contact metadata preserves interaction-property order and types");
            var behavior = restoredInteraction.Properties.OfType<SurfaceBehavior>().Single();
            var friction = restoredInteraction.Properties.OfType<Friction>().Single();
            var conductance = restoredInteraction.Properties.OfType<GapConductance>().Single();
            check(behavior.PressureOverclosureType == PressureOverclosureEnum.Tabular &&
                behavior.PressureOverclosure[1].SequenceEqual(new[] { 100d, .2 }) && behavior.K == 1000,
                "contact metadata preserves pressure-overclosure behavior");
            check(friction.Coefficient == .2 && friction.StickSlope == 5000 &&
                conductance.GapConductanceType == GapConductanceEnum.Tabular &&
                conductance.ConductnancePressureTemp[1].SequenceEqual(new[] { 20d, 100d, 50d }),
                "contact metadata preserves friction and gap conductance");
            var restoredPair = reopened.ContactPairs[pair.Name];
            check(restoredPair.SurfaceInteractionName == interaction.Name &&
                restoredPair.Method == ContactPairMethod.NodeToSurface && restoredPair.SmallSliding &&
                restoredPair.Adjust && restoredPair.AdjustmentSize == .02 &&
                restoredPair.MasterRegionName == "LOADED" && restoredPair.SlaveRegionName == "LOADED",
                "contact metadata preserves pair settings and named links");
            var restoredOutput = (ContactFieldOutput)reopened.StepCollection.StepsList[0].FieldOutputs[output.Name];
            check(restoredOutput.Variables == output.Variables && !restoredOutput.Active && restoredOutput.LastIterations &&
                restoredOutput.ContactElements && !restoredOutput.Global,
                "contact metadata preserves contact field-output variables and settings");

            var legacyFourteen = metadata.DeepClone();
            legacyFourteen.AsObject().Remove("SurfaceInteractions");
            legacyFourteen.AsObject().Remove("ContactPairs");
            legacyFourteen.AsObject().Remove("Constraints");
            MetadataCompatibility.RemoveUserKeywords(legacyFourteen);
            var legacyFields = legacyFourteen["Steps"]![0]!["Fields"]!.AsArray();
            legacyFields.Remove(legacyFields.Single(field => field!["Kind"]!.GetValue<string>() == nameof(ContactFieldOutput)));
            legacyFourteen["Version"] = 14;
            var legacy = StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacyFourteen.ToJsonString()), archive.ModelMesh);
            check(legacy.SurfaceInteractions.Count == 0 && legacy.ContactPairs.Count == 0,
                "metadata v14 external-mesh projects remain readable");

            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(model))!;
            check(inline["Version"]!.GetValue<int>() == 27, "inline contact metadata uses version 27");
            void Reject(Action<JsonNode> mutate, string message)
            {
                var invalid = inline.DeepClone();
                mutate(invalid);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(json => json.AsObject().Remove("ContactPairs"), "current metadata requires contact-pair definitions");
            Reject(json => json["SurfaceInteractions"]![0]!["Properties"]![0]!["Kind"] = "UnknownProperty",
                "contact metadata rejects unknown interaction properties");
            Reject(json => json["SurfaceInteractions"]![0]!["Properties"]!.AsArray().Add(
                    json["SurfaceInteractions"]![0]!["Properties"]![0]!.DeepClone()),
                "contact metadata rejects duplicate interaction properties");
            Reject(json => json["SurfaceInteractions"]![0]!["Properties"]![1]!["Values"]!["PressureOverclosure"]![0]!
                    .AsArray().RemoveAt(1),
                "contact metadata rejects malformed pressure-overclosure tables");
            Reject(json => json["ContactPairs"]![0]!["Values"]!["Method"] = 99,
                "contact metadata rejects unknown contact methods");
            Reject(json => json["ContactPairs"]![0]!["Values"]!["SurfaceInteractionName"] = "missing",
                "contact metadata rejects missing interaction links");
            Reject(json => json["ContactPairs"]![0]!["Values"]!["MasterRegionName"] = "missing",
                "contact metadata rejects missing surface links");
            Reject(json => json["Steps"]![0]!["Fields"]!.AsArray()
                    .Single(field => field!["Kind"]!.GetValue<string>() == nameof(ContactFieldOutput))!["Values"]!["Variables"] = 16,
                "contact metadata rejects unknown contact field-output variables");
            Reject(json => { MetadataCompatibility.RemoveUserKeywords(json); json["Version"] = 17; }, "metadata v17 rejects contact field outputs");
            Reject(json => { MetadataCompatibility.RemoveUserKeywords(json); json["Version"] = 13; }, "metadata v13 rejects contact definitions");
        }
        finally { File.Delete(path); }
    }
}
