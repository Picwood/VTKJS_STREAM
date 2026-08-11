using PrePoMax.Render.Contracts;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Server;

internal sealed record SectionActorOffset(string ActorName, IReadOnlyList<double> Offset);

internal sealed record SectionResolveRequest(
    IReadOnlyList<double> Point,
    IReadOnlyList<double> Normal,
    bool LightenColors,
    IReadOnlyList<double> SectionColor,
    IReadOnlyList<SectionActorOffset>? ActorOffsets = null);

internal sealed record SectionGeometryActor(
    string ActorId,
    string ParentActorId,
    string Name,
    IReadOnlyList<double> Rgba,
    float LineWidthPx,
    float[] Positions,
    uint[] TriangleConnectivity,
    uint[] LineConnectivity,
    float[] PointScalars,
    int[] SourceElementIds,
    IReadOnlyList<double> Bounds);

internal sealed record SectionGeometryResult(IReadOnlyList<SectionGeometryActor> Actors, int SkippedCells);

internal static class SectionGeometryBuilder
{
    internal const int MaximumActors = 512;
    internal const int MaximumPoints = 500_000;
    internal const int MaximumTriangles = 500_000;

    public static SectionGeometryResult Build(
        SceneManifest manifest,
        IReadOnlyList<SelectionActorTopology> topologyActors,
        SectionResolveRequest request)
    {
        Validate(request);
        var normalLength = Math.Sqrt(request.Normal.Sum(component => component * component));
        var normal = request.Normal.Select(component => component / normalLength).ToArray();
        var offsets = (request.ActorOffsets ?? [])
            .ToDictionary(item => item.ActorName, item => item.Offset, StringComparer.Ordinal);
        var actorsByName = manifest.Actors.Where(actor => actor.Layer == "base")
            .ToDictionary(actor => actor.Name, StringComparer.Ordinal);
        var output = new List<SectionGeometryActor>();
        var skippedCells = 0;
        var totalPoints = 0;
        var totalTriangles = 0;

        foreach (var topology in topologyActors)
        {
            if (!topology.SectionViewPossible || !actorsByName.TryGetValue(topology.Name, out var parent))
            {
                skippedCells += topology.Cells.Count;
                continue;
            }
            if (!parent.Visible) continue;
            var offset = offsets.TryGetValue(topology.Name, out var requestedOffset) ? requestedOffset : Zero;
            var built = BuildActor(manifest.WorldOrigin, topology, parent, request, normal, offset, ref skippedCells);
            if (built is null) continue;
            totalPoints = checked(totalPoints + built.Positions.Length / 3);
            totalTriangles = checked(totalTriangles + built.TriangleConnectivity.Length / 3);
            if (totalPoints > MaximumPoints || totalTriangles > MaximumTriangles)
                throw new SectionGeometryException("section-capacity-exceeded",
                    "The derived section exceeds the 500,000-point or 500,000-triangle bound.");
            output.Add(built);
            if (output.Count > MaximumActors)
                throw new SectionGeometryException("section-capacity-exceeded", "The derived section exceeds the 512-actor bound.");
        }
        return new SectionGeometryResult(output, skippedCells);
    }

