using PrePoMax.Render.Contracts;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Server;

internal sealed class SelectionSpatialIndex
{
    private const int MaximumResults = 100_000;
    private readonly IndexedActor[] actors;

    public SelectionSpatialIndex(SceneManifest manifest, IReadOnlyList<SelectionActorTopology> topology)
    {
        var actorIds = manifest.Actors.ToDictionary(actor => actor.Name, actor => actor.ActorId, StringComparer.Ordinal);
        actors = topology.Where(actor => actorIds.ContainsKey(actor.Name))
            .Select(actor => new IndexedActor(actorIds[actor.Name], actor)).ToArray();
    }

    private SelectionSpatialIndex(IndexedActor[] actors)
    {
        this.actors = actors;
    }

    public SelectionSpatialIndex Refit(SceneManifest manifest, IReadOnlyList<SelectionActorTopology> topology)
    {
        var actorIds = manifest.Actors.ToDictionary(actor => actor.Name, actor => actor.ActorId, StringComparer.Ordinal);
        var coordinates = topology.ToDictionary(actor => actor.Name, actor => actor.Coordinates, StringComparer.Ordinal);
        if (actors.Any(actor => !actorIds.ContainsKey(actor.Name) || !coordinates.ContainsKey(actor.Name)))
            return new SelectionSpatialIndex(manifest, topology);
        return new SelectionSpatialIndex(actors
            .Select(actor => actor.Refit(actorIds[actor.Name], coordinates[actor.Name])).ToArray());
    }

    public void RefitCoordinates(IReadOnlyList<SelectionActorTopology> topology)
    {
        var coordinates = topology.ToDictionary(actor => actor.Name, actor => actor.Coordinates, StringComparer.Ordinal);
        foreach (var actor in actors)
        {
            if (!coordinates.TryGetValue(actor.Name, out var values) || values.Count != actor.CoordinateCount)
                throw new InvalidOperationException($"Selection actor '{actor.Name}' changed during BVH refit.");
        }
        foreach (var actor in actors) actor.RefitCoordinates(coordinates[actor.Name]);
    }

    public bool IsAvailable => actors.Length > 0;

