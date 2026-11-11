using CaeMesh;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record ProjectNodeSet(string Name, int[] Nodes);

public sealed record NodeSelectionBox(double XMin, double XMax, double YMin, double YMax, double ZMin, double ZMax);

public static partial class ProjectNodeSets
{
    public static int[] Nodes(string path) => StaticProjectArchive.Open(path).Model.Mesh.Nodes.Keys.Order().ToArray();
    public static ProjectNodeSet[] List(string path) => StaticProjectArchive.Open(path).Model.Mesh.NodeSets.Values
        .Select(set => new ProjectNodeSet(set.Name, set.Labels.ToArray())).ToArray();

    public static NodeSelectionBox Bounds(string path)
    {
        var nodes = StaticProjectArchive.Open(path).Model.Mesh.Nodes.Values.ToArray();
        return new(nodes.Min(n => n.X), nodes.Max(n => n.X), nodes.Min(n => n.Y), nodes.Max(n => n.Y), nodes.Min(n => n.Z), nodes.Max(n => n.Z));
    }

    public static int[] SelectBox(string path, NodeSelectionBox box)
    {
        if (new[] { box.XMin, box.XMax, box.YMin, box.YMax, box.ZMin, box.ZMax }.Any(v => !double.IsFinite(v)) ||
            box.XMin > box.XMax || box.YMin > box.YMax || box.ZMin > box.ZMax)
            throw new ArgumentException("Box limits must be finite with minimum <= maximum.", nameof(box));
        return StaticProjectArchive.Open(path).Model.Mesh.Nodes.Values
            .Where(n => n.X >= box.XMin && n.X <= box.XMax && n.Y >= box.YMin && n.Y <= box.YMax && n.Z >= box.ZMin && n.Z <= box.ZMax)
            .Select(n => n.Id).Order().ToArray();
    }

    public static void AddCopy(string source, string destination, string name, int[] nodeIds)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a node-set name.", nameof(name));
        if (nodeIds == null || nodeIds.Length == 0) throw new ArgumentException("Select at least one node.", nameof(nodeIds));
        var project = ProjectRegionReferences.Editable(source);
        var mesh = project.Model.Mesh;
        ProjectRegionReferences.ValidateName(name);
        Unique(project.Model, name);
        var ids = nodeIds.Distinct().Order().ToArray();
        if (ids.Any(id => !mesh.Nodes.ContainsKey(id))) throw new ArgumentException("Unknown mesh node.", nameof(nodeIds));
        var set = new FeNodeSet(name, ids);
        mesh.UpdateNodeSetCenterOfGravity(set);
        mesh.NodeSets.Add(name, set);
        ProjectEdits.SaveNew(destination, project);
    }
}
