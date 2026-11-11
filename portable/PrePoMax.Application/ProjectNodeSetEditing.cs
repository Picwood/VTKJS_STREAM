using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record NodeSetSettings(string Name, string? NodeIds = null, NodeSelectionBox? Box = null, int[]? SelectedNodes = null);
public sealed record NodeSetSummary(string Name, int Nodes, bool MembershipLocked, string[] Dependents);
public sealed record NodeSetDetails(string Name, string? NodeIds);
public sealed record NodeSetEditorData(NodeSetSummary[] Sets, NodeSetDetails? Selected, NodeSelectionBox Bounds,
    int Nodes, int[] SampleNodeIds, string? EditingError);

public static partial class ProjectNodeSets
{
    public static NodeSetEditorData Read(string path, string? name = null)
    {
        var model = StaticProjectArchive.Open(path).Model;
        var references = ProjectRegionReferences.Collect(model, RegionTypeEnum.NodeSetName);
        var selected = name == null ? null : Find(model, name);
        string? expression = selected == null ? null : MeshIdSelection.Format(selected.Labels);
        if (expression?.Length > MeshIdSelection.MaxExpression) expression = null;
        var nodes = model.Mesh.Nodes.Values.ToArray();
        return new(model.Mesh.NodeSets.Values.Where(set => !set.Internal).Select(set =>
        {
            var uses = references.GetValueOrDefault(set.Name) ?? [];
            return new NodeSetSummary(set.Name, set.Labels.Length, uses.Any(use => use.Surface), uses.Select(use => use.Description).Distinct().ToArray());
        }).ToArray(), selected == null ? null : new(selected.Name, expression),
            new(nodes.Min(n => n.X), nodes.Max(n => n.X), nodes.Min(n => n.Y), nodes.Max(n => n.Y), nodes.Min(n => n.Z), nodes.Max(n => n.Z)),
            nodes.Length, model.Mesh.Nodes.Keys.Order().Take(1000).ToArray(), ProjectRegionReferences.EditingError(model));
    }

    public static int[] Preview(string path, NodeSetSettings settings, string? previousName = null)
    {
        var model = StaticProjectArchive.Open(path).Model;
        var set = Create(model, settings, previousName == null ? null : Find(model, previousName));
        if (set.Labels.Length > 10000) throw new NotSupportedException("Preview supports at most 10000 nodes; the complete set can still be applied.");
        return set.Labels;
    }

    public static void AddCopy(string source, string destination, NodeSetSettings settings)
    {
        var project = ProjectRegionReferences.Editable(source);
        var set = Create(project.Model, settings, null);
        Unique(project.Model, set.Name);
        project.Model.Mesh.NodeSets.Add(set.Name, set);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveCopy(string source, string destination, string name, NodeSetSettings settings)
    {
        var project = ProjectRegionReferences.Editable(source);
        var model = project.Model;
        var previous = Find(model, name);
        var replacement = Create(model, settings, previous);
        Unique(model, replacement.Name, previous);
        var uses = ProjectRegionReferences.Collect(model, RegionTypeEnum.NodeSetName).GetValueOrDefault(previous.Name) ?? [];
        if (uses.Any(use => use.Surface) && !previous.Labels.Order().SequenceEqual(replacement.Labels.Order()))
            throw new NotSupportedException("This node set supplies an element surface. Edit the surface membership instead; the node set can still be renamed or duplicated.");
        model.Mesh.NodeSets.Replace(previous.Name, replacement.Name, replacement);
        foreach (var use in uses) use.Rename(replacement.Name);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void DuplicateCopy(string source, string destination, string name, string newName)
    {
        var project = ProjectRegionReferences.Editable(source);
        var set = Create(project.Model, new(newName), Find(project.Model, name));
        Unique(project.Model, set.Name);
        project.Model.Mesh.NodeSets.Add(set.Name, set);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string name)
    {
        var project = ProjectRegionReferences.Editable(source);
        var set = Find(project.Model, name);
        ProjectRegionReferences.RequireUnused(ProjectRegionReferences.Collect(project.Model, RegionTypeEnum.NodeSetName).GetValueOrDefault(set.Name) ?? []);
        project.Model.Mesh.NodeSets.Remove(set.Name);
        ProjectEdits.SaveNew(destination, project);
    }

    private static FeNodeSet Create(FeModel model, NodeSetSettings settings, FeNodeSet? previous)
    {
        ProjectRegionReferences.ValidateName(settings.Name);
        if ((settings.NodeIds != null ? 1 : 0) + (settings.Box != null ? 1 : 0) + (settings.SelectedNodes != null ? 1 : 0) > 1)
            throw new ArgumentException("Choose one membership source.");
        int[] ids;
        if (settings.SelectedNodes != null)
        {
            if (settings.SelectedNodes.Length > ProjectMeshNodeSelection.MaximumNodes) throw new ArgumentException("Interactive selection supports at most 100000 nodes.");
            ids = settings.SelectedNodes.Distinct().Order().ToArray();
        }
        else if (settings.NodeIds != null) ids = MeshIdSelection.Parse(settings.NodeIds, "node");
        else if (settings.Box is { } box)
        {
            ValidateBox(box);
            ids = model.Mesh.Nodes.Values.Where(n => n.X >= box.XMin && n.X <= box.XMax && n.Y >= box.YMin && n.Y <= box.YMax &&
                n.Z >= box.ZMin && n.Z <= box.ZMax).Select(n => n.Id).Order().ToArray();
        }
        else if (previous != null) ids = previous.Labels.ToArray();
        else throw new ArgumentException("Choose existing node IDs or a coordinate box.");
        if (ids.Length == 0 || ids.Any(id => !model.Mesh.Nodes.ContainsKey(id))) throw new ArgumentException("Select at least one existing mesh node.");
        var set = new FeNodeSet(settings.Name, ids) { Active = previous?.Active ?? true };
        model.Mesh.UpdateNodeSetCenterOfGravity(set);
        return set;
    }

    private static void ValidateBox(NodeSelectionBox box)
    {
        if (new[] { box.XMin, box.XMax, box.YMin, box.YMax, box.ZMin, box.ZMax }.Any(v => !double.IsFinite(v)) ||
            box.XMin > box.XMax || box.YMin > box.YMax || box.ZMin > box.ZMax)
            throw new ArgumentException("Box limits must be finite with minimum <= maximum.");
    }
    private static FeNodeSet Find(FeModel model, string name) => model.Mesh.NodeSets.Values.SingleOrDefault(set =>
        set.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !set.Internal) ?? throw new ArgumentException("Select an existing user node set.");
    private static void Unique(FeModel model, string name, FeNodeSet? except = null)
    {
        if (model.Mesh.NodeSets.Values.Any(set => !ReferenceEquals(set, except) && set.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Node-set names must be unique, ignoring case.");
    }
}
