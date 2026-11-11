using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

/// <summary>Object identities and hierarchy shared by native shells; no GTK or renderer state.</summary>
public sealed record ModelTreeNode(string Key, string Kind, string Name, string? Step,
    string DefinitionType, bool? Active, string Details, string? Editor, string? Creator,
    string? RegionKind, string? Region, ModelTreeNode[] Children);

public sealed record ModelTreeSelection(int[] Nodes, ProjectElementFace[] Faces);

public static class ProjectModelTree
{
    /// <summary>Renderer highlight membership, independent of the bounded marker-preview scene.</summary>
    public static int[] NodeSetHighlight(FeMesh mesh, string key)
    {
        var set = mesh.NodeSets.Values.SingleOrDefault(set => !set.Internal &&
            key == "model/mesh/node-sets/" + Uri.EscapeDataString(set.Name))
            ?? throw new ArgumentException("Select an existing node set in the model tree.");
        if (set.Labels.Length > ProjectMeshNodeSelection.MaximumNodes)
            throw new NotSupportedException("Interactive highlighting supports at most 100000 nodes.");
        return set.Labels.Distinct().Order().ToArray();
    }

    public static ModelTreeNode[] Read(string path) => Build(StaticProjectArchive.Open(path).Model);

    private static ModelTreeNode[] Build(FeModel model)
    {
        ModelTreeNode Group(string key, string name, IEnumerable<ModelTreeNode> children, string? creator = null, string? step = null)
        {
            var items = children.ToArray();
            return new(key, "collection", name, step, "", null, $"{items.Length} items", null, creator, null, null, items);
        }
        ModelTreeNode Item(string parent, string kind, NamedClass item, string details = "", string? editor = null,
            string? regionKind = null, string? region = null, string? step = null) =>
            new(parent + "/" + Uri.EscapeDataString(item.Name), kind, item.Name, step, item.GetType().Name,
                item.Active, details, editor, null, regionKind, region, []);

        var mesh = model.Mesh;
        var meshTree = Group("model/mesh", "Mesh", [
            Group("model/mesh/parts", "Parts", mesh.Parts.Values.Select(part =>
                Item("model/mesh/parts", "part", part, $"{part.Labels.Length} elements", regionKind: "part", region: part.Name))),
            Group("model/mesh/node-sets", "Node Sets", mesh.NodeSets.Values.Where(set => !set.Internal).Select(set =>
                Item("model/mesh/node-sets", "node-set", set, $"{set.Labels.Length} nodes", editor: "node-set", regionKind: "node-set", region: set.Name)), "node-set"),
            Group("model/mesh/element-sets", "Element Sets", mesh.ElementSets.Values.Where(set => !set.Internal).Select(set =>
                Item("model/mesh/element-sets", "element-set", set,
                    $"{ElementIds(mesh, set).Length} elements" + (set.CreatedFromParts ? " (from parts)" : ""),
                    editor: "element-set", regionKind: "element-set", region: set.Name)), "element-set"),
            Group("model/mesh/surfaces", "Surfaces", mesh.Surfaces.Values.Where(surface => !surface.Internal).Select(surface =>
                Item("model/mesh/surfaces", "surface", surface, surface.Type.ToString(), editor: "surface",
                    regionKind: "surface", region: surface.Name)), "surface")
        ]);
        string? RegionKind(RegionTypeEnum type) => type switch
        {
            RegionTypeEnum.PartName => "part", RegionTypeEnum.NodeSetName => "node-set",
            RegionTypeEnum.ElementSetName => "element-set", RegionTypeEnum.SurfaceName => "surface", _ => null
        };
        var steps = model.StepCollection.StepsList.Select(step =>
        {
            string key = "model/steps/" + Uri.EscapeDataString(step.Name);
            return new ModelTreeNode(key, "step", step.Name, step.Name, step.GetType().Name, step.Active,
                step.RunAnalysis ? "Analysis step" : "Initial step", step is StaticStep ? "step" : null, null, null, null, [
                Group(key + "/history-outputs", "History Outputs", step.HistoryOutputs.Values.Select(output =>
                    Item(key + "/history-outputs", "history-output", output, editor: "history-output", step: step.Name)), "history-output", step.Name),
                Group(key + "/field-outputs", "Field Outputs", step.FieldOutputs.Values.Select(output =>
                    Item(key + "/field-outputs", "field-output", output, editor: "field-output", step: step.Name)), "field-output", step.Name),
                Group(key + "/bcs", "BCs", step.BoundaryConditions.Values.Select(boundary =>
                    Item(key + "/bcs", "boundary", boundary, $"{boundary.RegionType}: {boundary.RegionName}",
                        boundary is DisplacementRotation rotation && rotation.GetFixedDirections().Length == 0 ? "boundary" : null,
                        RegionKind(boundary.RegionType), boundary.RegionName, step.Name)), "boundary", step.Name),
                Group(key + "/loads", "Loads", step.Loads.Values.Select(load =>
                    Item(key + "/loads", "load", load, $"{load.RegionType}: {load.RegionName}", load switch
                    {
                        CLoad or STLoad => "force", GravityLoad => "gravity", CentrifLoad => "centrifugal",
                        DLoad pressure when pressure.GetType() == typeof(DLoad) => "pressure", _ => null
                    }, RegionKind(load.RegionType), load.RegionName, step.Name)), "force", step.Name),
                Group(key + "/defined-fields", "Defined Fields", step.DefinedFields.Values.Select(field =>
                    Item(key + "/defined-fields", "defined-field", field, step: step.Name)))
            ]);
        });
        return [
            new("model", "model", "Model", null, nameof(FeModel), null, model.Name, null, null, null, null, [
                meshTree,
                Group("model/features", "Features", [
                    Group("model/features/reference-points", "Reference Points", mesh.ReferencePoints.Values.Select(point =>
                        Item("model/features/reference-points", "reference-point", point))),
                    Group("model/features/cylinders", "Cylinders", model.Cylinders.Values.Select(cylinder =>
                        Item("model/features/cylinders", "cylinder", cylinder))),
                    Group("model/features/coordinate-systems", "Coordinate Systems", mesh.CoordinateSystems.Values.Select(system =>
                        Item("model/features/coordinate-systems", "coordinate-system", system)))
                ]),
                Group("model/materials", "Materials", model.Materials.Values.Select(material =>
                    Item("model/materials", "material", material, string.Join(", ", material.Properties.Select(p => p.GetType().Name)),
                        !material.TemperatureDependent && material.Properties.OfType<Elastic>().Count() == 1 &&
                        material.Properties.OfType<Elastic>().Single().YoungsPoissonsTemp.Length == 1 &&
                        material.Properties.OfType<Density>().Count() <= 1 &&
                        material.Properties.OfType<Density>().SingleOrDefault()?.DensityTemp.Length is null or 1 ? "material" : null)), "material"),
                Group("model/sections", "Sections", model.Sections.Values.Select(section =>
                    Item("model/sections", "section", section, $"{section.MaterialName} → {section.RegionType}: {section.RegionName}",
                        section is SolidSection ? "section" : null, RegionKind(section.RegionType), section.RegionName)), "section"),
                Group("model/constraints", "Constraints", model.Constraints.Values.Select(constraint =>
                    Item("model/constraints", "constraint", constraint, constraint switch
                    {
                        RigidBody body => $"{body.ReferencePointName} → {body.RegionType}: {body.RegionName}",
                        SurfaceSpring spring => $"{spring.RegionType}: {spring.RegionName}",
                        _ => $"{constraint.MasterRegionName} → {constraint.SlaveRegionName}"
                    }))),
                Group("model/contacts", "Contacts", [
                    Group("model/contacts/interactions", "Surface Interactions", model.SurfaceInteractions.Values.Select(interaction =>
                        Item("model/contacts/interactions", "interaction", interaction))),
                    Group("model/contacts/pairs", "Contact Pairs", model.ContactPairs.Values.Select(pair =>
                        Item("model/contacts/pairs", "contact-pair", pair,
                            $"{pair.MasterRegionName} → {pair.SlaveRegionName}")))
                ]),
                Group("model/distributions", "Distributions", model.Distributions.Values.Select(distribution =>
                    Item("model/distributions", "distribution", distribution))),
                Group("model/amplitudes", "Amplitudes", model.Amplitudes.Values.Select(amplitude =>
                    Item("model/amplitudes", "amplitude", amplitude))),
                Group("model/initial-conditions", "Initial Conditions", model.InitialConditions.Values.Select(condition =>
                    Item("model/initial-conditions", "initial-condition", condition))),
                Group("model/steps", "Steps", steps, "step")
            ]),
            Group("analyses", "Analyses", [])
        ];
    }

