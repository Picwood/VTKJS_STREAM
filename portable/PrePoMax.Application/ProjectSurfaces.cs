using CaeMesh;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record ProjectElementFace(int ElementId, string Face);
public sealed record ProjectPickedTriangle(int ElementId, int[] Nodes);
public sealed record ProjectSurface(string Name, int FaceCount, int ElementCount);
public sealed record ProjectSurfaceElement(int Id, string[] Faces);
public sealed record ProjectSurfaceInventory(int Total, ProjectSurfaceElement[] Elements);

/// <summary>Creates bounded named element surfaces from explicit element-face pairs.</summary>
public static partial class ProjectSurfaces
{
    internal const int MaximumFaces = 100;

    public static ProjectSurface[] List(string path)
    {
        var mesh = StaticProjectArchive.Open(path).Model.Mesh;
        return mesh.Surfaces.Values.Where(surface => surface.Type == FeSurfaceType.Element)
            .Select(surface => new ProjectSurface(surface.Name,
                surface.ElementFaces?.Values.Where(mesh.ElementSets.ContainsKey)
                    .Sum(set => mesh.ElementSets[set].Labels.Length) ?? 0,
                surface.ElementFaces?.Values.Where(mesh.ElementSets.ContainsKey)
                    .SelectMany(set => mesh.ElementSets[set].Labels).Distinct().Count() ?? 0)).ToArray();
    }

