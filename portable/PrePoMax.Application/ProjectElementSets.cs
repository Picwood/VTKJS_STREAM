using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record ElementSetSettings(string Name, string? ElementIds = null, string[]? Parts = null);
public sealed record ElementSetSummary(string Name, int Elements, bool CreatedFromParts, bool MembershipLocked, string[] Dependents);
public sealed record ElementSetPart(string Name, int Id, int Elements);
public sealed record ElementSetDetails(string Name, string? ElementIds, string[] Parts, bool CreatedFromParts);
public sealed record ElementSetEditorData(ElementSetSummary[] Sets, ElementSetPart[] Parts, int Elements,
    int[] SampleElementIds, ElementSetDetails? Selected, string? EditingError);

/// <summary>User element sets, including safe updates of named production region references.</summary>
public static class ProjectElementSets
{

    public static ElementSetEditorData Read(string path, string? name = null)
    {
        var model = StaticProjectArchive.Open(path).Model;
        var mesh = model.Mesh;
        var references = References(model);
        var selected = name == null ? null : Find(model, name);
        string? expression = selected == null || selected.CreatedFromParts ? null : Format(selected.Labels);
        if (expression?.Length > MeshIdSelection.MaxExpression) expression = null;
        return new(mesh.ElementSets.Values.Where(set => !set.Internal).Select(set =>
        {
            var uses = references.GetValueOrDefault(set.Name) ?? [];
            return new ElementSetSummary(set.Name, Resolve(mesh, set).Length, set.CreatedFromParts,
                uses.Any(item => item.Surface), uses.Select(item => item.Description).Distinct().ToArray());
        }).ToArray(), mesh.Parts.Values.Select(part => new ElementSetPart(part.Name, part.PartId, part.Labels.Length)).ToArray(),
            mesh.Elements.Count, mesh.Elements.Keys.Order().Take(1000).ToArray(),
            selected == null ? null : new(selected.Name, expression,
                selected.CreatedFromParts ? mesh.Parts.Values.Where(part => selected.Labels.Contains(part.PartId)).Select(part => part.Name).ToArray() : [],
                selected.CreatedFromParts), EditingError(model));
    }

    public static ModelTreeSelection Preview(string path, ElementSetSettings settings, string? previousName = null)
    {
        var model = StaticProjectArchive.Open(path).Model;
        var candidate = Create(model, settings, previousName == null ? null : Find(model, previousName));
        int[] nodes = Resolve(model.Mesh, candidate).SelectMany(id => model.Mesh.Elements[id].NodeIds).Distinct().Take(10001).ToArray();
        if (nodes.Length > 10000) throw new NotSupportedException("Preview supports at most 10000 nodes; the complete set can still be applied.");
        return new(nodes.Order().ToArray(), []);
    }

