using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;
using System.Text;
using System.Text.Json.Nodes;

internal static class MultiPartProjectChecks
{
    public static FeModel Create()
    {
        var workflow = new StaticModelWorkflow(1);
        var model = workflow.Model;
        var mesh = model.Mesh;
        foreach (var node in mesh.Nodes.Values.ToArray())
            mesh.Nodes.Add(node.Id + 8, new FeNode(node.Id + 8, node.X + 3, node.Y, node.Z));
        int partId = 17;
        mesh.Elements.Add(2, new LinearHexaElement(2, Enumerable.Range(9, 8).ToArray()) { PartId = partId });
        mesh.Parts.Add("second_block", new MeshPart("second_block", partId, Enumerable.Range(9, 8).ToArray(), new[] { 2 }, new[] { typeof(LinearHexaElement) }));
        mesh.NodeSets["ALLNODES"].Labels = Enumerable.Range(1, 16).ToArray();
        mesh.NodeSets.Add("LEFT2", new FeNodeSet("LEFT2", new[] { 9, 12, 13, 16 }));
        mesh.NodeSets.Add("RIGHT2", new FeNodeSet("RIGHT2", new[] { 10, 11, 14, 15 }));
        mesh.ElementSets.Add("SOLID2", new FeElementSet("SOLID2", new[] { 2 }));
        mesh.ElementSets.Add("ALL_PARTS", new FeElementSet("ALL_PARTS", new[] { 1, partId }, createdFromParts: true));
        var material = new Material("stiffer");
        material.AddProperty(new Elastic(new[] { new[] { 2000d, 0d, 0d } }));
        model.Materials.Add(material.Name, material);
        model.Sections.Add("section2", new SolidSection("section2", material.Name, "second_block", RegionTypeEnum.PartName, 1, false));
        var step = model.StepCollection.StepsList[0];
        step.AddBoundaryCondition(new DisplacementRotation("left2", "LEFT2", RegionTypeEnum.NodeSetName, false, false, 0)
            { U1 = new EquationContainer(typeof(StringLengthDOFConverter), 0) });
        step.AddLoad(new CLoad("force2", "RIGHT2", RegionTypeEnum.NodeSetName, .25, 0, 0, false, false, 0));
        step.AddHistoryOutput(new NodalHistoryOutput("right2-history", NodalHistoryVariable.U, "RIGHT2", RegionTypeEnum.NodeSetName));
        return model;
    }

    public static void Run(Action<bool, string> check)
    {
        var original = Create();
        ((MeshPart)original.Mesh.Parts["second_block"]).LinearHexaType = FeElementTypeLinearHexa.C3D8R;
        string path = Path.Combine(Path.GetTempPath(), "multi part " + Guid.NewGuid() + ".pma");
        try
        {
            StaticProjectArchive.Save(path, original);
            var reopened = StaticProjectArchive.Open(path).Model;
            check(reopened.Mesh.Parts.Keys.SequenceEqual(original.Mesh.Parts.Keys) && reopened.Mesh.Parts["second_block"].PartId == 17,
                "multipart persistence preserves names ordering and sparse part IDs");
            check(reopened.Mesh.Elements[2].PartId == 17 && reopened.Mesh.Parts["second_block"].Labels.SequenceEqual(new[] { 2 }),
                "multipart persistence preserves element membership");
            check(((MeshPart)reopened.Mesh.Parts["second_block"]).LinearHexaType == FeElementTypeLinearHexa.C3D8R,
                "multipart persistence preserves part element formulation");
            check(reopened.Mesh.ElementSets["ALL_PARTS"].CreatedFromParts &&
                reopened.Mesh.ElementSets["ALL_PARTS"].Labels.SequenceEqual(new[] { 1, 17 }),
                "multipart persistence preserves part-derived element sets");
            reopened.GetMaterialAssignments(out var materials);
            check(materials[1] == 0 && materials[2] == 1, "multipart persistence preserves distinct material assignments");
            check(reopened.Sections["section2"].RegionType == RegionTypeEnum.PartName && reopened.Sections["section2"].RegionName == "second_block",
                "multipart persistence preserves part-based section region");
            var moved = reopened.Mesh.Nodes[9];
            moved.X = 10;
            reopened.Mesh.Nodes[9] = moved;
            check(original.Mesh.Nodes[9].X == 3, "multipart reopen owns node coordinates");
            byte[] inlineMetadata = StaticModelMetadata.Serialize(original);
            void Reject(Action<JsonNode> mutate, string name)
            {
                var json = JsonNode.Parse(inlineMetadata)!;
                mutate(json);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(json.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, name);
            }
            Reject(j => j["Parts"]![1]!["Elements"]![0] = 1, "multipart metadata rejects overlapping membership");
            Reject(j => j["Parts"]![1]!["Id"] = j["Parts"]![0]!["Id"]!.DeepClone(), "multipart metadata rejects duplicate IDs");
            Reject(j => j["Parts"]!.AsArray().RemoveAt(1), "multipart metadata rejects unassigned elements");
            Reject(j => j["Parts"]![1]!["Elements"]![0] = 999, "multipart metadata rejects unknown elements");
            JsonNode PartSet(JsonNode json) => json["ElementSets"]!.AsArray()
                .Single(set => set!["Name"]!.GetValue<string>() == "ALL_PARTS")!;
            Reject(j => PartSet(j)["Labels"]![0] = 999, "part-derived element sets reject unknown part IDs");
            Reject(j => PartSet(j)["Labels"]![1] = 1, "part-derived element sets reject duplicate part IDs");
            Reject(j => { MetadataCompatibility.RemoveUserKeywords(j); j["Version"] = 9; }, "metadata v9 rejects part-derived element sets");
        }
        finally { File.Delete(path); }
    }
}