    public FrustumResolutionResult Query(FrustumResolveRequest request, SceneManifest manifest)
    {
        if (request.Corners is null || request.Corners.Count != 8 ||
            request.Corners.Any(corner => corner is null || corner.Count != 3 || corner.Any(value => !double.IsFinite(value))) ||
            request.CameraDirection is null || request.CameraDirection.Count != 3 ||
            request.CameraDirection.Any(value => !double.IsFinite(value)))
            return FrustumResolutionResult.Invalid("The frustum corners or camera direction are malformed.");
        if (!TryCreatePlanes(request.Corners, out var planes))
            return FrustumResolutionResult.Invalid("The frustum is degenerate.");
        if (!SelectionSectionPlaneFilter.TryCreate(request.SectionPlane, out var sectionHalfSpace))
            return FrustumResolutionResult.Invalid("The section selection plane is malformed.");
        var cellPlanes = request.SectionPlane is null
            ? planes
            : [.. planes, new Plane(sectionHalfSpace.X, sectionHalfSpace.Y, sectionHalfSpace.Z,
                sectionHalfSpace.Offset)];

        var visible = manifest.Actors.Where(actor => actor.Visible && actor.Pickable)
            .Select(actor => actor.ActorId).ToHashSet(StringComparer.Ordinal);
        var nodeIds = new HashSet<int>();
        var elementIds = new HashSet<int>();
        var actorNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var actor in actors)
        {
            if (!visible.Contains(actor.ActorId)) continue;
            if (!actor.LegacyOverallBoundsIntersect(planes)) continue;
            var actorSelected = false;
            for (var point = 0; point < actor.SourceNodeIds.Length; point++)
            {
                if (!ContainsPoint(planes, actor.Coordinates, point)) continue;
                if (request.SectionPlane is not null)
                {
                    var offset = point * 3;
                    if (!sectionHalfSpace.Contains(actor.Coordinates[offset], actor.Coordinates[offset + 1],
                            actor.Coordinates[offset + 2])) continue;
                }
                nodeIds.Add(actor.SourceNodeIds[point]);
                actorSelected = true;
                if (nodeIds.Count > MaximumResults)
                    return FrustumResolutionResult.Invalid("The frustum selected more than 100000 nodes.");
            }
            foreach (var cellIndex in actor.QueryCells(cellPlanes))
            {
                var cell = actor.Cells[cellIndex];
                if (!IntersectsCell(cellPlanes, actor.Coordinates, cell)) continue;
                elementIds.Add(cell.SourceElementId);
                actorSelected = true;
                if (elementIds.Count > MaximumResults)
                    return FrustumResolutionResult.Invalid("The frustum selected more than 100000 elements.");
            }
            if (actorSelected) actorNames.Add(actor.Name);
        }
        return FrustumResolutionResult.Success(new ResolvedFrustum(nodeIds.ToArray(), elementIds.ToArray(),
            actorNames.ToArray(), request.CameraDirection, request.CompletelyInside, true));
    }

    private static bool TryCreatePlanes(IReadOnlyList<IReadOnlyList<double>> corners, out Plane[] planes)
    {
        var center = new double[3];
        foreach (var corner in corners) for (var axis = 0; axis < 3; axis++) center[axis] += corner[axis] / 8;
        var faces = new[] { new[] { 0, 4, 7 }, new[] { 1, 2, 6 }, new[] { 0, 1, 5 },
            new[] { 3, 7, 6 }, new[] { 0, 3, 2 }, new[] { 4, 5, 6 } };
        planes = new Plane[6];
        for (var index = 0; index < faces.Length; index++)
        {
            var a = corners[faces[index][0]]; var b = corners[faces[index][1]]; var c = corners[faces[index][2]];
            var ab = new[] { b[0] - a[0], b[1] - a[1], b[2] - a[2] };
            var ac = new[] { c[0] - a[0], c[1] - a[1], c[2] - a[2] };
            var normal = new[] { ab[1] * ac[2] - ab[2] * ac[1], ab[2] * ac[0] - ab[0] * ac[2], ab[0] * ac[1] - ab[1] * ac[0] };
            var length = Math.Sqrt(normal.Sum(value => value * value));
            if (!double.IsFinite(length) || length <= 1e-12) return false;
            for (var axis = 0; axis < 3; axis++) normal[axis] /= length;
            var offset = -(normal[0] * a[0] + normal[1] * a[1] + normal[2] * a[2]);
            if (normal[0] * center[0] + normal[1] * center[1] + normal[2] * center[2] + offset < 0)
            { for (var axis = 0; axis < 3; axis++) normal[axis] = -normal[axis]; offset = -offset; }
            planes[index] = new Plane(normal[0], normal[1], normal[2], offset);
        }
        return true;
    }

    internal static bool ContainsPoint(Plane[] planes, IReadOnlyList<double> coordinates, int point)
    {
        var offset = point * 3;
        // vtkExtractSelectedFrustum POINT mode uses a strict half-space test. A point exactly on a
        // selection plane is therefore not an inside point, even though a cell touching that plane
        // can still be an intersecting cell.
        return planes.All(plane => plane.Evaluate(coordinates[offset], coordinates[offset + 1], coordinates[offset + 2]) > 0);
    }

    internal static bool IntersectsCell(Plane[] planes, IReadOnlyList<double> coordinates, SelectionCellTopology cell)
    {
        var points = cell.LocalPointIndices;
        foreach (var plane in planes)
        {
            var anyInside = false;
            foreach (var point in points)
            {
                var offset = point * 3;
                if (plane.Evaluate(coordinates[offset], coordinates[offset + 1], coordinates[offset + 2]) >= 0)
                { anyInside = true; break; }
            }
            if (!anyInside) return false;
        }

        // VTK first accepts a cell whose entire axis-aligned bound is inside. This also preserves
        // its inclusive CELL behavior for zero-size cells and cells lying on a frustum plane.
        var bounds = CellBounds(coordinates, points);
        if (BoundsInside(planes, bounds)) return true;

        // vtkQuadraticEdge reports neither faces nor edges to vtkExtractSelectedFrustum 5.8. Its
        // legacy fallback accepts it when any of its three interpolation points is strictly inside.
        if (cell.VtkCellType is 1 or 21)
            return points.Any(point => ContainsPoint(planes, coordinates, point));

        var faces = LegacyClipFaces(cell.VtkCellType);
        if (faces is null) return false; // Upload validation prevents this branch for protocol cells.
        foreach (var face in faces)
        {
            var polygon = new List<Point3>(face.Length);
            foreach (var facePoint in face)
            {
                var offset = points[facePoint] * 3;
                polygon.Add(new Point3(coordinates[offset], coordinates[offset + 1], coordinates[offset + 2]));
            }
            if (ClipPolygon(polygon, planes)) return true;
        }
        return false;
    }

    internal static bool IntersectsBoundsAsVoxel(Plane[] planes, double[] bounds)
    {
        var coordinates = new[]
        {
            bounds[0], bounds[2], bounds[4], bounds[1], bounds[2], bounds[4],
            bounds[0], bounds[3], bounds[4], bounds[1], bounds[3], bounds[4],
            bounds[0], bounds[2], bounds[5], bounds[1], bounds[2], bounds[5],
            bounds[0], bounds[3], bounds[5], bounds[1], bounds[3], bounds[5],
        };
        return IntersectsCell(planes, coordinates,
            new SelectionCellTopology(0, 11, Enumerable.Range(0, 8).ToArray()));
    }

    private static double[] CellBounds(IReadOnlyList<double> coordinates, IReadOnlyList<int> points)
    {
        var bounds = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue,
            double.MaxValue, double.MinValue };
        foreach (var point in points)
            for (var axis = 0; axis < 3; axis++)
            {
                var value = coordinates[point * 3 + axis];
                bounds[axis * 2] = Math.Min(bounds[axis * 2], value);
                bounds[axis * 2 + 1] = Math.Max(bounds[axis * 2 + 1], value);
            }
        return bounds;
    }

    private static bool BoundsInside(Plane[] planes, double[] bounds)
    {
        foreach (var plane in planes)
            for (var corner = 0; corner < 8; corner++)
                if (plane.Evaluate(bounds[(corner & 1) == 0 ? 0 : 1],
                        bounds[(corner & 2) == 0 ? 2 : 3], bounds[(corner & 4) == 0 ? 4 : 5]) < 0)
                    return false;
        return true;
    }

    private static bool ClipPolygon(List<Point3> polygon, Plane[] planes)
    {
        foreach (var plane in planes)
        {
            if (polygon.Count == 0) return false;
            var clipped = new List<Point3>(polygon.Count + 2);
            var previous = polygon[^1];
            var previousValue = plane.Evaluate(previous.X, previous.Y, previous.Z);
            foreach (var current in polygon)
            {
                var currentValue = plane.Evaluate(current.X, current.Y, current.Z);
                var previousInside = previousValue >= 0;
                var currentInside = currentValue >= 0;
                if (previousInside != currentInside)
                {
                    var fraction = previousValue / (previousValue - currentValue);
                    clipped.Add(new Point3(previous.X + fraction * (current.X - previous.X),
                        previous.Y + fraction * (current.Y - previous.Y),
                        previous.Z + fraction * (current.Z - previous.Z)));
                }
                if (currentInside) clipped.Add(current);
                previous = current;
                previousValue = currentValue;
            }
            polygon = clipped;
        }
        return polygon.Count > 0;
    }

    private static int[][]? CellFaces(int vtkCellType) => vtkCellType switch
    {
        3 => [[0, 1]],
        5 or 22 => [[0, 1, 2]],
        9 or 23 => [[0, 1, 2, 3]],
        10 or 24 => TetraFaces,
        11 => VoxelFaces,
        12 or 25 or 29 => HexahedronFaces,
        13 or 26 => WedgeFaces,
        14 or 27 => PyramidFaces,
        _ => null,
    };

    private static int[][]? LegacyClipFaces(int vtkCellType)
    {
        var faces = CellFaces(vtkCellType);
        if (faces is null || vtkCellType is not (24 or 25 or 26 or 27 or 29)) return faces;

        // vtkExtractSelectedFrustum 5.8 reconstructs quadratic face polygons through its default
        // branch. After reading edge 0 from the face it accidentally reads subsequent edges from
        // the parent cell. Reproduce that observable behavior for compatibility with PrePoMax's
        // legacy CELL selection, while leaving linear and quadratic 2-D cells geometrically intact.
        if (vtkCellType == 26) faces = QuadraticWedgeFaces;
        var parentEdgeEnds = vtkCellType == 26 ? new[] { 1, 2, 0 } : new[] { 1, 2, 3 };
        return faces.Select(face => face.Length == 3
            ? new[] { face[0], face[1], parentEdgeEnds[1] }
            : new[] { face[0], face[1], parentEdgeEnds[1], parentEdgeEnds[2] }).ToArray();
    }

    private static readonly int[][] TetraFaces =
        [[0, 1, 3], [1, 2, 3], [2, 0, 3], [0, 2, 1]];
    private static readonly int[][] VoxelFaces =
        [[0, 4, 6, 2], [1, 3, 7, 5], [0, 1, 5, 4], [2, 6, 7, 3], [0, 2, 3, 1], [4, 5, 7, 6]];
    private static readonly int[][] HexahedronFaces =
        [[0, 4, 7, 3], [1, 2, 6, 5], [0, 1, 5, 4], [3, 7, 6, 2], [0, 3, 2, 1], [4, 5, 6, 7]];
    private static readonly int[][] WedgeFaces =
        [[0, 2, 1], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]];
    private static readonly int[][] QuadraticWedgeFaces =
        [[0, 1, 2], [3, 5, 4], [0, 3, 4, 1], [1, 4, 5, 2], [2, 5, 3, 0]];
    private static readonly int[][] PyramidFaces =
        [[0, 3, 2, 1], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]];

    private readonly record struct Point3(double X, double Y, double Z);

    internal readonly record struct Plane(double X, double Y, double Z, double Offset)
    {
        public double Evaluate(double x, double y, double z) => X * x + Y * y + Z * z + Offset;
        public bool Intersects(double[] bounds)
        {
            var x = X >= 0 ? bounds[1] : bounds[0]; var y = Y >= 0 ? bounds[3] : bounds[2];
            var z = Z >= 0 ? bounds[5] : bounds[4];
            return Evaluate(x, y, z) >= 0;
        }
    }

    private sealed class IndexedActor
    {
        private readonly BvhNode root;
        private double[] actorBounds;
        public IndexedActor(string actorId, SelectionActorTopology topology)
        {
            ActorId = actorId; Name = topology.Name; SourceNodeIds = topology.SourceNodeIds.ToArray();
            Coordinates = CoordinateSource.Create(topology.Coordinates);
            Cells = topology.Cells.ToArray();
            var bounds = Enumerable.Range(0, Cells.Length).Select(CellBounds).ToArray();
            root = BvhNode.Build(Enumerable.Range(0, Cells.Length).ToArray(), bounds);
            actorBounds = AllPointBounds(Coordinates);
        }
        private IndexedActor(string actorId, string name, int[] sourceNodeIds, CoordinateSource coordinates,
                             SelectionCellTopology[] cells, BvhNode root, double[] actorBounds)
        {
            ActorId = actorId; Name = name; SourceNodeIds = sourceNodeIds;
            Coordinates = coordinates; Cells = cells; this.root = root; this.actorBounds = actorBounds;
        }
        public string ActorId { get; } public string Name { get; } public int[] SourceNodeIds { get; }
        public CoordinateSource Coordinates { get; private set; } public SelectionCellTopology[] Cells { get; }
        public int CoordinateCount => Coordinates.Count;
        public IEnumerable<int> QueryCells(Plane[] planes) => root.Query(planes);
        public bool LegacyOverallBoundsIntersect(Plane[] planes) => IntersectsBoundsAsVoxel(planes, actorBounds);
        public IndexedActor Refit(string actorId, IReadOnlyList<double> coordinates)
        {
            if (coordinates.Count != Coordinates.Count)
                throw new InvalidOperationException($"Selection actor '{Name}' changed coordinate count during BVH refit.");
            var updatedCoordinates = CoordinateSource.Create(coordinates);
            var updatedBounds = AllPointBounds(updatedCoordinates);
            var updated = new IndexedActor(actorId, Name, SourceNodeIds, updatedCoordinates, Cells, root, updatedBounds);
            return new IndexedActor(actorId, Name, SourceNodeIds, updatedCoordinates, Cells,
                root.Refit(updated.ExpandCellBounds), updatedBounds);
        }
        public void RefitCoordinates(IReadOnlyList<double> coordinates)
        {
            if (coordinates.Count != Coordinates.Count)
                throw new InvalidOperationException($"Selection actor '{Name}' changed coordinate count during BVH refit.");
            Coordinates = CoordinateSource.Create(coordinates);
            actorBounds = AllPointBounds(Coordinates);
            root.RefitInPlace(ExpandCellBounds);
        }
        private static double[] AllPointBounds(CoordinateSource coordinates)
        {
            var result = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue,
                double.MaxValue, double.MinValue };
            for (var offset = 0; offset < coordinates.Count; offset += 3)
                for (var axis = 0; axis < 3; axis++)
                {
                    var value = coordinates[offset + axis];
                    result[axis * 2] = Math.Min(result[axis * 2], value);
                    result[axis * 2 + 1] = Math.Max(result[axis * 2 + 1], value);
                }
            return result;
        }
        private double[] CellBounds(int cell)
        {
            var result = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue };
            Coordinates.ExpandBounds(Cells[cell].LocalPointIndices, result);
            return result;
        }
        private void ExpandCellBounds(int cell, double[] result)
        {
            Coordinates.ExpandBounds(Cells[cell].LocalPointIndices, result);
        }
    }

    private sealed class CoordinateSource : IReadOnlyList<double>
    {
        private readonly double[]? absolute;
        private readonly OriginRelativeCoordinateList? compact;

        private CoordinateSource(double[] absolute) => this.absolute = absolute;
        private CoordinateSource(OriginRelativeCoordinateList compact) => this.compact = compact;

        public static CoordinateSource Create(IReadOnlyList<double> coordinates) => coordinates switch
        {
            double[] values => new CoordinateSource(values),
            OriginRelativeCoordinateList values => new CoordinateSource(values),
            _ => new CoordinateSource(coordinates.ToArray()),
        };

        public int Count => absolute?.Length ?? compact!.Count;
        public double this[int index] => GetCoordinate(index);
        public double GetCoordinate(int index) => absolute is not null ? absolute[index] : compact!.GetCoordinate(index);
        public void ExpandBounds(IReadOnlyList<int> points, double[] bounds)
        {
            if (absolute is null)
            {
                compact!.ExpandBounds(points, bounds);
                return;
            }
            // Indexed access avoids boxing the IReadOnlyList enumerator for every BVH cell.
            for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                var point = points[pointIndex];
                for (var axis = 0; axis < 3; axis++)
                {
                    var value = absolute[point * 3 + axis];
                    bounds[axis * 2] = Math.Min(bounds[axis * 2], value);
                    bounds[axis * 2 + 1] = Math.Max(bounds[axis * 2 + 1], value);
                }
            }
        }

        public IEnumerator<double> GetEnumerator()
        {
            for (var index = 0; index < Count; index++) yield return GetCoordinate(index);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class BvhNode
    {
        private const int LeafSize = 16;
        private BvhNode(double[] bounds, int[]? cells, BvhNode? left, BvhNode? right) { Bounds = bounds; Cells = cells; Left = left; Right = right; }
        public double[] Bounds { get; } private int[]? Cells { get; } private BvhNode? Left { get; } private BvhNode? Right { get; }
        public static BvhNode Build(int[] cells, double[][] bounds)
        {
            var aggregate = Aggregate(cells, bounds);
            if (cells.Length <= LeafSize) return new BvhNode(aggregate, cells, null, null);
            var axis = Enumerable.Range(0, 3).MaxBy(value => aggregate[value * 2 + 1] - aggregate[value * 2]);
            var ordered = cells.OrderBy(cell => bounds[cell][axis * 2] / 2 + bounds[cell][axis * 2 + 1] / 2).ToArray();
            var middle = ordered.Length / 2;
            return new BvhNode(aggregate, null, Build(ordered[..middle], bounds), Build(ordered[middle..], bounds));
        }
        public IEnumerable<int> Query(Plane[] planes)
        {
            if (planes.Any(plane => !plane.Intersects(Bounds))) yield break;
            if (Cells is not null) { foreach (var cell in Cells) yield return cell; yield break; }
            foreach (var cell in Left!.Query(planes)) yield return cell;
            foreach (var cell in Right!.Query(planes)) yield return cell;
        }
        public BvhNode Refit(Action<int, double[]> expandCellBounds)
        {
            if (Cells is not null)
            {
                var leafBounds = EmptyBounds();
                foreach (var cell in Cells) expandCellBounds(cell, leafBounds);
                return new BvhNode(leafBounds, Cells, null, null);
            }
            var left = Left!.Refit(expandCellBounds);
            var right = Right!.Refit(expandCellBounds);
            var bounds = EmptyBounds();
            Merge(bounds, left.Bounds);
            Merge(bounds, right.Bounds);
            return new BvhNode(bounds, null, left, right);
        }
        public void RefitInPlace(Action<int, double[]> expandCellBounds)
        {
            Reset(Bounds);
            if (Cells is not null)
            {
                foreach (var cell in Cells) expandCellBounds(cell, Bounds);
                return;
            }
            Left!.RefitInPlace(expandCellBounds);
            Right!.RefitInPlace(expandCellBounds);
            Merge(Bounds, Left.Bounds);
            Merge(Bounds, Right.Bounds);
        }
        private static double[] Aggregate(int[] cells, double[][] bounds)
        {
            var result = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue };
            foreach (var cell in cells) for (var axis = 0; axis < 3; axis++)
            { result[axis * 2] = Math.Min(result[axis * 2], bounds[cell][axis * 2]); result[axis * 2 + 1] = Math.Max(result[axis * 2 + 1], bounds[cell][axis * 2 + 1]); }
            return result;
        }
        private static double[] EmptyBounds() =>
            [double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue];
        private static void Reset(double[] bounds)
        {
            bounds[0] = bounds[2] = bounds[4] = double.MaxValue;
            bounds[1] = bounds[3] = bounds[5] = double.MinValue;
        }
        private static void Merge(double[] target, double[] source)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                target[axis * 2] = Math.Min(target[axis * 2], source[axis * 2]);
                target[axis * 2 + 1] = Math.Max(target[axis * 2 + 1], source[axis * 2 + 1]);
            }
        }
    }
}

internal sealed record FrustumResolutionResult(bool Succeeded, ResolvedFrustum? Frustum, string? Error)
{
    public static FrustumResolutionResult Success(ResolvedFrustum frustum) => new(true, frustum, null);
    public static FrustumResolutionResult Invalid(string error) => new(false, null, error);
}
