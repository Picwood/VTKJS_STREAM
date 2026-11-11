using System.Text;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

/// <summary>Undeformed exterior mesh in the renderer's version 1 host-upload format.</summary>
public static class ProjectScene
{
    public static void Export(string projectPath, string outputPath, ResultField? field = null, double deformationScale = 0, int[]? selectedNodes = null,
                              ProjectResultSession? resultSession = null, CancellationToken cancellationToken = default,
                              ProjectElementFace[]? selectedFaces = null)
        => ExportCore(projectPath, outputPath, field, deformationScale, selectedNodes, selectedFaces, resultSession, cancellationToken);

    internal static string? ExportCore(string projectPath, string outputPath, ResultField? field, double deformationScale = 0,
                                      int[]? selectedNodes = null, ProjectElementFace[]? selectedFaces = null,
                                      ProjectResultSession? resultSession = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(deformationScale) || Math.Abs(deformationScale) > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(deformationScale), "Scale must be finite and between -1000000 and 1000000.");
        if (field == null && deformationScale != 0)
            throw new InvalidDataException("Deformation requires a selected result frame.");
        var markers = (selectedNodes ?? Array.Empty<int>()).Distinct().Order().ToArray();
        var faceSelection = selectedFaces ?? Array.Empty<ProjectElementFace>();
        if ((markers.Length != 0 || faceSelection.Length != 0) && field != null)
            throw new NotSupportedException("Selection preview uses undeformed mesh geometry.");
        var selected = field == null ? null : resultSession == null
            ? ProjectResults.Values(projectPath, field, deformationScale != 0)
            : resultSession.Values(projectPath, field, deformationScale != 0, cancellationToken);
        var model = field == null ? StaticProjectArchive.OpenModelForScene(projectPath) : null;
        var sceneMesh = selected?.Mesh ?? model!.Mesh;
        var scalars = selected?.Values;
        if (markers.Length > 10000 || markers.Any(id => !model!.Mesh.Nodes.ContainsKey(id)))
            throw new ArgumentException("Preview requires at most 10000 existing nodes.", nameof(selectedNodes));
        var normalizedFaces = faceSelection.Length == 0
            ? Array.Empty<(int ElementId, CaeMesh.FeFaceName Face)>()
            : ProjectSurfaces.Normalize(model!.Mesh, faceSelection);
        var previewFaces = normalizedFaces.Select(item => new
        {
            item.ElementId,
            Nodes = model!.Mesh.Elements[item.ElementId].GetNodeIdsFromFaceName(item.Face)
        }).ToArray();
        var previewFaceNodes = previewFaces.SelectMany(item => item.Nodes).Distinct().Order().ToArray();
        var parts = sceneMesh.Parts.Values.ToArray();
        int previewActorCount = (normalizedFaces.Length > 0 ? 1 : 0) + (markers.Length > 0 ? 1 : 0);
        if (parts.Length + previewActorCount > 512) throw new NotSupportedException("Scene exceeds 512 actors.");
        var sceneParts = parts.Select(part => new
        {
            Part = part,
            Nodes = part.Visualization.Cells.SelectMany(cell => cell).Distinct().Order().ToArray()
        }).ToArray();
        long points = 0, triangles = 0, bytes = 12;
        foreach (var scenePart in sceneParts)
        {
            var part = scenePart.Part;
            int nameBytes = Encoding.UTF8.GetByteCount(part.Name);
            if (nameBytes > 1024) throw new NotSupportedException("Scene part name is too long.");
            var cells = part.Visualization.Cells;
            if (cells.Length == 0 || cells.Any(c => c.Length is not (3 or 4 or 6 or 8)))
                throw new NotSupportedException("Scene export requires supported linear or quadratic exterior faces.");
            if (scenePart.Nodes.Length == 0 || scenePart.Nodes.Any(id => !sceneMesh.Nodes.ContainsKey(id)))
                throw new InvalidDataException("Scene visualization references a missing node.");
            points += scenePart.Nodes.Length;
            long partTriangles = cells.Sum(TriangleCount);
            triangles += partTriangles;
            bytes += 44L + nameBytes + 32L * scenePart.Nodes.Length + 16L * partTriangles;
        }
        points += markers.Length * 6L;
        triangles += markers.Length * 8L;
        bytes += markers.Length == 0 ? 0 : 64L + markers.Length * 320L;
        long previewFaceTriangles = previewFaces.Sum(item => TriangleCount(item.Nodes));
        points += previewFaceNodes.Length;
        triangles += previewFaceTriangles;
        bytes += normalizedFaces.Length == 0 ? 0 : 64L + 32L * previewFaceNodes.Length + 16L * previewFaceTriangles;
        if (points > 5_000_000 || triangles > 10_000_000 || bytes > 256L * 1024 * 1024)
            throw new NotSupportedException("Scene exceeds renderer upload bounds.");
        string destination = Path.GetFullPath(outputPath);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var writer = new BinaryWriter(new FileStream(temporary, FileMode.CreateNew), Encoding.UTF8))
            {
                writer.Write(Encoding.ASCII.GetBytes("PMXSCN1\0"));
                writer.Write(parts.Length + previewActorCount);
                foreach (var scenePart in sceneParts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var part = scenePart.Part;
                    byte[] name = Encoding.UTF8.GetBytes(part.Name);
                    writer.Write(name.Length); writer.Write(name);
                    writer.Write(unchecked((int)0xff80b0d0));
                    writer.Write(new byte[] { 1, 0, 0, 0 }); // pickable, no backface culling/smoothing, section capable
                    writer.Write(.2); writer.Write(.8); writer.Write(1f); writer.Write(3f);
                    writer.Write(scenePart.Nodes.Length); writer.Write(part.Visualization.Cells.Sum(TriangleCount));
                    var indices = new Dictionary<int, int>();
                    foreach (int id in scenePart.Nodes)
                    {
                        if ((indices.Count & 8191) == 0) cancellationToken.ThrowIfCancellationRequested();
                        indices.Add(id, indices.Count);
                        var node = sceneMesh.Nodes[id];
                        double[] position = { node.X, node.Y, node.Z };
                        if (selected?.Displacements != null)
                            for (int axis = 0; axis < 3; axis++) position[axis] += deformationScale * selected.Displacements[id][axis];
                        if (position.Any(v => !double.IsFinite(v))) throw new InvalidDataException("Deformed coordinates are not finite.");
                        writer.Write(id); foreach (double coordinate in position) writer.Write(coordinate);
                        writer.Write(scalars == null ? float.NaN : scalars[id]);
                    }
                    for (int i = 0; i < part.Visualization.Cells.Length; i++)
                    {
                        if ((i & 8191) == 0) cancellationToken.ThrowIfCancellationRequested();
                        var face = part.Visualization.Cells[i];
                        foreach (var triangle in Triangles(face))
                        {
                            writer.Write(part.Visualization.CellIds[i]);
                            writer.Write(indices[triangle[0]]); writer.Write(indices[triangle[1]]);
                            writer.Write(indices[triangle[2]]);
                        }
                    }
                }
                if (normalizedFaces.Length > 0)
                {
                    byte[] name = Encoding.UTF8.GetBytes("Selected faces");
                    writer.Write(name.Length); writer.Write(name);
                    writer.Write(unchecked((int)0xb0ff9d00));
                    writer.Write(new byte[] { 0, 0, 0, 0 });
                    writer.Write(1d); writer.Write(0d); writer.Write(2f); writer.Write(3f);
                    writer.Write(previewFaceNodes.Length); writer.Write(checked((int)previewFaceTriangles));
                    var indices = new Dictionary<int, int>();
                    foreach (int id in previewFaceNodes)
                    {
                        indices.Add(id, indices.Count);
                        var node = model!.Mesh.Nodes[id];
                        writer.Write(id); writer.Write(node.X); writer.Write(node.Y); writer.Write(node.Z); writer.Write(float.NaN);
                    }
                    foreach (var face in previewFaces)
                        foreach (var triangle in Triangles(face.Nodes))
                        {
                            writer.Write(face.ElementId);
                            writer.Write(indices[triangle[0]]); writer.Write(indices[triangle[1]]); writer.Write(indices[triangle[2]]);
                        }
                }
                if (markers.Length > 0)
                {
                    var nodes = model!.Mesh.Nodes.Values.ToArray();
                    double span = Math.Max(nodes.Max(n => n.X) - nodes.Min(n => n.X),
                        Math.Max(nodes.Max(n => n.Y) - nodes.Min(n => n.Y), nodes.Max(n => n.Z) - nodes.Min(n => n.Z)));
                    double radius = span > 0 ? span * .015 : .015;
                    byte[] name = Encoding.UTF8.GetBytes("Selected nodes");
                    writer.Write(name.Length); writer.Write(name);
                    writer.Write(unchecked((int)0xffff3080));
                    writer.Write(new byte[] { 0, 0, 0, 0 });
                    writer.Write(1d); writer.Write(0d); writer.Write(1f); writer.Write(3f);
                    writer.Write(markers.Length * 6); writer.Write(markers.Length * 8);
                    foreach (int id in markers)
                    {
                        var node = model!.Mesh.Nodes[id];
                        for (int v = 0; v < 6; v++)
                        {
                            writer.Write(id);
                            writer.Write(node.X + (v < 2 ? (v == 0 ? radius : -radius) : 0));
                            writer.Write(node.Y + (v is 2 or 3 ? (v == 2 ? radius : -radius) : 0));
                            writer.Write(node.Z + (v > 3 ? (v == 4 ? radius : -radius) : 0));
                            writer.Write(float.NaN);
                        }
                    }
                    int[][] faces = { new[] { 0, 2, 4 }, new[] { 2, 1, 4 }, new[] { 1, 3, 4 }, new[] { 3, 0, 4 },
                        new[] { 2, 0, 5 }, new[] { 1, 2, 5 }, new[] { 3, 1, 5 }, new[] { 0, 3, 5 } };
                    for (int i = 0; i < markers.Length; i++)
                        foreach (var face in faces)
                        {
                            writer.Write(-1); foreach (int v in face) writer.Write(i * 6 + v);
                        }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination); // never overwrite an existing export
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return selected?.Units;
    }

    static int TriangleCount(int[] face) => face.Length switch
    {
        3 => 1,
        4 => 2,
        6 => 4,
        8 => 6,
        _ => throw new NotSupportedException("Unsupported exterior face.")
    };

    static IEnumerable<int[]> Triangles(int[] face)
    {
        if (face.Length == 3) yield return [face[0], face[1], face[2]];
        else if (face.Length == 4)
        {
            yield return [face[0], face[1], face[2]];
            yield return [face[0], face[2], face[3]];
        }
        else if (face.Length == 6)
        {
            yield return [face[0], face[3], face[5]];
            yield return [face[3], face[1], face[4]];
            yield return [face[5], face[4], face[2]];
            yield return [face[3], face[4], face[5]];
        }
        else if (face.Length == 8)
        {
            int[] perimeter = [face[0], face[4], face[1], face[5], face[2], face[6], face[3], face[7]];
            for (int i = 1; i < perimeter.Length - 1; i++) yield return [perimeter[0], perimeter[i], perimeter[i + 1]];
        }
        else throw new NotSupportedException("Unsupported exterior face.");
    }
}
