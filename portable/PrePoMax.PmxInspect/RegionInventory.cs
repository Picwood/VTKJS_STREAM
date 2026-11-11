using System.Formats.Nrbf;

internal static class RegionInventory
{
    internal static object Inspect(ClassRecord model)
    {
        if (!model.HasMember("_mesh") || model.GetClassRecord("_mesh") is not ClassRecord mesh)
            return new { status = "no-mesh-metadata" };
        var diagnostics = new List<string>();
        Dictionary<string, ClassRecord> Collection(ClassRecord owner, string field) =>
            ActiveModelInventory.DictionaryPairs(owner.GetClassRecord(field)).ToDictionary(
                p => p.GetString("key") ?? throw new InvalidDataException("Null region key."),
                p => p.GetClassRecord("value") ?? throw new InvalidDataException("Null region definition."), StringComparer.Ordinal);
        var parts = Collection(mesh, "_parts");
        var nodes = Collection(mesh, "_nodeSets");
        var elements = Collection(mesh, "_elementSets");
        var surfaces = Collection(mesh, "_surfaces");
        var points = Collection(mesh, "_referencePoints");
        int links = 0;
        void Link(string? name, Dictionary<string, ClassRecord> target, string context)
        {
            links++;
            if (name == null || !target.ContainsKey(name)) diagnostics.Add(context + ": missing target");
        }
        int EnumValue(ClassRecord owner, string field) => owner.GetClassRecord(field)?.GetInt32("value__")
            ?? throw new InvalidDataException("Missing enum value.");
        foreach (var surface in surfaces.Values)
        {
            string? nodeSet = surface.GetString("_nodeSetName");
            if (!string.IsNullOrEmpty(nodeSet)) Link(nodeSet, nodes, "Surface node set");
            foreach (var pair in ActiveModelInventory.DictionaryPairs(surface.GetClassRecord("_elementFaces")))
                Link(pair.GetString("value"), elements, "Surface element set");
        }
        void Region(ClassRecord definition, string typeField = "_regionType", string nameField = "_regionName")
        {
            int region = EnumValue(definition, typeField);
            var target = region switch { 0 => parts, 2 => nodes, 4 => elements, 5 => surfaces, 6 => points, _ => null };
            if (target == null) diagnostics.Add(definition.TypeName.FullName + ": unchecked region kind " + region);
            else Link(definition.GetString(nameField), target, definition.TypeName.FullName);
        }
        var materials = Collection(model, "_materials");
        foreach (var section in Collection(model, "_sections").Values)
        {
            Region(section);
            Link(section.GetString("_materialName"), materials, "Section material");
        }
        foreach (var step in ActiveModelInventory.ListItems(model.GetClassRecord("_stepCollection")?.GetClassRecord("_steps")))
            foreach (string field in new[] { "_loads", "_boundaryConditions" })
                foreach (var definition in Collection(step, field).Values) Region(definition);
        var interactions = Collection(model, "_surfaceInteractions");
        var contacts = Collection(model, "_contactPairs");
        foreach (var contact in contacts.Values)
        {
            Region(contact, "_masterRegionType", "_masterSurfaceName");
            Region(contact, "_slaveRegionType", "_slaveSurfaceName");
            Link(contact.GetString("_surfaceInteractionName"), interactions, "Contact interaction");
        }
        var constraints = Collection(model, "_constraints");
        foreach (var constraint in constraints.Values)
        {
            switch (constraint.TypeName.FullName)
            {
                case "CaeModel.Tie":
                case "CaeModel.RigidBody":
                    Region(constraint, "_masterRegionType", "_masterRegionName");
                    Region(constraint, "_slaveRegionType", "_slaveRegionName");
                    break;
                case "CaeModel.SurfaceSpring":
                    Region(constraint, "_masterRegionType", "_masterRegionName");
                    break;
                default:
                    diagnostics.Add(constraint.TypeName.FullName + ": unchecked constraint type");
                    break;
            }
        }
        var formulations = parts.Values.Select(part => new {
            partId = part.GetInt32("_partId"),
            quadraticTetra = Map("_parabolicTetraType", EnumValue(part, "_parabolicTetraType")),
            quadraticHexa = Map("_parabolicHexaType", EnumValue(part, "_parabolicHexaType")),
            quadraticWedge = Map("_parabolicWedgeType", EnumValue(part, "_parabolicWedgeType"))
        }).OrderBy(p => p.partId).ToArray();
        return new { status = "metadata-links-inspected", surfaces = surfaces.Count, contacts = contacts.Count, constraints = constraints.Count, linksChecked = links, diagnostics, formulations,
            scope = "Name existence only, including inactive definitions. Quadratic solid settings only; not proof of topology/formulation consistency. Recognized constraint/contact names checked; face numbers, endpoint-kind applicability, region contents and solver eligibility remain unchecked." };
    }
    // CaeMesh/Elements/FeElementType.cs: retain unknown values explicitly.
    static string Map(string field, int value) => (field, value) switch {
        (_, 0) => "None", ("_parabolicTetraType", 10) => "C3D10", ("_parabolicTetraType", 11) => "C3D10T",
        ("_parabolicHexaType", 10) => "C3D20", ("_parabolicHexaType", 11) => "C3D20R",
        ("_parabolicWedgeType", 10) => "C3D15", _ => "Unknown:" + value
    };
}