    /// <summary>Resolve identities against the current model, never trust client-provided membership.</summary>
    public static ModelTreeSelection Select(string path, string key)
    {
        var meshModel = StaticProjectArchive.Open(path).Model;
        static IEnumerable<ModelTreeNode> Walk(IEnumerable<ModelTreeNode> nodes) =>
            nodes.SelectMany(node => new[] { node }.Concat(Walk(node.Children)));
        var node = Walk(Build(meshModel)).SingleOrDefault(node => node.Key == key)
            ?? throw new ArgumentException("The selected model object no longer exists.");
        var mesh = meshModel.Mesh;
        var faces = new List<ProjectElementFace>();
        int[] nodes;
        if (node.RegionKind == "node-set") nodes = mesh.NodeSets[node.Region!].Labels;
        else if (node.RegionKind == "surface")
        {
            var surface = mesh.Surfaces[node.Region!];
            if (surface.Type == FeSurfaceType.Element && surface.ElementFaces != null)
                foreach (var face in surface.ElementFaces)
                    foreach (int id in ElementIds(mesh, mesh.ElementSets[face.Value]))
                    {
                        faces.Add(new(id, face.Key.ToString()));
                        if (faces.Count > ProjectSurfaces.MaximumFaces)
                            throw new NotSupportedException("Surface preview supports at most 100 faces. The complete surface remains in the model.");
                    }
            nodes = faces.Count > 0 ? faces.SelectMany(face =>
                mesh.Elements[face.ElementId].GetNodeIdsFromFaceName(Enum.Parse<FeFaceName>(face.Face))).Distinct().ToArray()
                : mesh.NodeSets[surface.NodeSetName].Labels;
        }
        else if (node.RegionKind is "part" or "element-set")
        {
            int[] ids = node.RegionKind == "part" ? mesh.Parts[node.Region!].Labels : ElementIds(mesh, mesh.ElementSets[node.Region!]);
            nodes = ids.SelectMany(id => mesh.Elements[id].NodeIds).Distinct().Take(10001).ToArray();
        }
        else throw new NotSupportedException("This object has no supported mesh region to preview.");
        if (nodes.Length > 10000) throw new NotSupportedException("Region preview supports at most 10000 nodes. The complete region remains in the model.");
        return new(nodes.Distinct().Order().ToArray(), faces.Distinct().ToArray());
    }

    private static int[] ElementIds(FeMesh mesh, FeElementSet set) => set.CreatedFromParts
        ? mesh.Parts.Values.Where(part => set.Labels.Contains(part.PartId)).SelectMany(part => part.Labels).Distinct().ToArray()
        : set.Labels;
}
