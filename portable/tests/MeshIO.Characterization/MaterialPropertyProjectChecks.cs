using System.Text;
using System.Text.Json.Nodes;
using CaeGlobals;
using CaeModel;
using PrePoMax.Persistence;

internal static class MaterialPropertyProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "material properties " + Guid.NewGuid() + ".pma");
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var material = model.Materials["ELASTIC"];
            material.Properties.Clear();
            material.TemperatureDependent = true;
            material.AddProperty(new Density(new[] { new[] { 7.85e-9, 0d }, new[] { 7.7e-9, 100d } }));
            material.AddProperty(new Elastic(new[] { new[] { 210000d, .3, 0d }, new[] { 200000d, .29, 100d } }));
            material.AddProperty(new Plastic(new[] { new[] { 250d, 0d, 0d }, new[] { 300d, .1, 100d } })
                { Hardening = PlasticHardening.Combined });
            material.AddProperty(new ThermalExpansion(new[] { new[] { 1.2e-5, 0d }, new[] { 1.3e-5, 100d } })
                { ZeroTemperature = new EquationContainer(typeof(StringTemperatureConverter), -40) });
            material.AddProperty(new ThermalConductivity(new[] { new[] { 45d, 0d }, new[] { 40d, 100d } }));
            material.AddProperty(new SpecificHeat(new[] { new[] { 480d, 0d }, new[] { 500d, 100d } }));

            StaticProjectArchive.Save(path, model);
            var archive = AnalysisArchive.Open(path);
            var metadata = JsonNode.Parse(archive.ModelMetadata!)!;
            check(metadata["Version"]!.GetValue<int>() == 28 && archive.ModelMesh is { Length: > 8 },
                "material project uses metadata v28 with an external mesh");
            var reopened = StaticProjectArchive.Open(path).Model.Materials["ELASTIC"];
            check(reopened.Properties.Select(property => property.GetType()).SequenceEqual(new[] {
                    typeof(Density), typeof(Elastic), typeof(Plastic), typeof(ThermalExpansion),
                    typeof(ThermalConductivity), typeof(SpecificHeat) }),
                "material metadata preserves all supported property types and their order");
            check(reopened.TemperatureDependent && reopened.Properties.OfType<Density>().Single().DensityTemp[1][0].Value == 7.7e-9 &&
                reopened.Properties.OfType<Elastic>().Single().YoungsPoissonsTemp[1][1].Value == .29,
                "material metadata preserves density and elastic temperature tables");
            check(reopened.Properties.OfType<Plastic>().Single().Hardening == PlasticHardening.Combined &&
                reopened.Properties.OfType<Plastic>().Single().StressStrainTemp[1][1].Value == .1,
                "material metadata preserves plastic data and hardening");
            check(reopened.Properties.OfType<ThermalExpansion>().Single().ZeroTemperature.Value == -40 &&
                reopened.Properties.OfType<ThermalExpansion>().Single().ThermalExpansionTemp[1][0].Value == 1.3e-5,
                "material metadata preserves thermal expansion and reference temperature");
            check(reopened.Properties.OfType<ThermalConductivity>().Single().ThermalConductivityTemp[1][0].Value == 40 &&
                reopened.Properties.OfType<SpecificHeat>().Single().SpecificHeatTemp[1][0].Value == 500,
                "material metadata preserves conductivity and specific heat tables");

            var legacySix = metadata.DeepClone();
            MetadataCompatibility.UseLegacyMaterials(legacySix);
            legacySix["Version"] = 6;
            check(StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacySix.ToJsonString()), archive.ModelMesh)
                    .Materials["ELASTIC"].Properties.Select(property => property.GetType())
                    .SequenceEqual(new[] { typeof(Density), typeof(Elastic) }),
                "metadata v6 external-mesh projects remain readable");

            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(model))!;
            check(inline["Version"]!.GetValue<int>() == 27, "inline material metadata uses version 27");
            void Reject(Action<JsonNode> mutate, string message)
            {
                var invalid = inline.DeepClone();
                mutate(invalid);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(json => json["Materials"]![0]!["Properties"]![2]!["Kind"] = "Unknown",
                "material metadata rejects unknown property types");
            Reject(json => json["Materials"]![0]!["Properties"]![2]!["Hardening"] = 99,
                "material metadata rejects unknown plastic hardening");
            Reject(json => json["Materials"]![0]!["Properties"]![3]!["ReferenceTemperature"] = null,
                "material metadata requires the thermal expansion reference temperature");
            Reject(json => json["Materials"]![0]!["Properties"]!.AsArray().Add(
                    json["Materials"]![0]!["Properties"]![1]!.DeepClone()),
                "material metadata rejects duplicate property types");
            Reject(json => json["Materials"]![0]!["Properties"]![0]!["Data"]![0]!.AsArray().RemoveAt(1),
                "material metadata rejects malformed property table widths");
            Reject(json => json["Materials"]![0]!["Elastic"] =
                    json["Materials"]![0]!["Properties"]![1]!["Data"]!.DeepClone(),
                "material metadata rejects mixed legacy and current property schemas");
        }
        finally { File.Delete(path); }
    }
}
