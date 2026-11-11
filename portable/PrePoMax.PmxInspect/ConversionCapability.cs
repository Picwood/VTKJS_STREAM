using System.Formats.Nrbf;

internal sealed record ConversionFinding(string code, int count, string detail);

internal static class ConversionCapability
{
    static readonly HashSet<string> SupportedLoads = ["CaeModel.STLoad", "CaeModel.CLoad", "CaeModel.GravityLoad", "CaeModel.CentrifLoad"];
    static readonly HashSet<string> SupportedBoundaries = ["CaeModel.DisplacementRotation", "CaeModel.FixedBC"];
    static readonly HashSet<string> SupportedFields = ["CaeModel.NodalFieldOutput", "CaeModel.ElementFieldOutput", "CaeModel.ContactFieldOutput"];
    static readonly HashSet<string> SupportedHistories = ["CaeModel.NodalHistoryOutput"];

    internal static object Inspect(ClassRecord? model, IEnumerable<ConversionFinding> meshBlockers, bool meshInspected)
    {
        var blockers = meshBlockers.ToList();
        if (model?.TypeName.FullName != "CaeModel.FeModel")
        {
            blockers.Add(new("model.missing", 1, "Current FeModel metadata was not found."));
            return new { status = "unavailable", complete = false, blockerCount = blockers.Count, blockers = blockers.ToArray() };
        }

        ClassRecord? Member(ClassRecord owner, string field) => owner.HasMember(field) ? owner.GetClassRecord(field) : null;
        int Count(ClassRecord owner, string field) => ActiveModelInventory.DictionaryItems(Member(owner, field)).Count();
        void BlockCollection(ClassRecord owner, string field, string code, string detail)
        {
            int count = Count(owner, field);
            if (count != 0) blockers.Add(new(code, count, detail));
        }

        var geometry = Member(model, "_geometry");
        if (geometry != null) BlockCollection(geometry, "_parts", "model.geometry", "Current PMA metadata does not persist geometry parts.");
        var mesh = Member(model, "_mesh");
        if (mesh == null) blockers.Add(new("mesh.missing", 1, "Current analysis mesh metadata was not found."));
        else
        {
            BlockCollection(mesh, "_meshSetupItems", "mesh.setup", "Current PMA metadata does not persist mesh setup items.");
            var unsupportedPoints = ActiveModelInventory.DictionaryItems(Member(mesh, "_referencePoints"))
                .Where(point => point.TypeName.FullName != "CaeMesh.FeReferencePoint").ToArray();
            if (unsupportedPoints.Length != 0) blockers.Add(new("mesh.reference-points", unsupportedPoints.Length,
                "Current PMA metadata supports only FeReferencePoint definitions."));
            var unsupportedCoordinateSystems = ActiveModelInventory.DictionaryItems(Member(mesh, "_coordinateSystems"))
                .Where(record => !LegacyPmxConverter.CanConvertCoordinateSystem(record)).ToArray();
            if (unsupportedCoordinateSystems.Length != 0) blockers.Add(new("mesh.coordinate-systems", unsupportedCoordinateSystems.Length,
                "Current PMA metadata supports bounded coordinate-defined 3D rectangular or cylindrical coordinate systems."));
        }

        var unsupportedConstraints = ActiveModelInventory.DictionaryItems(Member(model, "_constraints"))
            .Where(constraint => constraint.TypeName.FullName is not
                ("CaeModel.Tie" or "CaeModel.RigidBody" or "CaeModel.SurfaceSpring")).ToArray();
        if (unsupportedConstraints.Length != 0) blockers.Add(new("model.constraints", unsupportedConstraints.Length,
            "Current PMA metadata supports tie, rigid-body and surface-spring constraints."));
        var unsupportedContacts = ActiveModelInventory.DictionaryItems(Member(model, "_contactPairs"))
            .Where(contact => contact.TypeName.FullName != "CaeModel.ContactPair").ToArray();
        if (unsupportedContacts.Length != 0) blockers.Add(new("model.contact-pairs", unsupportedContacts.Length,
            "Current PMA metadata supports only ContactPair definitions."));
        var unsupportedInteractions = ActiveModelInventory.DictionaryItems(Member(model, "_surfaceInteractions"))
            .Where(interaction => interaction.TypeName.FullName != "CaeModel.SurfaceInteraction" ||
                ActiveModelInventory.ListItems(Member(interaction, "_properties"))
                    .Select(property => property.TypeName.FullName).GroupBy(type => type)
                    .Any(group => group.Count() != 1 || group.Key is not ("CaeModel.SurfaceBehavior" or "CaeModel.Friction" or "CaeModel.GapConductance")))
            .ToArray();
        if (unsupportedInteractions.Length != 0) blockers.Add(new("model.surface-interactions", unsupportedInteractions.Length,
            "Current PMA metadata supports unique surface behavior, friction and gap-conductance properties."));
        BlockCollection(model, "_distributions", "model.distributions", "Current PMA metadata does not persist distributions.");
        var unsupportedAmplitudes = ActiveModelInventory.DictionaryItems(Member(model, "_amplitudes"))
            .Where(amplitude => amplitude.TypeName.FullName != "CaeModel.AmplitudeTabular").ToArray();
        if (unsupportedAmplitudes.Length != 0) blockers.Add(new("model.amplitudes", unsupportedAmplitudes.Length,
            "Current PMA metadata supports only tabular amplitudes."));
        BlockCollection(model, "_initialConditions", "model.initial-conditions", "Current PMA metadata does not persist initial conditions.");
        var unsupportedCylinders = ActiveModelInventory.DictionaryItems(Member(model, "_cylinders"))
            .Where(cylinder => cylinder.TypeName.FullName != "CaeModel.FeCylinder").ToArray();
        if (unsupportedCylinders.Length != 0) blockers.Add(new("model.cylinders", unsupportedCylinders.Length,
            "Current PMA metadata supports only FeCylinder definitions."));
        BlockCollection(model, "_userViews", "model.user-views", "Current PMA metadata does not persist user views.");
        var unsupportedKeywords = UserKeywordInventory.Read(model).Where(entry =>
        {
            var keyword = entry.Keyword;
            string data = keyword.GetString("_data") ?? "";
            return keyword.HasMember("_active") && !keyword.GetBoolean("_active") ||
                keyword.HasMember("_suppressed") && keyword.GetBoolean("_suppressed") ||
                ActiveModelInventory.ListItems(keyword.HasMember("_keywords") ? keyword.GetClassRecord("_keywords") : null).Any() ||
                string.IsNullOrEmpty(data) || data.Contains('\0') || System.Text.Encoding.UTF8.GetByteCount(data) > 1024 * 1024;
        }).ToArray();
        if (unsupportedKeywords.Length != 0) blockers.Add(new("model.user-keywords", unsupportedKeywords.Length,
            "Only bounded active, unsuppressed leaf CalculiX user keywords are supported."));
        var parameters = Member(model, "_parameters");
        if (parameters != null) BlockCollection(parameters, "_parameters", "model.parameters", "Current PMA metadata requires numeric values without model parameters.");