    private static SectionGeometryActor? BuildActor(
        IReadOnlyList<double> origin,
        SelectionActorTopology topology,
        RenderActor parent,
        SectionResolveRequest request,
        IReadOnlyList<double> normal,
        IReadOnlyList<double> offset,
        ref int skippedCells)
    {
        if (topology.Coordinates.Count != topology.SourceNodeIds.Count * 3 ||
            topology.Scalars is not null && topology.Scalars.Count != topology.SourceNodeIds.Count)
        {
            skippedCells += topology.Cells.Count;
            return null;
        }
        var positions = new List<float>();
        var scalars = new List<float>();
        var triangles = new List<uint>();
        var lines = new List<uint>();
        var elementIds = new List<int>();
        var vertices = new Dictionary<VertexKey, uint>();
        var positionVertices = new Dictionary<PositionKey, uint>();
        var generatedFaceIdentities = new Dictionary<FaceCenterKey, long>();
        long nextGeneratedIdentity = -1;
        var triangleSet = new HashSet<(uint, uint, uint)>();
        var lineSet = new HashSet<(uint, uint)>();
        var scale = BoundsDiagonal(parent.Bounds);
        var tolerance = Math.Max(1e-12, scale * 1e-10);
        var basisU = Math.Abs(normal[0]) < 0.8 ? Normalize(Cross(normal, new[] { 1d, 0d, 0d }))
                                              : Normalize(Cross(normal, new[] { 0d, 1d, 0d }));
        var basisV = Cross(normal, basisU);

        foreach (var cell in topology.Cells)
        {
            var chains = EdgeChains(cell.VtkCellType);
            if (chains is null)
            {
                skippedCells++;
                continue;
            }
            var polygon = new HashSet<uint>();
            foreach (var chain in chains)
            {
                for (var segment = 0; segment + 1 < chain.Length; segment++)
                {
                    var first = cell.LocalPointIndices[chain[segment]];
                    var second = cell.LocalPointIndices[chain[segment + 1]];
                    AddSegmentIntersections(polygon, first, second);
                }
            }

            var isVolume = IsVolume(cell.VtkCellType);
            if (cell.VtkCellType == 10 && polygon.Count >= 3)
            {
                var samples = cell.LocalPointIndices.Select(OriginalSample).ToArray();
                var triangleStart = triangles.Count;
                AddMarchingSampleTetra(samples, LinearTetraCell, cell.SourceElementId);
                AddTriangleBoundaryLines(triangleStart);
            }
            else if (cell.VtkCellType is 11 or 12 && polygon.Count >= 3)
            {
                var samples = cell.LocalPointIndices.Select(OriginalSample).ToArray();
                var triangleStart = triangles.Count;
                AddMarchingHex(samples, cell.VtkCellType == 11 ? VoxelToHexCell : LinearHexCell,
                    cell.SourceElementId);
                AddTriangleBoundaryLines(triangleStart);
            }
            else if (cell.VtkCellType == 13 && polygon.Count >= 3)
            {
                var samples = cell.LocalPointIndices.Select(OriginalSample).ToArray();
                var triangleStart = triangles.Count;
                AddMarchingWedge(samples, LinearWedgeCell, cell.SourceElementId);
                AddTriangleBoundaryLines(triangleStart);
            }
            else if (cell.VtkCellType == 14 && polygon.Count >= 3)
            {
                var samples = cell.LocalPointIndices.Select(OriginalSample).ToArray();
                var triangleStart = triangles.Count;
                AddMarchingPyramid(samples, LinearPyramidCell, cell.SourceElementId);
                AddTriangleBoundaryLines(triangleStart);
            }
            else if (cell.VtkCellType == 24 && polygon.Count >= 3)
            {
                // VTK 5.8 contours vtkQuadraticTetra by decomposing it into these exact eight
                // linear tetrahedra. Keeping the subdivision (including its internal cut vertices)
                // is required for ActiViz parity; contouring only the twelve outer half-edges can
                // produce the same boundary but a different, and sometimes invalid, cap mesh.
                foreach (var tetra in QuadraticTetraSubcells)
                {
                    var caseIndex = 0;
                    for (var vertex = 0; vertex < 4; vertex++)
                    {
                        var point = cell.LocalPointIndices[tetra[vertex]];
                        if (SignedDistance(topology.Coordinates, point, offset, request.Point, normal) >= 0)
                            caseIndex |= 1 << vertex;
                    }
                    var contourEdges = MarchingTetraCases[caseIndex];
                    for (var triangle = 0; triangle < contourEdges.Length; triangle += 3)
                    {
                        var firstEdge = TetraEdges[contourEdges[triangle]];
                        var secondEdge = TetraEdges[contourEdges[triangle + 1]];
                        var thirdEdge = TetraEdges[contourEdges[triangle + 2]];
                        AddCapTriangle(
                            GetSegmentVertex(cell.LocalPointIndices[tetra[firstEdge[0]]],
                                cell.LocalPointIndices[tetra[firstEdge[1]]]),
                            GetSegmentVertex(cell.LocalPointIndices[tetra[secondEdge[0]]],
                                cell.LocalPointIndices[tetra[secondEdge[1]]]),
                            GetSegmentVertex(cell.LocalPointIndices[tetra[thirdEdge[0]]],
                                cell.LocalPointIndices[tetra[thirdEdge[1]]]),
                            cell.SourceElementId);
                    }
                }
                AddBoundaryLines(polygon);
            }
            else if (cell.VtkCellType == 25 && polygon.Count >= 3)
            {
                var samples = BuildQuadraticHexSamples(cell);
                var triangleStart = triangles.Count;
                foreach (var hexahedron in QuadraticHexSubcells)
                    AddMarchingHex(samples, hexahedron, cell.SourceElementId);
                AddTriangleBoundaryLines(triangleStart);
            }
            else if (cell.VtkCellType == 26 && polygon.Count >= 3)
            {
                var samples = BuildQuadraticWedgeSamples(cell);
                var triangleStart = triangles.Count;
                foreach (var wedge in QuadraticWedgeSubcells)
                    AddMarchingWedge(samples, wedge, cell.SourceElementId);
                AddTriangleBoundaryLines(triangleStart);
            }
            else if (cell.VtkCellType == 27 && polygon.Count >= 3)
            {
                var samples = BuildQuadraticPyramidSamples(cell);
                var triangleStart = triangles.Count;
                for (var subcell = 0; subcell < 6; subcell++)
                    AddMarchingPyramid(samples, QuadraticPyramidSubcells[subcell], cell.SourceElementId);
                for (var subcell = 6; subcell < QuadraticPyramidSubcells.Length; subcell++)
                    AddMarchingSampleTetra(samples, QuadraticPyramidSubcells[subcell], cell.SourceElementId);
                AddTriangleBoundaryLines(triangleStart);
            }
            else if (cell.VtkCellType == 29 && polygon.Count >= 3)
            {
                var samples = new SectionSample[27];
                for (var point = 0; point < samples.Length; point++)
                    samples[point] = OriginalSample(cell.LocalPointIndices[point]);
                var triangleStart = triangles.Count;
                foreach (var hexahedron in QuadraticHexSubcells)
                    AddMarchingHex(samples, hexahedron, cell.SourceElementId);
                AddTriangleBoundaryLines(triangleStart);
            }
            else if (isVolume && polygon.Count >= 3)
            {
                AddCapPolygon(polygon, cell.SourceElementId);
                AddBoundaryLines(polygon);
            }
            else if (!isVolume && polygon.Count >= 2)
            {
                var ordered = OrderPolygon(polygon, positions, origin, basisU, basisV);
                for (var index = 0; index + 1 < ordered.Count; index++) AddLine(ordered[index], ordered[index + 1]);
            }
        }

        if (triangles.Count == 0 && lines.Count == 0) return null;
        var rgba = ResolveColor(parent.Material.FrontRgba, request);
        var bounds = CalculateBounds(positions, origin);
        return new SectionGeometryActor(Guid.NewGuid().ToString("D"), parent.ActorId, topology.Name,
            rgba, (float)Math.Max(1, parent.Material.LineWidthPx), positions.ToArray(), triangles.ToArray(),
            lines.ToArray(), scalars.ToArray(), elementIds.ToArray(), bounds);

        uint GetVertex(VertexKey key, int first, int second, double fraction)
        {
            return GetSampleVertex(key, OriginalSample(first), OriginalSample(second), fraction);
        }

        SectionSample OriginalSample(int point) => new(point,
            topology.Coordinates[point * 3], topology.Coordinates[point * 3 + 1],
            topology.Coordinates[point * 3 + 2], topology.Scalars?[point] ?? float.NaN);

        uint GetSampleVertex(VertexKey key, SectionSample first, SectionSample second, double fraction)
        {
            if (vertices.TryGetValue(key, out var existing)) return existing;
            var x = first.X * (1 - fraction) + second.X * fraction - origin[0];
            var y = first.Y * (1 - fraction) + second.Y * fraction - origin[1];
            var z = first.Z * (1 - fraction) + second.Z * fraction - origin[2];
            var positionKey = new PositionKey(x, y, z);
            if (positionVertices.TryGetValue(positionKey, out existing))
            {
                vertices.Add(key, existing);
                return existing;
            }
            var index = checked((uint)(positions.Count / 3));
            var convertedX = (float)x;
            var convertedY = (float)y;
            var convertedZ = (float)z;
            if (!float.IsFinite(convertedX) || !float.IsFinite(convertedY) || !float.IsFinite(convertedZ))
                throw new SectionGeometryException("section-numeric-invalid", "A derived section coordinate exceeds Float32 range.");
            positions.Add(convertedX);
            positions.Add(convertedY);
            positions.Add(convertedZ);
            scalars.Add(float.IsFinite(first.Scalar) && float.IsFinite(second.Scalar)
                ? (float)(first.Scalar * (1 - fraction) + second.Scalar * fraction)
                : float.NaN);
            vertices.Add(key, index);
            positionVertices.Add(positionKey, index);
            return index;
        }

        void AddSegmentIntersections(HashSet<uint> polygon, int first, int second)
        {
            var firstDistance = SignedDistance(topology.Coordinates, first, offset, request.Point, normal);
            var secondDistance = SignedDistance(topology.Coordinates, second, offset, request.Point, normal);
            if (Math.Abs(firstDistance) <= tolerance)
                polygon.Add(GetVertex(new VertexKey(first, first, 0), first, first, 0));
            if (Math.Abs(secondDistance) <= tolerance)
                polygon.Add(GetVertex(new VertexKey(second, second, 0), second, second, 0));
            if (firstDistance * secondDistance < -tolerance * tolerance)
            {
                var fraction = firstDistance / (firstDistance - secondDistance);
                var low = Math.Min(first, second);
                var high = Math.Max(first, second);
                var canonicalFraction = first <= second ? fraction : 1 - fraction;
                polygon.Add(GetVertex(new VertexKey(low, high, Quantize(canonicalFraction)), first, second, fraction));
            }
        }

        double SampleSignedDistance(SectionSample sample) =>
            (sample.X + offset[0] - request.Point[0]) * normal[0] +
            (sample.Y + offset[1] - request.Point[1]) * normal[1] +
            (sample.Z + offset[2] - request.Point[2]) * normal[2];

        uint GetSampleSegmentVertex(SectionSample first, SectionSample second)
        {
            var firstDistance = SampleSignedDistance(first);
            var secondDistance = SampleSignedDistance(second);
            if (Math.Abs(firstDistance) <= tolerance)
                return GetSampleVertex(new VertexKey(first.Identity, first.Identity, 0), first, first, 0);
            if (Math.Abs(secondDistance) <= tolerance)
                return GetSampleVertex(new VertexKey(second.Identity, second.Identity, 0), second, second, 0);
            var fraction = firstDistance / (firstDistance - secondDistance);
            var low = Math.Min(first.Identity, second.Identity);
            var high = Math.Max(first.Identity, second.Identity);
            var canonicalFraction = first.Identity <= second.Identity ? fraction : 1 - fraction;
            return GetSampleVertex(new VertexKey(low, high, Quantize(canonicalFraction)), first, second, fraction);
        }

        void AddMarchingHex(IReadOnlyList<SectionSample> samples, IReadOnlyList<int> hexahedron,
            int sourceElementId)
        {
            var caseIndex = 0;
            for (var vertex = 0; vertex < 8; vertex++)
                if (SampleSignedDistance(samples[hexahedron[vertex]]) >= 0) caseIndex |= 1 << vertex;
            var caseOffset = caseIndex * 16;
            for (var triangle = 0; triangle < 16 && MarchingHexCases[caseOffset + triangle] != byte.MaxValue;
                 triangle += 3)
            {
                var firstEdge = HexEdges[MarchingHexCases[caseOffset + triangle]];
                var secondEdge = HexEdges[MarchingHexCases[caseOffset + triangle + 1]];
                var thirdEdge = HexEdges[MarchingHexCases[caseOffset + triangle + 2]];
                AddCapTriangle(
                    GetSampleSegmentVertex(samples[hexahedron[firstEdge[0]]], samples[hexahedron[firstEdge[1]]]),
                    GetSampleSegmentVertex(samples[hexahedron[secondEdge[0]]], samples[hexahedron[secondEdge[1]]]),
                    GetSampleSegmentVertex(samples[hexahedron[thirdEdge[0]]], samples[hexahedron[thirdEdge[1]]]),
                    sourceElementId);
            }
        }

        void AddMarchingWedge(IReadOnlyList<SectionSample> samples, IReadOnlyList<int> wedge,
            int sourceElementId)
        {
            var caseIndex = 0;
            for (var vertex = 0; vertex < 6; vertex++)
                if (SampleSignedDistance(samples[wedge[vertex]]) >= 0) caseIndex |= 1 << vertex;
            var caseOffset = caseIndex * 13;
            for (var triangle = 0; triangle < 13 && MarchingWedgeCases[caseOffset + triangle] != byte.MaxValue;
                 triangle += 3)
            {
                var firstEdge = WedgeEdges[MarchingWedgeCases[caseOffset + triangle]];
                var secondEdge = WedgeEdges[MarchingWedgeCases[caseOffset + triangle + 1]];
                var thirdEdge = WedgeEdges[MarchingWedgeCases[caseOffset + triangle + 2]];
                AddCapTriangle(
                    GetSampleSegmentVertex(samples[wedge[firstEdge[0]]], samples[wedge[firstEdge[1]]]),
                    GetSampleSegmentVertex(samples[wedge[secondEdge[0]]], samples[wedge[secondEdge[1]]]),
                    GetSampleSegmentVertex(samples[wedge[thirdEdge[0]]], samples[wedge[thirdEdge[1]]]),
                    sourceElementId);
            }
        }

        void AddMarchingPyramid(IReadOnlyList<SectionSample> samples, IReadOnlyList<int> pyramid,
            int sourceElementId)
        {
            var caseIndex = 0;
            for (var vertex = 0; vertex < 5; vertex++)
                if (SampleSignedDistance(samples[pyramid[vertex]]) >= 0) caseIndex |= 1 << vertex;
            var caseOffset = caseIndex * 13;
            for (var triangle = 0; triangle < 13 && MarchingPyramidCases[caseOffset + triangle] != byte.MaxValue;
                 triangle += 3)
            {
                var firstEdge = PyramidEdges[MarchingPyramidCases[caseOffset + triangle]];
                var secondEdge = PyramidEdges[MarchingPyramidCases[caseOffset + triangle + 1]];
                var thirdEdge = PyramidEdges[MarchingPyramidCases[caseOffset + triangle + 2]];
                AddCapTriangle(
                    GetSampleSegmentVertex(samples[pyramid[firstEdge[0]]], samples[pyramid[firstEdge[1]]]),
                    GetSampleSegmentVertex(samples[pyramid[secondEdge[0]]], samples[pyramid[secondEdge[1]]]),
                    GetSampleSegmentVertex(samples[pyramid[thirdEdge[0]]], samples[pyramid[thirdEdge[1]]]),
                    sourceElementId);
            }
        }

        void AddMarchingSampleTetra(IReadOnlyList<SectionSample> samples, IReadOnlyList<int> tetra,
            int sourceElementId)
        {
            var caseIndex = 0;
            for (var vertex = 0; vertex < 4; vertex++)
                if (SampleSignedDistance(samples[tetra[vertex]]) >= 0) caseIndex |= 1 << vertex;
            var contourEdges = MarchingTetraCases[caseIndex];
            for (var triangle = 0; triangle < contourEdges.Length; triangle += 3)
            {
                var firstEdge = TetraEdges[contourEdges[triangle]];
                var secondEdge = TetraEdges[contourEdges[triangle + 1]];
                var thirdEdge = TetraEdges[contourEdges[triangle + 2]];
                AddCapTriangle(
                    GetSampleSegmentVertex(samples[tetra[firstEdge[0]]], samples[tetra[firstEdge[1]]]),
                    GetSampleSegmentVertex(samples[tetra[secondEdge[0]]], samples[tetra[secondEdge[1]]]),
                    GetSampleSegmentVertex(samples[tetra[thirdEdge[0]]], samples[tetra[thirdEdge[1]]]),
                    sourceElementId);
            }
        }

        SectionSample[] BuildQuadraticHexSamples(SelectionCellTopology cell)
        {
            var samples = new SectionSample[27];
            for (var point = 0; point < 20; point++) samples[point] = OriginalSample(cell.LocalPointIndices[point]);
            for (var generated = 0; generated < QuadraticHexGeneratedParametricPoints.Length; generated++)
            {
                var weights = QuadraticHexGeneratedWeights[generated];
                var x = 0d; var y = 0d; var z = 0d; var scalar = 0d; var scalarValid = true;
                for (var point = 0; point < 20; point++)
                {
                    x += samples[point].X * weights[point];
                    y += samples[point].Y * weights[point];
                    z += samples[point].Z * weights[point];
                    if (Math.Abs(weights[point]) <= 1e-15) continue;
                    if (!float.IsFinite(samples[point].Scalar)) scalarValid = false;
                    else scalar += samples[point].Scalar * weights[point];
                }
                var identity = generated < 6
                    ? GetFaceCenterIdentity(cell, QuadraticHexFaces[generated]) : nextGeneratedIdentity--;
                samples[20 + generated] = new SectionSample(identity, x, y, z,
                    scalarValid && double.IsFinite(scalar) ? (float)scalar : float.NaN);
            }
            return samples;
        }

        SectionSample[] BuildQuadraticWedgeSamples(SelectionCellTopology cell)
        {
            var samples = new SectionSample[18];
            for (var point = 0; point < 15; point++) samples[point] = OriginalSample(cell.LocalPointIndices[point]);
            for (var generated = 0; generated < QuadraticWedgeGeneratedWeights.Length; generated++)
            {
                var weights = QuadraticWedgeGeneratedWeights[generated];
                var x = 0d; var y = 0d; var z = 0d; var scalar = 0d; var scalarValid = true;
                for (var point = 0; point < 15; point++)
                {
                    x += samples[point].X * weights[point];
                    y += samples[point].Y * weights[point];
                    z += samples[point].Z * weights[point];
                    if (Math.Abs(weights[point]) <= 1e-15) continue;
                    if (!float.IsFinite(samples[point].Scalar)) scalarValid = false;
                    else scalar += samples[point].Scalar * weights[point];
                }
                samples[15 + generated] = new SectionSample(
                    GetFaceCenterIdentity(cell, QuadraticWedgeFaces[generated]), x, y, z,
                    scalarValid && double.IsFinite(scalar) ? (float)scalar : float.NaN);
            }
            return samples;
        }

        SectionSample[] BuildQuadraticPyramidSamples(SelectionCellTopology cell)
        {
            var samples = new SectionSample[14];
            for (var point = 0; point < 13; point++) samples[point] = OriginalSample(cell.LocalPointIndices[point]);
            var weights = QuadraticPyramidGeneratedWeights;
            var x = 0d; var y = 0d; var z = 0d; var scalar = 0d; var scalarValid = true;
            for (var point = 0; point < 13; point++)
            {
                x += samples[point].X * weights[point];
                y += samples[point].Y * weights[point];
                z += samples[point].Z * weights[point];
                if (Math.Abs(weights[point]) <= 1e-15) continue;
                if (!float.IsFinite(samples[point].Scalar)) scalarValid = false;
                else scalar += samples[point].Scalar * weights[point];
            }
            samples[13] = new SectionSample(GetFaceCenterIdentity(cell, QuadraticPyramidBaseFace), x, y, z,
                scalarValid && double.IsFinite(scalar) ? (float)scalar : float.NaN);
            return samples;
        }

        long GetFaceCenterIdentity(SelectionCellTopology cell, IReadOnlyList<int> face)
        {
            Span<int> points = stackalloc int[8];
            for (var point = 0; point < points.Length; point++)
                points[point] = cell.LocalPointIndices[face[point]];
            points.Sort();
            var key = new FaceCenterKey(points[0], points[1], points[2], points[3],
                points[4], points[5], points[6], points[7]);
            if (generatedFaceIdentities.TryGetValue(key, out var identity)) return identity;
            identity = nextGeneratedIdentity--;
            generatedFaceIdentities.Add(key, identity);
            return identity;
        }

        uint GetSegmentVertex(int first, int second)
        {
            var firstDistance = SignedDistance(topology.Coordinates, first, offset, request.Point, normal);
            var secondDistance = SignedDistance(topology.Coordinates, second, offset, request.Point, normal);
            if (Math.Abs(firstDistance) <= tolerance)
                return GetVertex(new VertexKey(first, first, 0), first, first, 0);
            if (Math.Abs(secondDistance) <= tolerance)
                return GetVertex(new VertexKey(second, second, 0), second, second, 0);
            var fraction = firstDistance / (firstDistance - secondDistance);
            var low = Math.Min(first, second);
            var high = Math.Max(first, second);
            var canonicalFraction = first <= second ? fraction : 1 - fraction;
            return GetVertex(new VertexKey(low, high, Quantize(canonicalFraction)), first, second, fraction);
        }

        void AddCapPolygon(HashSet<uint> polygon, int sourceElementId)
        {
            if (polygon.Count < 3) return;
            var ordered = OrderPolygon(polygon, positions, origin, basisU, basisV);
            if (PolygonNormalDot(ordered, positions, normal) < 0) ordered.Reverse();
            for (var index = 1; index + 1 < ordered.Count; index++)
                AddCapTriangle(ordered[0], ordered[index], ordered[index + 1], sourceElementId);
        }

        void AddCapTriangle(uint first, uint second, uint third, int sourceElementId)
        {
            if (first == second || first == third || second == third) return;
            var low = Math.Min(first, Math.Min(second, third));
            var high = Math.Max(first, Math.Max(second, third));
            var middle = first + second + third - low - high;
            if (!triangleSet.Add((low, middle, high))) return;
            var orientation = Dot(Cross(Subtract(Point(second, positions), Point(first, positions)),
                Subtract(Point(third, positions), Point(first, positions))), normal);
            triangles.Add(first);
            if (orientation >= 0)
            {
                triangles.Add(second);
                triangles.Add(third);
            }
            else
            {
                triangles.Add(third);
                triangles.Add(second);
            }
            elementIds.Add(sourceElementId);
        }

        void AddBoundaryLines(HashSet<uint> polygon)
        {
            if (polygon.Count < 2) return;
            var ordered = OrderPolygon(polygon, positions, origin, basisU, basisV);
            for (var index = 0; index < ordered.Count; index++)
                AddLine(ordered[index], ordered[(index + 1) % ordered.Count]);
        }

        void AddTriangleBoundaryLines(int triangleStart)
        {
            var counts = new Dictionary<(uint, uint), int>();
            for (var triangle = triangleStart; triangle < triangles.Count; triangle += 3)
            {
                CountEdge(triangles[triangle], triangles[triangle + 1]);
                CountEdge(triangles[triangle + 1], triangles[triangle + 2]);
                CountEdge(triangles[triangle + 2], triangles[triangle]);
            }
            foreach (var (edge, count) in counts)
                if (count == 1) AddLine(edge.Item1, edge.Item2);

            void CountEdge(uint first, uint second)
            {
                var key = first < second ? (first, second) : (second, first);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        void AddLine(uint first, uint second)
        {
            if (first == second) return;
            var key = first < second ? (first, second) : (second, first);
            if (!lineSet.Add(key)) return;
            lines.Add(first);
            lines.Add(second);
        }
    }

    private static readonly double[] Zero = [0, 0, 0];
    private static readonly int[][] TetraEdges =
        [[0, 1], [1, 2], [2, 0], [0, 3], [1, 3], [2, 3]];
    private static readonly int[][] HexEdges =
    [
        [0, 1], [1, 2], [3, 2], [0, 3], [4, 5], [5, 6],
        [7, 6], [4, 7], [0, 4], [1, 5], [3, 7], [2, 6],
    ];
    private static readonly int[][] WedgeEdges =
        [[0, 1], [1, 2], [2, 0], [3, 4], [4, 5], [5, 3], [0, 3], [1, 4], [2, 5]];
    private static readonly int[][] PyramidEdges =
        [[0, 1], [1, 2], [2, 3], [3, 0], [0, 4], [1, 4], [2, 4], [3, 4]];
    // Exact 256 x 16 signed-edge table embedded in VTK 5.8 Filtering/vtkCell3D.cxx.
    // 255 is the byte representation of the source table's -1 terminator.
    private static readonly byte[] MarchingHexCases = Convert.FromBase64String(
        "/////////////////////wADCP////////////////8ACQH/////////////////AQMICQEI/////////////wELAv////////////////8AAwgBCwL/////" +
        "////////CQsCAAkC/////////////wIDCAIICwsICf////////8DAgr/////////////////AAIKCAAK/////////////wEACQIKA/////////////8BAgoB" +
        "CgkJCgj/////////AwELCgML/////////////wABCwALCAgLCv////////8DAAkDCQoKCQv/////////CQsICwoI/////////////wQIB///////////////" +
        "//8EAAMHBAP/////////////AAkBCAcE/////////////wQJAQQBBwcBA/////////8BCwIIBwT/////////////AwcEAwQAAQsC/////////wkLAgkCAAgH" +
        "BP////////8CCQsCBwkCAwcHBAn/////CAcEAwIK/////////////woHBAoEAgIEAP////////8JAQAIBwQCCgP/////////BAoHCQoECQIKCQEC/////wMB" +
        "CwMLCgcECP////////8BCwoBCgQBBAAHBAr/////BAgHCQoACQsKCgMA/////wQKBwQJCgkLCv////////8JBAX/////////////////CQQFAAMI////////" +
        "/////wAEBQEABf////////////8IBAUIBQMDBQH/////////AQsCCQQF/////////////wMIAAELAgQFCf////////8FCwIFAgQEAgD/////////AgULAwUC" +
        "AwQFAwgE/////wkEBQIKA/////////////8AAgoACggEBQn/////////AAQFAAUBAgoD/////////wIFAQIIBQIKCAQFCP////8LCgMLAwEJBAX/////////" +
        "BAUJAAEICAELCAsK/////wUABAUKAAULCgoDAP////8FCAQFCwgLCgj/////////CQgHBQkH/////////////wkAAwkDBQUDB/////////8ACAcABwEBBwX/" +
        "////////AQMFAwcF/////////////wkIBwkHBQsCAf////////8LAgEJAAUFAAMFAwf/////CAIACAUCCAcFCwIF/////wIFCwIDBQMHBf////////8HBQkH" +
        "CQgDAgr/////////CQcFCQIHCQACAgoH/////wIKAwAIAQEIBwEHBf////8KAQIKBwEHBQH/////////CQgFCAcFCwMBCwoD/////wUABwUJAAcACgELAAoA" +
        "C/8KAAsKAwALAAUIBwAFAAf/CgULBwUK/////////////wsFBv////////////////8AAwgFBgv/////////////CQEABQYL/////////////wEDCAEICQUG" +
        "C/////////8BBQYCAQb/////////////AQUGAQYCAwgA/////////wkFBgkGAAAGAv////////8FCAkFAggFBgIDCAL/////AgoDCwUG/////////////woI" +
        "AAoAAgsFBv////////8ACQECCgMFBgv/////////BQYLAQIJCQIKCQoI/////wYKAwYDBQUDAf////////8ACggABQoAAQUFBgr/////AwYKAAYDAAUGAAkF" +
        "/////wYJBQYKCQoICf////////8FBgsECAf/////////////BAADBAMHBgsF/////////wEACQUGCwgHBP////////8LBQYBBwkBAwcHBAn/////BgIBBgEF" +
        "BAgH/////////wEFAgUGAgMEAAMHBP////8IBwQJBQAABQYABgL/////BwkDBwQJAwkCBQYJAgkG/wMCCgcECAsFBv////////8FBgsEAgcEAAICCgf/////" +
        "AAkBBAgHAgoDBQYL/////wkBAgkCCgkKBAcECgUGC/8IBwQDBQoDAQUFBgr/////BQoBBQYKAQoABwQKAAoE/wAJBQAFBgAGAwoDBggHBP8GCQUGCgkECQcH" +
        "CQr/////CwkEBgsE/////////////wQGCwQLCQADCP////////8LAQALAAYGAAT/////////CAEDCAYBCAQGBgsB/////wEJBAEEAgIEBv////////8DCAAB" +
        "CQICCQQCBAb/////AAQCBAYC/////////////wgCAwgEAgQGAv////////8LCQQLBAYKAwL/////////AAIIAgoIBAsJBAYL/////wMCCgAGAQAEBgYLAf//" +
        "//8GAQQGCwEEAQgCCgEIAQr/CQQGCQYDCQMBCgMG/////wgBCggAAQoBBgkEAQYBBP8DBgoDAAYABAb/////////BggECggG/////////////wcGCwcLCAgL" +
        "Cf////////8AAwcABwsACwkGCwf/////CwcGAQcLAQgHAQAI/////wsHBgsBBwEDB/////////8BBgIBCAYBCQgIBwb/////AgkGAgEJBgkHAAMJBwkD/wcA" +
        "CAcGAAYCAP////////8HAgMGAgf/////////////AgoDCwgGCwkICAcG/////wIHAAIKBwAHCQYLBwkHC/8BAAgBCAcBBwsGCwcCCgP/CgECCgcBCwEGBgEH" +
        "/////wgGCQgHBgkGAQoDBgEGA/8AAQkKBwb/////////////BwAIBwYAAwAKCgAG/////wcGCv////////////////8HCgb/////////////////AwgACgYH" +
        "/////////////wAJAQoGB/////////////8ICQEIAQMKBgf/////////CwIBBgcK/////////////wELAgMIAAYHCv////////8CAAkCCQsGBwr/////////" +
        "BgcKAgMLCwMICwgJ/////wcDAgYHAv////////////8HCAAHAAYGAAL/////////AgYHAgcDAAkB/////////wECBgEGCAEICQgGB/////8LBgcLBwEBBwP/" +
        "////////CwYHAQsHAQcIAQgA/////wAHAwALBwAJCwYHC/////8HCwYHCAsICQv/////////BgQICgYI/////////////wMKBgMGAAAGBP////////8ICgYI" +
        "BgQJAQD/////////CQYECQMGCQEDCgYD/////wYECAYICgIBC/////////8BCwIDCgAACgYABgT/////BAgKBAoGAAkCAgkL/////wsDCQsCAwkDBAoGAwQD" +
        "Bv8IAwIIAgQEAgb/////////AAIEBAIG/////////////wEACQIEAwIGBAQIA/////8BBAkBAgQCBgT/////////CAMBCAEGCAYEBgEL/////wsAAQsGAAYE" +
        "AP////////8EAwYECAMGAwsACQMLAwn/CwQJBgQL/////////////wQFCQcKBv////////////8AAwgEBQkKBgf/////////BQEABQAEBwoG/////////woG" +
        "BwgEAwMEBQMFAf////8JBAULAgEHCgb/////////BgcKAQsCAAMIBAUJ/////wcKBgULBAQLAgQCAP////8DCAQDBAUDBQILAgUKBgf/BwMCBwIGBQkE////" +
        "/////wkEBQAGCAACBgYHCP////8DAgYDBgcBAAUFAAT/////BggCBgcIAggBBAUIAQgF/wkEBQsGAQEGBwEHA/////8BCwYBBgcBBwAIAAcJBAX/BAsABAUL" +
        "AAsDBgcLAwsH/wcLBgcICwULBAQLCP////8GBQkGCQoKCQj/////////AwoGAAMGAAYFAAUJ/////wAICgAKBQAFAQUKBv////8GAwoGBQMFAQP/////////" +
        "AQsCCQoFCQgKCgYF/////wADCgAKBgAGCQUJBgELAv8KBQgKBgUIBQALAgUABQL/BgMKBgUDAgMLCwMF/////wUJCAUIAgUCBgMCCP////8JBgUJAAYAAgb/" +
        "////////AQgFAQAIBQgGAwIIBggC/wEGBQIGAf////////////8BBgMBCwYDBggFCQYIBgn/CwABCwYACQAFBQAG/////wAIAwULBv////////////8LBgX/" +
        "////////////////CgsFBwoF/////////////woLBQoFBwgAA/////////8FBwoFCgsBAAn/////////CwUHCwcKCQEICAED/////woCAQoBBwcBBf//////" +
        "//8AAwgBBwIBBQcHCgL/////CQUHCQcCCQIAAgcK/////wcCBQcKAgUCCQMIAgkCCP8CCwUCBQMDBQf/////////CAACCAIFCAUHCwUC/////wkBAAUDCwUH" +
        "AwMCC/////8JAggJAQIIAgcLBQIHAgX/AQUDAwUH/////////////wAHCAABBwEFB/////////8JAwAJBQMFBwP/////////CQcIBQcJ/////////////wUE" +
        "CAUICwsICv////////8FBAAFAAoFCgsKAAP/////AAkBCAsECAoLCwUE/////wsECgsFBAoEAwkBBAMEAf8CAQUCBQgCCAoECAX/////AAoEAAMKBAoFAgEK" +
        "BQoB/wAFAgAJBQIFCgQIBQoFCP8JBQQCAwr/////////////AgsFAwIFAwUEAwQI/////wUCCwUEAgQAAv////////8DAgsDCwUDBQgECAUACQH/BQILBQQC" +
        "AQIJCQIE/////wgFBAgDBQMBBf////////8ABQQBBQD/////////////CAUECAMFCQUAAAUD/////wkFBP////////////////8EBwoECgkJCgv/////////" +
        "AAMIBAcJCQcKCQoL/////wEKCwEECgEABAcKBP////8DBAEDCAQBBAsHCgQLBAr/BAcKCQQKCQoCCQIB/////wkEBwkHCgkKAQIBCgADCP8KBAcKAgQCAAT/" +
        "////////CgQHCgIECAQDAwQC/////wILCQIJBwIHAwcJBP////8JBwsJBAcLBwIIAAcCBwD/AwsHAwILBwsEAQALBAsA/wECCwgEB/////////////8EAQkE" +
        "BwEHAwH/////////BAEJBAcBAAEICAEH/////wQDAAcDBP////////////8EBwj/////////////////CQgLCwgK/////////////wMJAAMKCQoLCf//////" +
        "//8ACwEACAsICgv/////////AwsBCgsD/////////////wEKAgEJCgkICv////////8DCQADCgkBCQICCQr/////AAoCCAoA/////////////wMKAv//////" +
        "//////////8CCAMCCwgLCQj/////////CQILAAIJ/////////////wIIAwILCAAIAQEIC/////8BAgv/////////////////AQgDCQgB/////////////wAB" +
        "Cf////////////////8ACAP//////////////////////////////////////w==");

    // Exact 64 x 13 signed-edge table embedded in VTK 5.8 Filtering/vtkWedge.cxx.
    private static readonly byte[] MarchingWedgeCases = Convert.FromBase64String(
        "/////////////////wAGAv////////////8AAQf/////////////BgEHBgIB/////////wECCP////////////8GAQAGCAH/////////AAIIBwAI////////" +
        "/wcGCP////////////8DBQb/////////////AwUABQIA/////////wABBwYDBf////////8BBwMBAwUBBQL/////AggBBgMF/////////wADAQEDBQEFCP//" +
        "//8GAwUACAcAAgj/////BwMFBwUI/////////wcEA/////////////8HBAMABgL/////////AAEDAQQD/////////wEEAwEDBgEGAv////8HBAMCCAH/////" +
        "////BwQDBgEABggB/////wAEAwAIBAACCP////8GCAMDCAT/////////BgcEBgQF/////////wAHBQcEBQIABf////8BBgABBQYBBAX/////AgEFBQEE////" +
        "/////wIIAQYHBQcEBf////8ABwUHBAUABQEBBQj/AAIIAAgEAAQFAAUG/wgEBf////////////8ECAX/////////////BAgFAAYC/////////wQIBQABB///" +
        "//////8ECAUGAQcGAgH/////AQUEAgUB/////////wEFBAEGBQEABv////8FBAcFBwAFAAL/////BgQHBgUE/////////wYDCAMECP////////8AAwQABAgA" +
        "CAL/////BwABBgMEBgQI/////wEHAwEDAgIDCAgDBP8CBgEGAwEDBAH/////AAMBAQME/////////wcABAQAAgQCAwMCBv8HAwT/////////////BwgFBwUD" +
        "/////////wAGAgcIBQcFA/////8AAQMBBQMBCAX/////AgEGBgEDBQEIAwEF/wEDBwEFAwECBf////8BAAYBBgUBBQcHBQP/AAIFAAUD/////////wMGBf//" +
        "//////////8HCAb/////////////AAcIAAgC/////////wABBgEIBv////////8CAQj/////////////BgcBBgEC/////////wAHAf////////////8AAgb/" +
        "/////////////////////////////w==");
    // Exact 32 x 13 signed-edge table embedded in VTK 5.8 Filtering/vtkPyramid.cxx.
    private static readonly byte[] MarchingPyramidCases = Convert.FromBase64String(
        "/////////////////wMEAP////////////8FAQD/////////////BQEEAQME/////////wYCAf////////////8DBAAGAgH/////////BQIABgIF////////" +
        "/wIDBAIEBgQFBv////8CBwP/////////////AgcEBAAC/////////wUBAAIHA/////////8FBwQBBwUCBwH/////BgMBBwMG/////////wQGBwAGBAEGAP//" +
        "//8HBQYDBQcABQP/////BwQFBwUG/////////wUHBAYHBf////////8ABQMFBgMGBwP/////AAEEAQcEAQYH/////wEGAwYHA/////////8HBQQHAQUHAgH/" +
        "////AwcABwUABwIFAgEF/wQCAAcCBP////////8HAgP/////////////AgQDBQQCBgUC/////wIFAAIGBf////////8GAQAEBgADBgQDAgb/AgYB////////" +
        "/////wEEAwEFBP////////8BBQD/////////////BAMA//////////////////////////////8=");
    private static readonly int[] LinearTetraCell = [0, 1, 2, 3];
    private static readonly int[] LinearHexCell = [0, 1, 2, 3, 4, 5, 6, 7];
    private static readonly int[] VoxelToHexCell = [0, 1, 3, 2, 4, 5, 7, 6];
    private static readonly int[] LinearWedgeCell = [0, 1, 2, 3, 4, 5];
    private static readonly int[] LinearPyramidCell = [0, 1, 2, 3, 4];
    private static readonly int[][] QuadraticTetraSubcells =
    [
        [0, 4, 6, 7], [4, 1, 5, 8], [6, 5, 2, 9], [7, 8, 9, 3],
        [6, 4, 5, 8], [6, 5, 9, 8], [6, 9, 7, 8], [6, 7, 4, 8],
    ];
    private static readonly int[][] MarchingTetraCases =
    [
        [], [0, 3, 2], [0, 1, 4], [3, 2, 4, 4, 2, 1],
        [1, 2, 5], [3, 5, 1, 3, 1, 0], [0, 2, 5, 0, 5, 4], [3, 5, 4],
        [3, 4, 5], [0, 4, 5, 0, 5, 2], [0, 5, 3, 0, 1, 5], [5, 2, 1],
        [3, 4, 1, 3, 1, 2], [0, 4, 1], [0, 2, 3], [],
    ];
    private static readonly int[][] QuadraticHexSubcells =
    [
        [0, 8, 24, 11, 16, 22, 26, 20], [8, 1, 9, 24, 22, 17, 21, 26],
        [11, 24, 10, 3, 20, 26, 23, 19], [24, 9, 2, 10, 26, 21, 18, 23],
        [16, 22, 26, 20, 4, 12, 25, 15], [22, 17, 21, 26, 12, 5, 13, 25],
        [20, 26, 23, 19, 15, 25, 14, 7], [26, 21, 18, 23, 25, 13, 6, 14],
    ];
    private static readonly int[][] QuadraticWedgeSubcells =
    [
        [0, 6, 8, 12, 15, 17], [6, 7, 8, 15, 16, 17],
        [6, 1, 7, 15, 13, 16], [8, 7, 2, 17, 16, 14],
        [12, 15, 17, 3, 9, 11], [15, 16, 17, 9, 10, 11],
        [15, 13, 16, 9, 4, 10], [17, 16, 14, 11, 10, 5],
    ];
    private static readonly int[][] QuadraticPyramidSubcells =
    [
        [0, 5, 13, 8, 9], [5, 1, 6, 13, 10], [8, 13, 7, 3, 12],
        [13, 6, 2, 7, 11], [9, 10, 11, 12, 4], [9, 12, 11, 10, 13],
        [5, 10, 9, 13], [6, 11, 10, 13], [7, 12, 11, 13], [8, 9, 12, 13],
    ];
    private static readonly int[][] QuadraticHexFaces =
    [
        [0, 4, 7, 3, 16, 15, 19, 11], [1, 2, 6, 5, 9, 18, 13, 17],
        [0, 1, 5, 4, 8, 17, 12, 16], [3, 7, 6, 2, 19, 14, 18, 10],
        [0, 3, 2, 1, 11, 10, 9, 8], [4, 5, 6, 7, 12, 13, 14, 15],
    ];
    private static readonly int[][] QuadraticWedgeFaces =
    [
        [0, 3, 4, 1, 12, 9, 13, 6], [1, 4, 5, 2, 13, 10, 14, 7],
        [2, 5, 3, 0, 14, 11, 12, 8],
    ];
    private static readonly int[] QuadraticPyramidBaseFace = [0, 3, 2, 1, 8, 7, 6, 5];
    private static readonly double[][] QuadraticHexGeneratedParametricPoints =
    [
        [0, 0.5, 0.5], [1, 0.5, 0.5], [0.5, 0, 0.5], [0.5, 1, 0.5],
        [0.5, 0.5, 0], [0.5, 0.5, 1], [0.5, 0.5, 0.5],
    ];
    private static readonly double[][] QuadraticHexGeneratedWeights =
        QuadraticHexGeneratedParametricPoints.Select(QuadraticHexWeights).ToArray();
    private static readonly double[][] QuadraticWedgeGeneratedParametricPoints =
        [[0.5, 0, 0.5], [0.5, 0.5, 0.5], [0, 0.5, 0.5]];
    private static readonly double[][] QuadraticWedgeGeneratedWeights =
        QuadraticWedgeGeneratedParametricPoints.Select(QuadraticWedgeWeights).ToArray();
    private static readonly double[] QuadraticPyramidGeneratedWeights =
        QuadraticPyramidWeights([0.5, 0.5, 0]);

    private static double[] QuadraticHexWeights(IReadOnlyList<double> parametric)
    {
        var r = 2 * (parametric[0] - 0.5); var s = 2 * (parametric[1] - 0.5);
        var t = 2 * (parametric[2] - 0.5);
        var rm = 1 - r; var rp = 1 + r; var sm = 1 - s; var sp = 1 + s;
        var tm = 1 - t; var tp = 1 + t; var r2 = 1 - r * r; var s2 = 1 - s * s;
        var t2 = 1 - t * t;
        return
        [
            0.125 * rm * sm * tm * (-r - s - t - 2),
            0.125 * rp * sm * tm * (r - s - t - 2),
            0.125 * rp * sp * tm * (r + s - t - 2),
            0.125 * rm * sp * tm * (-r + s - t - 2),
            0.125 * rm * sm * tp * (-r - s + t - 2),
            0.125 * rp * sm * tp * (r - s + t - 2),
            0.125 * rp * sp * tp * (r + s + t - 2),
            0.125 * rm * sp * tp * (-r + s + t - 2),
            0.25 * r2 * sm * tm, 0.25 * s2 * rp * tm,
            0.25 * r2 * sp * tm, 0.25 * s2 * rm * tm,
            0.25 * r2 * sm * tp, 0.25 * s2 * rp * tp,
            0.25 * r2 * sp * tp, 0.25 * s2 * rm * tp,
            0.25 * t2 * rm * sm, 0.25 * t2 * rp * sm,
            0.25 * t2 * rp * sp, 0.25 * t2 * rm * sp,
        ];
    }

    private static double[] QuadraticWedgeWeights(IReadOnlyList<double> parametric)
    {
        var r = parametric[0]; var s = parametric[1]; var t = parametric[2];
        return
        [
            2 * (1 - r - s) * (1 - t) * (0.5 - r - s - t),
            2 * r * (1 - t) * (r - t - 0.5),
            2 * s * (1 - t) * (s - t - 0.5),
            2 * (1 - r - s) * t * (t - r - s - 0.5),
            2 * r * t * (r + t - 1.5),
            2 * s * t * (s + t - 1.5),
            4 * r * (1 - r - s) * (1 - t), 4 * r * s * (1 - t),
            4 * (1 - r - s) * s * (1 - t), 4 * r * (1 - r - s) * t,
            4 * r * s * t, 4 * (1 - r - s) * s * t,
            4 * t * (1 - r - s) * (1 - t), 4 * t * r * (1 - t),
            4 * t * s * (1 - t),
        ];
    }

    private static double[] QuadraticPyramidWeights(IReadOnlyList<double> parametric)
    {
        var r = 2 * parametric[0] - 1; var s = 2 * parametric[1] - 1;
        var t = 2 * parametric[2] - 1;
        var rm = 1 - r; var rp = 1 + r; var sm = 1 - s; var sp = 1 + s;
        var tm = 1 - t; var tp = 1 + t; var r2 = 1 - r * r; var s2 = 1 - s * s;
        var t2 = 1 - t * t;
        return
        [
            0.125 * rm * sm * tm * (-r - s - t - 2),
            0.125 * rp * sm * tm * (r - s - t - 2),
            0.125 * rp * sp * tm * (r + s - t - 2),
            0.125 * rm * sp * tm * (-r + s - t - 2),
            0.5 * t * tp,
            0.25 * r2 * sm * tm, 0.25 * s2 * rp * tm,
            0.25 * r2 * sp * tm, 0.25 * s2 * rm * tm,
            0.25 * rm * sm * t2, 0.25 * rp * sm * t2,
            0.25 * rp * sp * t2, 0.25 * rm * sp * t2,
        ];
    }

    private static void Validate(SectionResolveRequest request)
    {
        if (request.Point.Count != 3 || request.Normal.Count != 3 || request.SectionColor.Count != 4 ||
            request.Point.Concat(request.Normal).Any(value => !double.IsFinite(value) || Math.Abs(value) > 1e150) ||
            request.SectionColor.Any(value => !double.IsFinite(value) || value is < 0 or > 1) ||
            request.Normal.All(value => Math.Abs(value) <= 1e-12))
            throw new SectionGeometryException("section-invalid", "The section plane or color is invalid.");
        var offsets = request.ActorOffsets ?? [];
        if (offsets.Count > MaximumActors || offsets.Select(item => item.ActorName).Distinct(StringComparer.Ordinal).Count() != offsets.Count ||
            offsets.Any(item => string.IsNullOrWhiteSpace(item.ActorName) || item.ActorName.Length > 1024 || item.Offset.Count != 3 ||
                item.Offset.Any(value => !double.IsFinite(value) || Math.Abs(value) > 1e150)))
            throw new SectionGeometryException("section-invalid", "The section actor offsets are invalid.");
    }

    private static int[][]? EdgeChains(int vtkCellType) => vtkCellType switch
    {
        3 => [[0, 1]], 5 => [[0, 1], [1, 2], [2, 0]], 9 => [[0, 1], [1, 2], [2, 3], [3, 0]],
        10 => [[0, 1], [1, 2], [2, 0], [0, 3], [1, 3], [2, 3]],
        11 => [[0, 1], [0, 2], [1, 3], [2, 3], [4, 5], [4, 6], [5, 7], [6, 7], [0, 4], [1, 5], [2, 6], [3, 7]],
        12 => [[0, 1], [1, 2], [2, 3], [3, 0], [4, 5], [5, 6], [6, 7], [7, 4], [0, 4], [1, 5], [2, 6], [3, 7]],
        13 => [[0, 1], [1, 2], [2, 0], [3, 4], [4, 5], [5, 3], [0, 3], [1, 4], [2, 5]],
        14 => [[0, 1], [1, 2], [2, 3], [3, 0], [0, 4], [1, 4], [2, 4], [3, 4]],
        21 => [[0, 2, 1]],
        22 => [[0, 3, 1], [1, 4, 2], [2, 5, 0]],
        23 => [[0, 4, 1], [1, 5, 2], [2, 6, 3], [3, 7, 0]],
        24 => [[0, 4, 1], [1, 5, 2], [2, 6, 0], [0, 7, 3], [1, 8, 3], [2, 9, 3]],
        25 or 29 => [[0, 8, 1], [1, 9, 2], [2, 10, 3], [3, 11, 0], [4, 12, 5], [5, 13, 6],
                     [6, 14, 7], [7, 15, 4], [0, 16, 4], [1, 17, 5], [2, 18, 6], [3, 19, 7]],
        26 => [[0, 6, 1], [1, 7, 2], [2, 8, 0], [3, 9, 4], [4, 10, 5], [5, 11, 3],
               [0, 12, 3], [1, 13, 4], [2, 14, 5]],
        27 => [[0, 5, 1], [1, 6, 2], [2, 7, 3], [3, 8, 0], [0, 9, 4], [1, 10, 4], [2, 11, 4], [3, 12, 4]],
        _ => null,
    };

    private static bool IsVolume(int vtkCellType) => vtkCellType is 10 or 11 or 12 or 13 or 14 or 24 or 25 or 26 or 27 or 29;

    private static double SignedDistance(IReadOnlyList<double> coordinates, int point, IReadOnlyList<double> offset,
        IReadOnlyList<double> planePoint, IReadOnlyList<double> normal) =>
        (coordinates[point * 3] + offset[0] - planePoint[0]) * normal[0] +
        (coordinates[point * 3 + 1] + offset[1] - planePoint[1]) * normal[1] +
        (coordinates[point * 3 + 2] + offset[2] - planePoint[2]) * normal[2];

    private static List<uint> OrderPolygon(IEnumerable<uint> indices, IReadOnlyList<float> positions,
        IReadOnlyList<double> origin, IReadOnlyList<double> basisU, IReadOnlyList<double> basisV)
    {
        var values = indices.ToArray();
        var center = new double[3];
        foreach (var index in values) for (var component = 0; component < 3; component++)
            center[component] += positions[(int)index * 3 + component] + origin[component];
        for (var component = 0; component < 3; component++) center[component] /= values.Length;
        return values.OrderBy(index =>
        {
            var delta = new[] { positions[(int)index * 3] + origin[0] - center[0],
                positions[(int)index * 3 + 1] + origin[1] - center[1],
                positions[(int)index * 3 + 2] + origin[2] - center[2] };
            return Math.Atan2(Dot(delta, basisV), Dot(delta, basisU));
        }).ToList();
    }

    private static double PolygonNormalDot(IReadOnlyList<uint> polygon, IReadOnlyList<float> positions, IReadOnlyList<double> normal)
    {
        var first = Point(polygon[0], positions);
        var second = Point(polygon[1], positions);
        var third = Point(polygon[2], positions);
        return Dot(Cross(Subtract(second, first), Subtract(third, first)), normal);
    }

    private static double BoundsDiagonal(IReadOnlyList<double> bounds) => bounds.Count == 6
        ? Math.Sqrt(Math.Pow(bounds[1] - bounds[0], 2) + Math.Pow(bounds[3] - bounds[2], 2) + Math.Pow(bounds[5] - bounds[4], 2)) : 1;

    private static IReadOnlyList<double> ResolveColor(IReadOnlyList<double> source, SectionResolveRequest request)
    {
        if (request.LightenColors) return [source[0] + (1 - source[0]) * 0.35,
            source[1] + (1 - source[1]) * 0.35, source[2] + (1 - source[2]) * 0.35, source[3]];
        return request.SectionColor[3] > 0 ? request.SectionColor.ToArray() : source.ToArray();
    }

    private static double[] CalculateBounds(IReadOnlyList<float> positions, IReadOnlyList<double> origin)
    {
        var bounds = new[] { double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity,
            double.NegativeInfinity, double.PositiveInfinity, double.NegativeInfinity };
        for (var point = 0; point < positions.Count / 3; point++) for (var component = 0; component < 3; component++)
        {
            var value = positions[point * 3 + component] + origin[component];
            bounds[component * 2] = Math.Min(bounds[component * 2], value);
            bounds[component * 2 + 1] = Math.Max(bounds[component * 2 + 1], value);
        }
        return bounds;
    }

    private static long Quantize(double value) => (long)Math.Round(value * 1_000_000_000_000d);
    private static double[] Point(uint index, IReadOnlyList<float> values) => [values[(int)index * 3], values[(int)index * 3 + 1], values[(int)index * 3 + 2]];
    private static double[] Subtract(IReadOnlyList<double> first, IReadOnlyList<double> second) => [first[0] - second[0], first[1] - second[1], first[2] - second[2]];
    private static double Dot(IReadOnlyList<double> first, IReadOnlyList<double> second) => first[0] * second[0] + first[1] * second[1] + first[2] * second[2];
    private static double[] Cross(IReadOnlyList<double> first, IReadOnlyList<double> second) => [first[1] * second[2] - first[2] * second[1], first[2] * second[0] - first[0] * second[2], first[0] * second[1] - first[1] * second[0]];
    private static double[] Normalize(IReadOnlyList<double> value) { var length = Math.Sqrt(Dot(value, value)); return value.Select(component => component / length).ToArray(); }

    private readonly record struct SectionSample(long Identity, double X, double Y, double Z, float Scalar);
    private readonly record struct VertexKey(long First, long Second, long Fraction);
    private readonly record struct PositionKey(double X, double Y, double Z);
    private readonly record struct FaceCenterKey(int A, int B, int C, int D, int E, int F, int G, int H);
}

internal sealed class SectionGeometryException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
