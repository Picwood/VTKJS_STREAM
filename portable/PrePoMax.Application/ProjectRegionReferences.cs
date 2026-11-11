using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

internal sealed record RegionReference(string Description, bool Surface, Action<string> Rename);

/// <summary>Typed named-region references shared by the three region editors.</summary>
internal static class ProjectRegionReferences
{
    internal static Dictionary<string, List<RegionReference>> Collect(FeModel model, RegionTypeEnum kind)
    {
        var references = new Dictionary<string, List<RegionReference>>(StringComparer.OrdinalIgnoreCase);
        void Add(string? name, string description, Action<string> rename, bool surface = false)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!references.TryGetValue(name, out var list)) references[name] = list = [];
            list.Add(new(description, surface, rename));
        }
        void Inspect(NamedClass item, string description)
        {
            if (item is IMultiRegion region && region.RegionType == kind)
                Add(region.RegionName, description, name => region.RegionName = name);
            if (item is IMasterSlaveMultiRegion pair)
            {
                if (pair.MasterRegionType == kind) Add(pair.MasterRegionName, description + " (master)", name => pair.MasterRegionName = name);
                if (pair.SlaveRegionType == kind) Add(pair.SlaveRegionName, description + " (slave)", name => pair.SlaveRegionName = name);
            }
        }
        foreach (var section in model.Sections.Values) Inspect(section, "Section: " + section.Name);
        foreach (var constraint in model.Constraints.Values) Inspect(constraint, "Constraint: " + constraint.Name);
        foreach (var pair in model.ContactPairs.Values) Inspect(pair, "Contact pair: " + pair.Name);
        foreach (var condition in model.InitialConditions.Values) Inspect(condition, "Initial condition: " + condition.Name);
        foreach (var point in model.Mesh.ReferencePoints.Values) Inspect(point, "Reference point: " + point.Name);
        foreach (var step in model.StepCollection.StepsList)
        {
            foreach (var boundary in step.BoundaryConditions.Values) Inspect(boundary, step.Name + " / BC: " + boundary.Name);
            foreach (var load in step.Loads.Values) Inspect(load, step.Name + " / Load: " + load.Name);
            foreach (var field in step.DefinedFields.Values) Inspect(field, step.Name + " / Defined field: " + field.Name);
            foreach (var output in step.HistoryOutputs.Values) Inspect(output, step.Name + " / History output: " + output.Name);
        }
        foreach (var surface in model.Mesh.Surfaces.Values)
        {
            if (kind == RegionTypeEnum.ElementSetName && surface.ElementFaces != null)
                foreach (var face in surface.ElementFaces.ToArray())
                    Add(face.Value, "Surface: " + surface.Name + " / " + face.Key, name => surface.ElementFaces[face.Key] = name, true);
            if (kind == RegionTypeEnum.NodeSetName)
            {
                bool derived = surface.Type == FeSurfaceType.Element;
                Add(surface.NodeSetName, "Surface nodes: " + surface.Name, name => surface.NodeSetName = name, derived);
                Add(surface.CreatedFromNodeSetName, "Surface source: " + surface.Name, name => surface.CreatedFromNodeSetName = name, derived);
            }
        }
        return references;
    }

    internal static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 128 || name.Any(char.IsControl))
            throw new ArgumentException("Enter a region name of 1–128 characters without surrounding whitespace or control characters.");
    }

    internal static string? EditingError(FeModel model) => model.CalculixUserKeywords?.Count > 0
        ? "Region editing is unavailable while custom CalculiX keywords may contain untracked references." : null;

    internal static ReopenedStaticProject Editable(string source)
    {
        var project = StaticProjectArchive.Open(source);
        if (EditingError(project.Model) is { } error) throw new NotSupportedException(error);
        return project;
    }

    internal static void RequireUnused(List<RegionReference> uses)
    {
        if (uses.Count != 0) throw new NotSupportedException("Reassign these definitions before removing the region: " +
            string.Join("; ", uses.Select(use => use.Description).Distinct().Take(10)));
    }
}