    public static ProjectSurfaceInventory Inventory(string path, int maximum = 1000)
    {
        if (maximum is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maximum));
        var elements = StaticProjectArchive.Open(path).Model.Mesh.Elements.Values
            .Where(element => element.Dimension == 3).OrderBy(element => element.Id).ToArray();
        return new ProjectSurfaceInventory(elements.Length, elements.Take(maximum).Select(element =>
            new ProjectSurfaceElement(element.Id, ValidFaces(element).Select(face => face.ToString()).ToArray())).ToArray());
    }

    public static int[] FaceNodes(string path, ProjectElementFace[] faces)
    {
        var mesh = StaticProjectArchive.Open(path).Model.Mesh;
        return Normalize(mesh, faces).SelectMany(item =>
            mesh.Elements[item.ElementId].GetNodeIdsFromFaceName(item.Face)).Distinct().Order().ToArray();
    }

    public static ProjectElementFace ResolveFace(string path, int elementId, int[] triangleNodeIds)
    {
        var mesh = StaticProjectArchive.Open(path).Model.Mesh;
        return ResolveFace(mesh, elementId, triangleNodeIds);
    }

    public static ProjectElementFace[] ResolveFaces(string path, ProjectPickedTriangle[] triangles)
    {
        if (triangles == null || triangles.Length is < 1 or > 1000)
            throw new ArgumentException("Select between 1 and 1000 rendered triangles.", nameof(triangles));
        var mesh = StaticProjectArchive.Open(path).Model.Mesh;
        var faces = triangles.Select(triangle => triangle == null
            ? throw new ArgumentException("A picked triangle is missing.", nameof(triangles))
            : ResolveFace(mesh, triangle.ElementId, triangle.Nodes)).Distinct().OrderBy(face => face.ElementId)
            .ThenBy(face => face.Face).ToArray();
        if (faces.Length > MaximumFaces) throw new ArgumentException($"Selection resolves to more than {MaximumFaces} element faces.", nameof(triangles));
        return faces;
    }

    public static ProjectElementFace[] GrowFace(string path, int elementId, int[] triangleNodeIds, double angleDegrees)
    {
        if (!double.IsFinite(angleDegrees) || angleDegrees is < 0 or > 90)
            throw new ArgumentOutOfRangeException(nameof(angleDegrees), "Face growth angle must be between 0 and 90 degrees.");
        var mesh = StaticProjectArchive.Open(path).Model.Mesh;
        var seed = ResolveFace(mesh, elementId, triangleNodeIds);
        var seedFace = (seed.ElementId, Enum.Parse<FeFaceName>(seed.Face));
        if (!IsBoundaryFace(mesh, seedFace.ElementId, seedFace.Item2))
            throw new ArgumentException("The picked face is not on the solid boundary.", nameof(triangleNodeIds));
        var selected = new HashSet<(int ElementId, FeFaceName Face)> { seedFace };
        var pending = new Queue<(int ElementId, FeFaceName Face)>();
        pending.Enqueue(seedFace);
        double maximumAngle = angleDegrees * Math.PI / 180;
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            int[] currentCorners = CornerNodes(mesh.Elements[current.ElementId].GetNodeIdsFromFaceName(current.Face));
            mesh.GetElementFaceNormal(current.ElementId, current.Face, out double[] currentNormal);
            for (int edgeIndex = 0; edgeIndex < currentCorners.Length; edgeIndex++)
            {
                int[] edge = [currentCorners[edgeIndex], currentCorners[(edgeIndex + 1) % currentCorners.Length]];
                foreach (int adjacentElementId in mesh.GetElementIdsFromNodeIds(edge, true, false, false,
                    mesh.Elements[current.ElementId].PartId))
                {
                    if (!mesh.Elements.TryGetValue(adjacentElementId, out var adjacentElement) || adjacentElement.Dimension != 3) continue;
                    foreach (FeFaceName adjacentFace in ValidFaces(adjacentElement))
                    {
                        var candidate = (adjacentElementId, adjacentFace);
                        if (selected.Contains(candidate)) continue;
                        int[] adjacentCorners = CornerNodes(adjacentElement.GetNodeIdsFromFaceName(adjacentFace));
                        if (!HasEdge(adjacentCorners, edge) || !IsBoundaryFace(mesh, adjacentElementId, adjacentFace)) continue;
                        mesh.GetElementFaceNormal(adjacentElementId, adjacentFace, out double[] adjacentNormal);
                        if (NormalAngle(currentNormal, adjacentNormal) > maximumAngle + 1e-10) continue;
                        selected.Add(candidate);
                        if (selected.Count > MaximumFaces)
                            throw new ArgumentException($"Angle growth resolves to more than {MaximumFaces} element faces.", nameof(angleDegrees));
                        pending.Enqueue(candidate);
                    }
                }
            }
        }
        return selected.OrderBy(item => item.ElementId).ThenBy(item => item.Face)
            .Select(item => new ProjectElementFace(item.ElementId, item.Face.ToString())).ToArray();
    }

    private static ProjectElementFace ResolveFace(FeMesh mesh, int elementId, int[] triangleNodeIds)
    {
        if (triangleNodeIds == null || triangleNodeIds.Length != 3 || triangleNodeIds.Distinct().Count() != 3)
            throw new ArgumentException("A picked triangle requires three distinct source nodes.", nameof(triangleNodeIds));
        if (!mesh.Elements.TryGetValue(elementId, out var element) || element.Dimension != 3 ||
            triangleNodeIds.Any(id => !element.NodeIds.Contains(id)))
            throw new ArgumentException("The picked triangle does not belong to an existing solid element.", nameof(elementId));
        var picked = triangleNodeIds.ToHashSet();
        var matches = ValidFaces(element).Where(face => picked.IsSubsetOf(element.GetNodeIdsFromFaceName(face))).ToArray();
        if (matches.Length != 1) throw new ArgumentException("The picked triangle does not identify one solid face.", nameof(triangleNodeIds));
        return new ProjectElementFace(elementId, matches[0].ToString());
    }

    private static bool IsBoundaryFace(FeMesh mesh, int elementId, FeFaceName face)
    {
        int[] nodes = mesh.Elements[elementId].GetNodeIdsFromFaceName(face);
        return mesh.GetElementIdsFromNodeIds(nodes, false, true, false).Count(id =>
            mesh.Elements.TryGetValue(id, out var element) && element.Dimension == 3) == 1;
    }

    private static int[] CornerNodes(int[] nodes) => nodes.Length switch
    {
        3 or 4 => nodes,
        6 => nodes.Take(3).ToArray(),
        8 => nodes.Take(4).ToArray(),
        _ => throw new NotSupportedException("Face growth requires triangular or quadrilateral solid faces.")
    };

    private static bool HasEdge(int[] corners, int[] edge) => Enumerable.Range(0, corners.Length).Any(index =>
        (corners[index] == edge[0] && corners[(index + 1) % corners.Length] == edge[1]) ||
        (corners[index] == edge[1] && corners[(index + 1) % corners.Length] == edge[0]));

    private static double NormalAngle(double[] first, double[] second)
    {
        double firstLength = Math.Sqrt(first.Sum(value => value * value));
        double secondLength = Math.Sqrt(second.Sum(value => value * value));
        if (!(firstLength > 0) || !(secondLength > 0)) throw new InvalidDataException("A face normal is degenerate.");
        double dot = Math.Abs(first.Zip(second, (left, right) => left * right).Sum()) / (firstLength * secondLength);
        return Math.Acos(Math.Clamp(dot, 0, 1));
    }

    public static void AddCopy(string source, string destination, string name, ProjectElementFace[] faces)
    {
        name = name?.Trim() ?? "";
        ProjectRegionReferences.ValidateName(name);
        var project = ProjectRegionReferences.Editable(source);
        Unique(project.Model, name);
        var normalized = Normalize(project.Model.Mesh, faces);
        var surface = Build(project.Model.Mesh, name,
            normalized.Select(face => new ProjectElementFace(face.ElementId, face.Face.ToString())));
        project.Model.Mesh.Surfaces.Add(surface.Name, surface);
        ProjectEdits.SaveNew(destination, project);
    }

    internal static (int ElementId, FeFaceName Face)[] Normalize(FeMesh mesh, ProjectElementFace[] faces)
    {
        if (faces == null || faces.Length is < 1 or > MaximumFaces)
            throw new ArgumentException($"Select between 1 and {MaximumFaces} element faces.", nameof(faces));
        var normalized = new HashSet<(int ElementId, FeFaceName Face)>();
        foreach (var item in faces)
        {
            if (item == null || item.ElementId <= 0 ||
                !Enum.TryParse(item.Face, true, out FeFaceName face) || face == FeFaceName.Empty || !Enum.IsDefined(face) ||
                !mesh.Elements.TryGetValue(item.ElementId, out var element) || element.Dimension != 3)
                throw new ArgumentException("Select existing solid elements and valid face labels.", nameof(faces));
            try { element.GetNodeIdsFromFaceName(face); }
            catch (NotSupportedException ex) { throw new ArgumentException("A face label is invalid for its element type.", nameof(faces), ex); }
            normalized.Add((item.ElementId, face));
        }
        return normalized.OrderBy(item => item.ElementId).ThenBy(item => item.Face).ToArray();
    }

    private static IEnumerable<FeFaceName> ValidFaces(FeElement element)
    {
        foreach (FeFaceName face in Enum.GetValues<FeFaceName>().Where(face => face != FeFaceName.Empty))
        {
            try { element.GetNodeIdsFromFaceName(face); }
            catch (NotSupportedException) { continue; }
            yield return face;
        }
    }
}
