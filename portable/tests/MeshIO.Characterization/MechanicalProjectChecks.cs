using CaeGlobals;
using CaeModel;
using PrePoMax.Persistence;
using System.Text.Json.Nodes;

internal static class MechanicalProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "mechanical " + Guid.NewGuid() + ".pma");
        try
        {
            foreach (bool densityFirst in new[] { false, true })
            {
                var model = new StaticModelWorkflow(1).Model;
                var material = model.Materials["ELASTIC"];
                var elastic = material.Properties[0];
                material.Properties.Clear();
                if (densityFirst) material.AddProperty(new Density(new[] { new[] { 2.5, 0d } }));
                material.AddProperty(elastic);
                if (!densityFirst) material.AddProperty(new Density(new[] { new[] { 2.5, 0d } }));
                var step = model.StepCollection.StepsList[0];
                step.BoundaryConditions.Clear();
                step.AddBoundaryCondition(new FixedBC("clamp", "LEFT", RegionTypeEnum.NodeSetName, false));
                step.Loads.Clear();
                step.AddLoad(new CLoad("point", 2, 3, -4, 0, false, true, 30));
                step.AddLoad(new CLoad("set", "RIGHT", RegionTypeEnum.NodeSetName, 1, 2, 3, false, false, 0));
                step.AddLoad(new GravityLoad("gravity", "SOLID", RegionTypeEnum.ElementSetName, 0, -9.81, 0, false, false, 0));
                step.AddLoad(new CentrifLoad("spin", "SOLID", RegionTypeEnum.ElementSetName,
                    new[] { .1, .2, .3 }, new[] { 0d, 0d, 1d }, 12.5, false, false, false, 0));
                StaticProjectArchive.Save(path, model);
                var reopened = StaticProjectArchive.Open(path).Model;
                var restored = reopened.StepCollection.StepsList[0];
                check(restored.BoundaryConditions["clamp"] is FixedBC fixedBC && fixedBC.RegionName == "LEFT",
                    "mechanical metadata restores fixed boundary");
                var point = (CLoad)restored.Loads["point"];
                check(point.NodeId == 2 && point.RegionType == RegionTypeEnum.NodeId && point.Magnitude.Value == 5 && point.PhaseDeg.Value == 30,
                    "mechanical metadata preserves nodal force identity magnitude and phase");
                check(restored.Loads["set"].RegionName == "RIGHT" && restored.Loads["set"].RegionType == RegionTypeEnum.NodeSetName,
                    "mechanical metadata preserves set load region");
                check(((GravityLoad)restored.Loads["gravity"]).Magnitude.Value == 9.81,
                    "mechanical metadata recomputes gravity magnitude");
                var spin = (CentrifLoad)restored.Loads["spin"];
                check(spin.X.Value == .1 && spin.N3.Value == 1 && spin.RotationalSpeed2 == 156.25,
                    "mechanical metadata preserves centrifugal axis origin and speed");
                var properties = reopened.Materials["ELASTIC"].Properties;
                check((properties[0] is Density) == densityFirst && properties.OfType<Density>().Single().DensityTemp[0][0].Value == 2.5,
                    "mechanical metadata preserves density and material property order");
                point.F1 = new EquationContainer(typeof(StringForceConverter), 6);
                check(((CLoad)step.Loads["point"]).F1.Value == 3 && point.F1.Value == 6,
                    "reopened mechanical definitions can be edited independently");
                StaticProjectArchive.Save(path, reopened);
                check(((CLoad)StaticProjectArchive.Open(path).Model.StepCollection.StepsList[0].Loads["point"]).F1.Value == 6,
                    "mechanical project can be edited saved and reopened again");
            }
            var oldDocument = JsonNode.Parse(StaticModelMetadata.Serialize(new StaticModelWorkflow(1).Model))!;
            MetadataCompatibility.UseLegacyMaterials(oldDocument);
            oldDocument["Version"] = 1;
            foreach (var element in oldDocument["Elements"]!.AsArray()) element!.AsObject().Remove("Kind");
            oldDocument["Part"] = oldDocument["Parts"]![0]!["State"]!.DeepClone();
            oldDocument.AsObject().Remove("Parts");
            foreach (var material in oldDocument["Materials"]!.AsArray())
            {
                material!.AsObject().Remove("Density");
                material.AsObject().Remove("DensityFirst");
            }
            check(StaticModelMetadata.Deserialize(System.Text.Encoding.UTF8.GetBytes(oldDocument.ToJsonString())).StepCollection.StepsList.Count == 1,
                "metadata reader retains version 1 static projects");
            var current = StaticProjectArchive.Open(path).Model;
            var legacyTwo = JsonNode.Parse(StaticModelMetadata.Serialize(current))!;
            MetadataCompatibility.UseLegacyMaterials(legacyTwo);
            legacyTwo["Version"] = 2;
            foreach (var element in legacyTwo["Elements"]!.AsArray()) element!.AsObject().Remove("Kind");
            legacyTwo["Part"] = legacyTwo["Parts"]![0]!["State"]!.DeepClone();
            legacyTwo.AsObject().Remove("Parts");
            check(StaticModelMetadata.Deserialize(System.Text.Encoding.UTF8.GetBytes(legacyTwo.ToJsonString()))
                .StepCollection.StepsList[0].Loads["point"] is CLoad,
                "metadata reader retains version 2 mechanical projects");
            var invalid = JsonNode.Parse(StaticModelMetadata.Serialize(current))!;
            MetadataCompatibility.UseLegacyMaterials(invalid);
            invalid["Version"] = 1;
            bool rejected = false;
            try { StaticModelMetadata.Deserialize(System.Text.Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, "version 1 rejects version 2 mechanical definitions");
        }
        finally { File.Delete(path); }
    }
}
