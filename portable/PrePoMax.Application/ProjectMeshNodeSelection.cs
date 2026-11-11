using CaeMesh;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record MeshNodePick(string Mode, int ElementId, int[] TriangleNodes, double[] Point, double Angle = 30);
public sealed record MeshNodeArea(string Mode, int[] NodeIds, int[] ElementIds, bool CompletelyInside = true);

/// <summary>Node-target dispatch from the original controller, without its native UI dependency.</summary>
public static class ProjectMeshNodeSelection
{
    public const int MaximumNodes = 100000;

    public static int[] Initial(string path, string? name) => Initial(StaticProjectArchive.Open(path).Model.Mesh, name);
    public static int[] Initial(FeMesh mesh, string? name) => name == null ? [] : Bounded(mesh.NodeSets.Values
        .SingleOrDefault(set => !set.Internal && set.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Labels
        ?? throw new ArgumentException("Select an existing user node set."));

    public static int[] Universe(string path) => Universe(StaticProjectArchive.Open(path).Model.Mesh);
    public static int[] Universe(FeMesh mesh) => Bounded(mesh.Parts.Values.Where(p => p.Visible && p.Active).SelectMany(p => p.NodeLabels));

    public static int[] Ids(string path, string expression) => Ids(StaticProjectArchive.Open(path).Model.Mesh, expression);
    public static int[] Ids(FeMesh mesh, string expression)
    {
        var ids = MeshIdSelection.Parse(expression, "node");
        if (ids.Any(id => !mesh.Nodes.ContainsKey(id))) throw new ArgumentException("Unknown mesh node.");
        var visible = mesh.Parts.Values.Where(p => p.Visible && p.Active).SelectMany(p => p.NodeLabels).ToHashSet();
        return ids.Where(visible.Contains).ToArray();
    }

    public static int[] Pick(string path, MeshNodePick pick) => Pick(StaticProjectArchive.Open(path).Model.Mesh, pick);
    public static int[] Pick(FeMesh mesh, MeshNodePick pick)
    {
        if (pick == null || pick.Point is not { Length: 3 } || pick.Point.Any(v => !double.IsFinite(v)) ||
            pick.TriangleNodes is not { Length: 3 } || pick.TriangleNodes.Distinct().Count() != 3 ||
            !double.IsFinite(pick.Angle) || pick.Angle < 0 || pick.Angle > 180)
            throw new ArgumentException("Select a mesh primitive with a finite point and angle between 0 and 180 degrees.");
        if (!mesh.Elements.TryGetValue(pick.ElementId, out var element)) throw new ArgumentException("Unknown picked element.");
        var part = mesh.GetPartContainingElementId(pick.ElementId);
        if (part == null || !part.Visible || !part.Active) throw new ArgumentException("The picked part is hidden or inactive.");
        // PMA stores the mesh, not visualization caches. Rebuild the same production topology used by the original UI.
        if (part.Visualization.Cells == null || part.Visualization.CellIdsByFace == null || part.Visualization.CellNeighboursOverCellEdge == null)
            mesh.ExtractPartVisualization(part, false, Globals.EdgeAngle);
        if (!mesh.TryGetVisualizationFaceNodeIds(pick.ElementId, pick.TriangleNodes, out int[] face))
            throw new ArgumentException("The rendered triangle does not identify an exterior mesh face.");
        int[] result = pick.Mode switch
        {
            "node" => [mesh.GetCellFaceNodeIdClosestToPoint(pick.Point, face)],
            "element" => element.NodeIds,
            "part" => part.NodeLabels,
            "face" => mesh.GetSurfaceNodeIds(pick.ElementId, face),
            "face-angle" => mesh.GetSurfaceByAngleNodeIds(pick.ElementId, face, pick.Angle),
            "edge" => mesh.GetEdgeNodeIds(pick.ElementId, ClosestEdge(mesh, face, pick.Point)),
            "edge-angle" => mesh.GetEdgeByAngleNodeIds(pick.ElementId, ClosestEdge(mesh, face, pick.Point), pick.Angle),
            _ => throw new ArgumentException("Choose Node, Element, Edge, Face, Edge angle, Face angle or Part for picking.")
        };
        if (result == null || result.Length == 0) throw new ArgumentException("No mesh topology was resolved for this pick. Try another mesh edge or face.");
        return Bounded(result);
    }

    public static int[] Area(string path, MeshNodeArea area) => Area(StaticProjectArchive.Open(path).Model.Mesh, area);
    public static int[] Area(FeMesh mesh, MeshNodeArea area)
    {
        if (area == null || area.NodeIds == null || area.ElementIds == null ||
            area.NodeIds.Length > MaximumNodes || area.ElementIds.Length > MaximumNodes ||
            area.NodeIds.Any(id => !mesh.Nodes.ContainsKey(id)) || area.ElementIds.Any(id => !mesh.Elements.ContainsKey(id)))
            throw new ArgumentException("The area contains invalid mesh IDs or exceeds 100000 items.");
        var parts = mesh.Parts.Values.Where(p => p.Visible && p.Active).ToArray();
        var visibleNodes = parts.SelectMany(p => p.NodeLabels).ToHashSet();
        var visibleElements = parts.SelectMany(p => p.Labels).ToHashSet();
        var nodes = area.NodeIds.Where(visibleNodes.Contains).ToHashSet();
        var elements = area.ElementIds.Where(visibleElements.Contains).ToArray();
        return area.Mode switch
        {
            "node" => Bounded(nodes),
            // Original GetNodeIdsFromFrustum keeps elements whose complete node list lies inside.
            "element" => Bounded(elements.Where(id => mesh.Elements[id].NodeIds.All(nodes.Contains)).SelectMany(id => mesh.Elements[id].NodeIds)),
            "part" => Bounded(parts.Where(p => p.Labels.Intersect(elements).Any()).SelectMany(p => p.NodeLabels)),
            _ => throw new ArgumentException("Rectangle selection is supported in Node, Element and Part modes. Pick a point for edges, faces and angles.")
        };
    }

    // Use the face perimeter, never a diagonal introduced by renderer triangulation.
    private static int[] ClosestEdge(FeMesh mesh, int[] face, double[] point)
    {
        int corners = face.Length switch { 3 or 6 => 3, 4 or 8 => 4, _ => throw new NotSupportedException("Unsupported mesh face topology.") };
        double best = double.PositiveInfinity;
        int[]? selected = null;
        for (int i = 0; i < corners; i++)
        {
            int[] edge = face.Length == corners ? [face[i], face[(i + 1) % corners]] : [face[i], face[corners + i], face[(i + 1) % corners]];
            double distance = double.PositiveInfinity;
            for (int j = 0; j < edge.Length - 1; j++)
            {
                var a = mesh.Nodes[edge[j]].Coor;
                var b = mesh.Nodes[edge[j + 1]].Coor;
                double length = Enumerable.Range(0, 3).Sum(k => (b[k] - a[k]) * (b[k] - a[k]));
                if (length == 0) continue;
                double t = Math.Clamp(Enumerable.Range(0, 3).Sum(k => (point[k] - a[k]) * (b[k] - a[k])) / length, 0, 1);
                distance = Math.Min(distance, Enumerable.Range(0, 3).Sum(k => Math.Pow(point[k] - a[k] - t * (b[k] - a[k]), 2)));
            }
            if (distance < best) { best = distance; selected = edge; }
        }
        return selected ?? throw new ArgumentException("The selected face has no nondegenerate edge.");
    }

    private static int[] Bounded(IEnumerable<int> nodes)
    {
        var result = nodes.Distinct().Take(MaximumNodes + 1).Order().ToArray();
        if (result.Length > MaximumNodes) throw new NotSupportedException("Interactive selection supports at most 100000 nodes.");
        return result;
    }
}
