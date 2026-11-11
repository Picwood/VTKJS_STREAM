using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectModelTreeChecks
{
    private static IEnumerable<ModelTreeNode> Walk(IEnumerable<ModelTreeNode> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Walk(node.Children)));

    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "model-tree-" + Guid.NewGuid() + ".pma");
        try
        {
            var model = new StaticModelWorkflow(2).Model;
            var fixedSurface = new FeSurface("FIXED", "LEFT");
            fixedSurface.AddElementFace(FeFaceName.S6, "SOLID");
            model.Mesh.Surfaces.Add(fixedSurface.Name, fixedSurface);
            model.Constraints.Add("bond", new Tie("bond", .05, false, "LOADED", RegionTypeEnum.SurfaceName,
                "FIXED", RegionTypeEnum.SurfaceName, false) { Active = false });
            var interaction = new SurfaceInteraction("law");
            interaction.AddProperty(new Friction(.2, 5000));
            model.SurfaceInteractions.Add(interaction.Name, interaction);
            model.ContactPairs.Add("pair", new ContactPair("pair", interaction.Name, ContactPairMethod.NodeToSurface,
                true, true, .02, "LOADED", RegionTypeEnum.SurfaceName, "FIXED", RegionTypeEnum.SurfaceName));
            StaticProjectArchive.Save(path, model);
            byte[] before = File.ReadAllBytes(path);
            var tree = ProjectModelTree.Read(path);
            var all = Walk(tree).ToArray();
            ModelTreeNode Node(string key) => all.Single(node => node.Key == key);
            check(tree.Select(node => node.Name).SequenceEqual(new[] { "Model", "Analyses" }), "tree retains original model and analyses roots");
            check(Node("model").Children.Select(node => node.Name).SequenceEqual(new[] {
                "Mesh", "Features", "Materials", "Sections", "Constraints", "Contacts", "Distributions", "Amplitudes", "Initial Conditions", "Steps" }),
                "tree preserves the project owner's reference screenshot branch order");
            check(Node("model/features").Children.Select(node => node.Name).SequenceEqual(new[] {
                "Reference Points", "Cylinders", "Coordinate Systems" }), "tree preserves the reference screenshot feature hierarchy");
            check(Node("model/mesh").Children.Select(node => node.Name).SequenceEqual(
                new[] { "Parts", "Node Sets", "Element Sets", "Surfaces" }), "tree retains original mesh region hierarchy");
            check(Node("model/contacts").Children.Select(node => node.Name).SequenceEqual(
                new[] { "Surface Interactions", "Contact Pairs" }), "tree retains original contacts hierarchy");
            check(all.Select(node => node.Key).Distinct().Count() == all.Length, "tree identities are unique across identical step definition names");
            foreach (var step in model.StepCollection.StepsList)
            {
                var stepNode = all.Single(node => node.Kind == "step" && node.Name == step.Name);
                check(stepNode.Children.Select(node => node.Name).SequenceEqual(
                    new[] { "History Outputs", "Field Outputs", "BCs", "Loads", "Defined Fields" }),
                    "tree preserves original step child ordering");
                check(Walk(stepNode.Children).Where(node => node.Kind != "collection").All(node => node.Step == step.Name),
                    "tree scopes definition identities to the owning step");
            }
            var tie = Node("model/constraints/bond");
            check(tie.Active == false && tie.Editor == null && tie.Details.Contains("LOADED") && tie.Details.Contains("FIXED"),
                "tree exposes prepared inactive constraints and regions without claiming editor support");
            check(Node("model/contacts/pairs/pair").Editor == null && Node("model/contacts/interactions/law").Name == "law",
                "tree exposes prepared contact pairs and interactions");
            check(Node("model/mesh/node-sets").Creator == "node-set" && Node("model/mesh/element-sets").Creator == "element-set" &&
                Node("model/mesh/element-sets/SOLID").Editor == "element-set", "tree exposes node-set and element-set creation and the general element-set editor");
            check(Node("model/sections/solid").Editor == "section" && Node("model/materials/ELASTIC").Editor == "material",
                "tree advertises existing solid section and elastic material editors");
            var selection = ProjectModelTree.Select(path, "model/mesh/node-sets/LEFT");
            check(selection.Nodes.SequenceEqual(model.Mesh.NodeSets["LEFT"].Labels.Order()) && selection.Faces.Length == 0,
                "tree node-set selection resolves exact stored membership");
            check(ProjectModelTree.NodeSetHighlight(model.Mesh, "model/mesh/node-sets/LEFT").SequenceEqual(selection.Nodes),
                "tree highlights exact node-set membership without publishing a new scene");
            selection = ProjectModelTree.Select(path, "model/mesh/element-sets/SOLID");
            check(selection.Nodes.SequenceEqual(Enumerable.Range(1, 8)), "tree element-set selection resolves mesh nodes");
            selection = ProjectModelTree.Select(path, "model/mesh/surfaces/FIXED");
            check(selection.Faces.SequenceEqual(new[] { new ProjectElementFace(1, "S6") }) && selection.Nodes.SequenceEqual(
                model.Mesh.Elements[1].GetNodeIdsFromFaceName(FeFaceName.S6).Order()), "tree surface selection retains exact element-face identity");
            foreach (string key in new[] { "model/mesh/node-sets/missing", "model/mesh/node-sets/../LEFT", "model/steps/missing" })
            {
                bool refused = false;
                try { ProjectModelTree.Select(path, key); } catch (ArgumentException) { refused = true; }
                check(refused, "tree refuses unknown or stale object identity");
            }
            bool unsupported = false;
            try { ProjectModelTree.Select(path, tie.Key); } catch (NotSupportedException) { unsupported = true; }
            check(unsupported, "tree refuses unsupported region preview without inventing membership");
            check(File.ReadAllBytes(path).SequenceEqual(before), "tree inventory and preview preserve project bytes");

            model = MultiPartProjectChecks.Create();
            StaticProjectArchive.Save(path, model);
            selection = ProjectModelTree.Select(path, "model/mesh/element-sets/ALL_PARTS");
            check(selection.Nodes.SequenceEqual(Enumerable.Range(1, 16)), "tree expands part-derived sets using sparse part IDs");

            model = new StaticModelWorkflow(1).Model;
            for (int id = 9; id <= 10001; id++) model.Mesh.Nodes.Add(id, new FeNode(id, id, 0, 0));
            model.Mesh.NodeSets.Add("large", new FeNodeSet("large", model.Mesh.Nodes.Keys.ToArray()));
            StaticProjectArchive.Save(path, model);
            unsupported = false;
            try { ProjectModelTree.Select(path, "model/mesh/node-sets/large"); } catch (NotSupportedException) { unsupported = true; }
            check(unsupported && ProjectModelTree.Read(path).Length == 2, "tree preview limit refuses truncation while inventory remains available");
            check(ProjectModelTree.NodeSetHighlight(model.Mesh, "model/mesh/node-sets/large").Length == 10001,
                "in-place tree highlighting is independent of the 10000-node marker preview limit");
            bool invalidHighlight = false;
            try { ProjectModelTree.NodeSetHighlight(model.Mesh, "model/mesh/node-sets/../LEFT"); } catch (ArgumentException) { invalidHighlight = true; }
            check(invalidHighlight, "tree highlighting rejects stale or noncanonical node-set identities");
        }
        finally { File.Delete(path); }
    }
}
