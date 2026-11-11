using System.Text;
using System.Text.Json.Nodes;
using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

internal static class QuadraticProjectChecks
{
    static FeModel Create()
    {
        var nodes = new Dictionary<int, FeNode>();
        var elements = new Dictionary<int, FeElement>();
        int nextNode = 1;
        int[] AddNodes(int count, double offset)
        {
            var ids = new int[count];
            for (int i = 0; i < count; i++)
            {
                ids[i] = nextNode;
                nodes.Add(nextNode, new FeNode(nextNode++, offset + i % 4, i / 4, i % 3));
            }
            return ids;
        }
        elements.Add(101, new ParabolicTetraElement(101, AddNodes(10, 0)) { PartId = 11 });
        elements.Add(202, new ParabolicWedgeElement(202, AddNodes(15, 10)) { PartId = 22 });
        elements.Add(303, new ParabolicHexaElement(303, AddNodes(20, 20)) { PartId = 33 });
        var mesh = new FeMesh(nodes, elements, MeshRepresentation.Mesh);
        var parts = mesh.Parts.Values.Cast<MeshPart>().ToDictionary(part => part.PartId);
        parts[11].ParabolicTetraType = FeElementTypeParabolicTetra.C3D10T;
        parts[22].ParabolicWedgeType = FeElementTypeParabolicWedge.C3D15;
        parts[33].ParabolicHexaType = FeElementTypeParabolicHexa.C3D20R;
        mesh.NodeSets.Add("ALL", new FeNodeSet("ALL", nodes.Keys.ToArray()));
        foreach (var element in elements) mesh.ElementSets.Add("E" + element.Key, new FeElementSet("E" + element.Key, new[] { element.Key }));

        var model = new FeModel("quadratic solids", null, false);
        model.Properties.ModelSpace = ModelSpaceEnum.ThreeD;
        model.SetMesh(mesh);
        var material = new Material("elastic");
        material.AddProperty(new Elastic(new[] { new[] { 1000d, .25, 0d } }));
        model.Materials.Add(material.Name, material);
        foreach (var part in mesh.Parts.Values)
            model.Sections.Add("section-" + part.PartId,
                new SolidSection("section-" + part.PartId, material.Name, part.Name, RegionTypeEnum.PartName, 1, false));
        var step = new StaticStep("load", false) { Nlgeom = true };
        step.AddFieldOutput(new NodalFieldOutput("displacement", NodalFieldVariable.U));
        step.AddFieldOutput(new ElementFieldOutput("stress", ElementFieldVariable.S));
        step.AddHistoryOutput(new NodalHistoryOutput("history", NodalHistoryVariable.U, "ALL", RegionTypeEnum.NodeSetName));
        model.StepCollection.AddStep(step);
        return model;
    }

    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "quadratic project " + Guid.NewGuid() + ".pma");
        try
        {
            var original = Create();
            StaticProjectArchive.Save(path, original);
            var reopened = StaticProjectArchive.Open(path).Model;
            check(reopened.Mesh.Elements[101] is ParabolicTetraElement && reopened.Mesh.Elements[202] is ParabolicWedgeElement &&
                reopened.Mesh.Elements[303] is ParabolicHexaElement, "PMA v5 preserves quadratic solid topology");
            var parts = reopened.Mesh.Parts.Values.Cast<MeshPart>().ToDictionary(part => part.PartId);
            check(parts[11].ParabolicTetraType == FeElementTypeParabolicTetra.C3D10T &&
                parts[22].ParabolicWedgeType == FeElementTypeParabolicWedge.C3D15 &&
                parts[33].ParabolicHexaType == FeElementTypeParabolicHexa.C3D20R,
                "PMA v5 preserves quadratic solver formulations");
            check(reopened.Mesh.Parts.Values.All(part => part.Labels.Length == 1) && reopened.StepCollection.StepsList.Single().Nlgeom,
                "quadratic PMA round trip preserves part membership and nonlinear step");

            var savedArchive = AnalysisArchive.Open(path);
            check(savedArchive.ModelMesh is { Length: > 8 } && JsonNode.Parse(savedArchive.ModelMetadata)!["Version"]!.GetValue<int>() == 28,
                "editable archive v3 stores metadata v28 with a separate binary mesh payload");
            void RejectArchiveMesh(Func<byte[], byte[]?> mutate, string message)
            {
                AnalysisArchive.Save(path, savedArchive with { ModelMesh = mutate(savedArchive.ModelMesh!) });
                bool rejected = false;
                try { StaticProjectArchive.Open(path); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            RejectArchiveMesh(bytes => null, "editable metadata v28 requires its binary mesh payload");
            RejectArchiveMesh(bytes => bytes[..^1], "binary mesh rejects truncation");
            RejectArchiveMesh(bytes => [.. bytes, 0], "binary mesh rejects trailing data");
            RejectArchiveMesh(bytes => { var changed = bytes.ToArray(); changed[0] ^= 1; return changed; }, "binary mesh rejects invalid signature");
            AnalysisArchive.Save(path, savedArchive);
            var legacyEighteen = JsonNode.Parse(savedArchive.ModelMetadata)!.DeepClone();
            MetadataCompatibility.RemoveUserKeywords(legacyEighteen);
            legacyEighteen["Version"] = 18;
            check(StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacyEighteen.ToJsonString()), savedArchive.ModelMesh)
                    .Mesh.Elements.Count == original.Mesh.Elements.Count,
                "metadata v18 external-mesh projects remain readable");
            var metadata = JsonNode.Parse(StaticModelMetadata.Serialize(original))!;
            check(metadata["Version"]!.GetValue<int>() == 27 &&
                metadata["Elements"]!.AsArray().Select(element => element!["Kind"]!.GetValue<string>()).SequenceEqual(
                    new[] { nameof(ParabolicTetraElement), nameof(ParabolicWedgeElement), nameof(ParabolicHexaElement) }),
                "current inline metadata records explicit element topology");
            void Reject(Action<JsonNode> mutate, string message)
            {
                var invalid = metadata.DeepClone();
                mutate(invalid);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(json => json["Elements"]![0]!["Kind"] = "UnknownElement", "PMA v5 rejects unknown topology");
            Reject(json => json["Elements"]![0]!["Nodes"]!.AsArray().RemoveAt(9), "PMA v5 rejects topology width mismatch");
            Reject(json => json["Parts"]![0]!["ParabolicTetraType"] = 0, "PMA v5 rejects missing quadratic formulation");
            Reject(json => { MetadataCompatibility.UseLegacyMaterials(json); json["Version"] = 4; },
                "PMA v4 rejects v5 topology fields");

            var legacyFive = metadata.DeepClone();
            MetadataCompatibility.UseLegacyMaterials(legacyFive);
            legacyFive["Version"] = 5;
            check(StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacyFive.ToJsonString())).Mesh.Elements[303]
                    is ParabolicHexaElement,
                "PMA v5 quadratic projects remain readable");

            var legacy = JsonNode.Parse(StaticModelMetadata.Serialize(new StaticModelWorkflow(1).Model))!;
            MetadataCompatibility.UseLegacyMaterials(legacy);
            legacy["Version"] = 4;
            foreach (var element in legacy["Elements"]!.AsArray()) element!.AsObject().Remove("Kind");
            foreach (var part in legacy["Parts"]!.AsArray())
            {
                part!.AsObject().Remove("ParabolicTetraType");
                part.AsObject().Remove("ParabolicWedgeType");
                part.AsObject().Remove("ParabolicHexaType");
            }
            check(StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacy.ToJsonString())).Mesh.Elements.Values.Single() is LinearHexaElement,
                "PMA v4 linear projects remain readable");
        }
        finally { File.Delete(path); }
    }
}