    public static void AddCopy(string source, string destination, ElementSetSettings settings)
    {
        var project = Editable(source);
        var set = Create(project.Model, settings, null);
        Unique(project.Model, set.Name);
        project.Model.Mesh.ElementSets.Add(set.Name, set);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveCopy(string source, string destination, string name, ElementSetSettings settings)
    {
        var project = Editable(source);
        var model = project.Model;
        var previous = Find(model, name);
        var replacement = Create(model, settings, previous);
        Unique(model, replacement.Name, previous);
        var uses = References(model).GetValueOrDefault(previous.Name) ?? [];
        if (uses.Any(use => use.Surface) && (previous.CreatedFromParts != replacement.CreatedFromParts ||
            !previous.Labels.Order().SequenceEqual(replacement.Labels.Order())))
            throw new NotSupportedException("This set backs an element surface. Edit surface membership through the surface workflow; its set can still be renamed or duplicated.");
        model.Mesh.ElementSets.Replace(previous.Name, replacement.Name, replacement);
        foreach (var use in uses) use.Rename(replacement.Name);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void DuplicateCopy(string source, string destination, string name, string newName)
    {
        var project = Editable(source);
        var original = Find(project.Model, name);
        var duplicate = Create(project.Model, new(newName), original);
        Unique(project.Model, duplicate.Name);
        project.Model.Mesh.ElementSets.Add(duplicate.Name, duplicate);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string name)
    {
        var project = Editable(source);
        var set = Find(project.Model, name);
        var uses = References(project.Model).GetValueOrDefault(set.Name) ?? [];
        if (uses.Count != 0)
            throw new NotSupportedException("Reassign these definitions before removing the set: " +
                string.Join("; ", uses.Select(use => use.Description).Distinct().Take(10)));
        project.Model.Mesh.ElementSets.Remove(set.Name);
        ProjectEdits.SaveNew(destination, project);
    }

    internal static int[] Resolve(FeMesh mesh, FeElementSet set)
    {
        if (!set.CreatedFromParts) return set.Labels.ToArray();
        var parts = mesh.Parts.Values.ToDictionary(part => part.PartId);
        if (set.Labels.Any(id => !parts.ContainsKey(id))) throw new InvalidDataException("The set references a missing mesh part.");
        return set.Labels.SelectMany(id => parts[id].Labels).Distinct().Order().ToArray();
    }

    private static FeElementSet Create(FeModel model, ElementSetSettings settings, FeElementSet? previous)
    {
        if (string.IsNullOrWhiteSpace(settings.Name) || settings.Name != settings.Name.Trim() ||
            settings.Name.Length > 128 || settings.Name.Any(char.IsControl))
            throw new ArgumentException("Enter a set name of 1–128 characters without surrounding whitespace or control characters.");
        if (settings.ElementIds != null && settings.Parts != null)
            throw new ArgumentException("Choose element IDs or whole parts, not both.");
        int[] labels;
        bool fromParts;
        if (settings.ElementIds != null)
        {
            labels = Parse(settings.ElementIds);
            if (labels.Any(id => !model.Mesh.Elements.ContainsKey(id))) throw new ArgumentException("The selection contains an unknown element ID.");
            fromParts = false;
        }
        else if (settings.Parts != null)
        {
            if (settings.Parts.Length > 1000) throw new ArgumentException("Choose at most 1000 mesh parts.");
            var parts = settings.Parts.Distinct(StringComparer.OrdinalIgnoreCase).Select(name =>
                model.Mesh.Parts.Values.SingleOrDefault(part => part.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("The selection contains an unknown mesh part.")).ToArray();
            labels = parts.Select(part => part.PartId).Order().ToArray();
            fromParts = true;
        }
        else if (previous != null)
        {
            labels = previous.Labels.ToArray();
            fromParts = previous.CreatedFromParts;
        }
        else throw new ArgumentException("Select element IDs or whole mesh parts.");
        if (labels.Length == 0) throw new ArgumentException("Select at least one element or nonempty mesh part.");
        var candidate = new FeElementSet(settings.Name, labels, fromParts) { Active = previous?.Active ?? true };
        int[] ids = Resolve(model.Mesh, candidate);
        if (ids.Length == 0) throw new ArgumentException("Choose a nonempty mesh region.");
        return candidate;
    }

    private static int[] Parse(string expression) => MeshIdSelection.Parse(expression, "element");

    private static string Format(int[] labels) => MeshIdSelection.Format(labels);

    private static ReopenedStaticProject Editable(string source)
    {
        var project = StaticProjectArchive.Open(source);
        if (EditingError(project.Model) is { } error) throw new NotSupportedException(error);
        return project;
    }
    private static string? EditingError(FeModel model) => model.CalculixUserKeywords?.Count > 0
        ? "Element-set editing is unavailable while custom CalculiX keywords may contain untracked region references." : null;
    private static FeElementSet Find(FeModel model, string name) =>
        model.Mesh.ElementSets.Values.SingleOrDefault(set => set.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !set.Internal)
        ?? throw new ArgumentException("Select an existing user element set.");
    private static void Unique(FeModel model, string name, FeElementSet? except = null)
    {
        if (model.Mesh.ElementSets.Values.Any(set => !ReferenceEquals(set, except) && set.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            model.Mesh.Parts.Keys.Any(part => part.Equals(name, StringComparison.OrdinalIgnoreCase)) &&
                (except == null || !name.Equals(except.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Choose a name distinct from existing element sets and mesh parts, ignoring case.");
    }

    private static Dictionary<string, List<RegionReference>> References(FeModel model) =>
        ProjectRegionReferences.Collect(model, RegionTypeEnum.ElementSetName);
}