        var properties = Member(model, "_properties");
        int modelSpace = properties?.GetClassRecord("ModelSpace")?.GetInt32("value__") ?? -1;
        if (modelSpace != 1) blockers.Add(new("model.space", 1, "Current PMA metadata supports only three-dimensional models."));

        foreach (var material in ActiveModelInventory.DictionaryItems(model.GetClassRecord("_materials")))
        {
            var propertyTypes = ActiveModelInventory.ListItems(Member(material, "_properties"))
                .Select(property => property.TypeName.FullName).ToArray();
            bool supported = propertyTypes.Count(type => type == "CaeModel.Elastic") == 1 &&
                propertyTypes.GroupBy(type => type).All(group => group.Count() == 1) &&
                propertyTypes.All(type => type is "CaeModel.Elastic" or "CaeModel.Density" or "CaeModel.Plastic" or
                    "CaeModel.ThermalExpansion" or "CaeModel.ThermalConductivity" or "CaeModel.SpecificHeat");
            if (!supported) blockers.Add(new("material.properties", 1,
                "Material contains duplicate or unsupported PMA material properties: " + string.Join(", ", propertyTypes) + "."));
        }

        var steps = ActiveModelInventory.ListItems(model.GetClassRecord("_stepCollection")?.GetClassRecord("_steps")).ToArray();
        foreach (var step in steps)
        {
            if (step.TypeName.FullName != "CaeModel.StaticStep")
                blockers.Add(new("step.type", 1, "Current PMA metadata supports only StaticStep; found " + step.TypeName.FullName + "."));
            CheckDefinitions(step, "_loads", SupportedLoads, "step.loads", blockers);
            CheckDefinitions(step, "_boundaryConditions", SupportedBoundaries, "step.boundaries", blockers);
            CheckDefinitions(step, "_fieldOutputs", SupportedFields, "step.field-outputs", blockers);
            CheckDefinitions(step, "_historyOutputs", SupportedHistories, "step.history-outputs", blockers);
            int defined = Count(step, "_definedFields");
            if (defined != 0) blockers.Add(new("step.defined-fields", defined, "Current PMA metadata does not persist defined fields."));
            var stepControls = Member(step, "_stepControls");
            int controls = ActiveModelInventory.ListItems(stepControls == null ? null : Member(stepControls, "_parameters")).Count();
            if (controls != 0) blockers.Add(new("step.controls", controls, "Current PMA metadata does not persist custom step controls."));
        }

        var grouped = blockers.GroupBy(item => (item.code, item.detail)).Select(group =>
            new ConversionFinding(group.Key.code, group.Sum(item => item.count), group.Key.detail)).OrderBy(item => item.code).ToArray();
        return new {
            status = !meshInspected ? "partial-mesh-inspection-required" : grouped.Length == 0 ? "eligible-for-converter-implementation" : "blocked-by-current-pma-contract",
            complete = meshInspected,
            blockerCount = grouped.Length,
            blockers = grouped,
            supported = new { modelSpace = "3D", staticSteps = steps.Length,
                note = "Use --mesh for a complete topology and size report. A clean complete report means inspected definitions fit the current PMA contract; a converter and round-trip validation are still required." }
        };
    }

    static void CheckDefinitions(ClassRecord step, string field, HashSet<string> supported, string code, List<ConversionFinding> blockers)
    {
        var collection = step.HasMember(field) ? step.GetClassRecord(field) : null;
        var unsupported = ActiveModelInventory.DictionaryItems(collection)
            .Select(item => item.TypeName.FullName).Where(type => !supported.Contains(type)).GroupBy(type => type);
        foreach (var group in unsupported)
            blockers.Add(new(code, group.Count(), "Unsupported current PMA definition type: " + group.Key + "."));
    }
}
