using CaeGlobals;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using vtkControl;

namespace PrePoMax.Rendering
{
    internal sealed class VtkJsSceneCapture
    {
        private const long MaximumUploadBytes = 256L * 1024 * 1024;
        private readonly object _gate = new object();
        private readonly Dictionary<string, CapturedActor> _actors =
            new Dictionary<string, CapturedActor>(StringComparer.Ordinal);

        public bool Capture(vtkMaxActorData actorData, out string diagnostic)
        {
            return Capture(actorData, false, out diagnostic);
        }

        public bool CaptureSelection(vtkMaxActorData actorData, out string diagnostic)
        {
            return Capture(actorData, true, out diagnostic);
        }

        public bool CaptureAuxiliary(vtkMaxActorData actorData, out string diagnostic)
        {
            return Capture(actorData, true, out diagnostic);
        }

        private bool Capture(vtkMaxActorData actorData, bool allowNonPart, out string diagnostic)
        {
            CapturedActor actor;
            if (!TryCapture(actorData, null, null, allowNonPart, out actor, out diagnostic)) return false;
            lock (_gate) _actors[actor.Name] = actor;
            diagnostic = "captured actor='" + actor.Name + "' points=" + actor.SourcePointIds.Length +
                         " triangles=" + actor.SourceElementIds.Length +
                         " sectionViewPossible=" + actor.SectionViewPossible;
            return true;
        }

        public bool CaptureAnimationFrame(vtkMaxActorData actorData, int frameNumber, out string diagnostic)
        {
            diagnostic = null;
            if (actorData == null || actorData.Geometry == null || actorData.Geometry.NodesAnimation == null ||
                frameNumber < 0 || frameNumber >= actorData.Geometry.NodesAnimation.Length)
            {
                diagnostic = "rejected animation frame=" + frameNumber + ": unavailable geometry";
                return false;
            }
            NodesExchangeData locatorNodes = null;
            if (actorData.CellLocator != null && actorData.CellLocator.NodesAnimation != null)
            {
                if (frameNumber >= actorData.CellLocator.NodesAnimation.Length)
                {
                    diagnostic = "rejected animation frame=" + frameNumber + ": unavailable locator geometry";
                    return false;
                }
                locatorNodes = actorData.CellLocator.NodesAnimation[frameNumber];
            }
            CapturedActor actor;
            if (!TryCapture(actorData, actorData.Geometry.NodesAnimation[frameNumber], locatorNodes, false,
                            out actor, out diagnostic)) return false;
            lock (_gate) _actors[actor.Name] = actor;
            diagnostic = "captured animation actor='" + actor.Name + "' frame=" + frameNumber +
                         " points=" + actor.SourcePointIds.Length + " triangles=" + actor.SourceElementIds.Length;
            return true;
        }

        public bool CaptureAnimationFrame(IEnumerable<vtkMaxActorData> actorData, int frameNumber,
                                          out string[] diagnostics)
        {
            if (actorData == null) throw new ArgumentNullException("actorData");
            Dictionary<string, CapturedActor> baselines;
            lock (_gate) baselines = _actors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var captured = new List<CapturedActor>();
            var messages = new List<string>();
            foreach (vtkMaxActorData source in actorData)
            {
                string diagnostic = "rejected animation frame=" + frameNumber + ": unavailable captured baseline";
                if (source == null || source.Geometry == null || source.Geometry.NodesAnimation == null ||
                    frameNumber < 0 || frameNumber >= source.Geometry.NodesAnimation.Length)
                {
                    messages.Add("rejected animation frame=" + frameNumber + ": unavailable geometry for actor='" +
                                 (source == null ? string.Empty : source.Name) + "'");
                    diagnostics = messages.ToArray();
                    return false;
                }
                NodesExchangeData locatorNodes = null;
                if (source.CellLocator != null && source.CellLocator.NodesAnimation != null)
                {
                    if (frameNumber >= source.CellLocator.NodesAnimation.Length)
                    {
                        messages.Add("rejected animation frame=" + frameNumber +
                                     ": unavailable locator geometry for actor='" + source.Name + "'");
                        diagnostics = messages.ToArray();
                        return false;
                    }
                    locatorNodes = source.CellLocator.NodesAnimation[frameNumber];
                }
                CapturedActor actor;
                CapturedActor baseline;
                if (!baselines.TryGetValue(source.Name, out baseline) ||
                    !TryCaptureFrame(source, source.Geometry.NodesAnimation[frameNumber], locatorNodes,
                                     baseline, out actor, out diagnostic))
                {
                    messages.Add(diagnostic);
                    diagnostics = messages.ToArray();
                    return false;
                }
                captured.Add(actor);
                messages.Add("captured animation actor='" + actor.Name + "' frame=" + frameNumber +
                             " points=" + actor.SourcePointIds.Length +
                             " triangles=" + actor.SourceElementIds.Length);
            }
            if (captured.Count == 0)
            {
                diagnostics = new[] { "rejected animation frame=" + frameNumber + ": no actors" };
                return false;
            }
            if (captured.Select(actor => actor.Name).Distinct(StringComparer.Ordinal).Count() != captured.Count)
            {
                diagnostics = new[] { "rejected animation frame=" + frameNumber + ": duplicate actor names" };
                return false;
            }
            lock (_gate)
            {
                foreach (CapturedActor actor in captured) _actors[actor.Name] = actor;
            }
            diagnostics = messages.ToArray();
            return true;
        }

        private static bool TryCaptureFrame(vtkMaxActorData actorData, NodesExchangeData renderNodes,
                                            NodesExchangeData locatorNodes, CapturedActor baseline,
                                            out CapturedActor actor, out string diagnostic)
        {
            actor = null;
            diagnostic = null;
            if (actorData == null || renderNodes == null || renderNodes.Coor == null ||
                renderNodes.Coor.Length != baseline.SourcePointIds.Length ||
                (renderNodes.Values != null && renderNodes.Values.Length != renderNodes.Coor.Length))
            {
                diagnostic = "rejected animation frame for actor='" +
                             (actorData == null ? string.Empty : actorData.Name) + "': inconsistent render arrays";
                return false;
            }
            var coordinates = new double[checked(renderNodes.Coor.Length * 3)];
            var scalars = new float[renderNodes.Coor.Length];
            for (int point = 0; point < renderNodes.Coor.Length; point++)
            {
                double[] coordinate = renderNodes.Coor[point];
                if (coordinate == null || coordinate.Length < 3 ||
                    double.IsNaN(coordinate[0]) || double.IsInfinity(coordinate[0]) ||
                    double.IsNaN(coordinate[1]) || double.IsInfinity(coordinate[1]) ||
                    double.IsNaN(coordinate[2]) || double.IsInfinity(coordinate[2]))
                {
                    diagnostic = "rejected animation frame for actor='" + actorData.Name +
                                 "': invalid render point " + point;
                    return false;
                }
                coordinates[point * 3] = coordinate[0];
                coordinates[point * 3 + 1] = coordinate[1];
                coordinates[point * 3 + 2] = coordinate[2];
                scalars[point] = renderNodes.Values == null ? float.NaN : renderNodes.Values[point];
            }

            var locatorCoordinates = new double[baseline.LocatorNodeIds.Length * 3];
            if (baseline.LocatorNodeIds.Length > 0)
            {
                if (locatorNodes == null || locatorNodes.Coor == null ||
                    locatorNodes.Coor.Length != baseline.LocatorNodeIds.Length)
                {
                    diagnostic = "rejected animation frame for actor='" + actorData.Name +
                                 "': inconsistent locator arrays";
                    return false;
                }
                for (int point = 0; point < locatorNodes.Coor.Length; point++)
                {
                    double[] coordinate = locatorNodes.Coor[point];
                    if (coordinate == null || coordinate.Length < 3 ||
                        double.IsNaN(coordinate[0]) || double.IsInfinity(coordinate[0]) ||
                        double.IsNaN(coordinate[1]) || double.IsInfinity(coordinate[1]) ||
                        double.IsNaN(coordinate[2]) || double.IsInfinity(coordinate[2]))
                    {
                        diagnostic = "rejected animation frame for actor='" + actorData.Name +
                                     "': invalid locator point " + point;
                        return false;
                    }
                    locatorCoordinates[point * 3] = coordinate[0];
                    locatorCoordinates[point * 3 + 1] = coordinate[1];
                    locatorCoordinates[point * 3 + 2] = coordinate[2];
                }
            }
            actor = baseline.WithFrame(coordinates, scalars, locatorCoordinates);
            return true;
        }

        public void Clear()
        {
            lock (_gate) _actors.Clear();
        }

        public void RemoveActors(IEnumerable<string> actorNames)
        {
            if (actorNames == null) throw new ArgumentNullException("actorNames");
            lock (_gate)
            {
                foreach (string actorName in actorNames) _actors.Remove(actorName);
            }
        }

        public void UpdateActor(string oldName, string newName, Color color)
        {
            lock (_gate)
            {
                CapturedActor actor;
                if (!_actors.TryGetValue(oldName, out actor)) return;
                if (!string.Equals(oldName, newName, StringComparison.Ordinal) && _actors.ContainsKey(newName))
                    throw new InvalidOperationException("A captured PrePoMax actor already uses the requested name.");
                _actors.Remove(oldName);
                _actors[newName] = actor.WithPresentation(newName, color.ToArgb());
            }
        }

        public bool UpdateScalars(string actorName, float[] values)
        {
            lock (_gate)
            {
                CapturedActor actor;
                if (!_actors.TryGetValue(actorName, out actor)) return false;
                if (values == null || values.Length != actor.SourcePointIds.Length)
                    throw new InvalidOperationException("Updated scalar values do not match the captured actor point count.");
                _actors[actorName] = actor.WithScalars((float[])values.Clone());
                return true;
            }
        }

        public VtkJsSceneSnapshot Snapshot()
        {
            lock (_gate) return new VtkJsSceneSnapshot(_actors.Values.OrderBy(actor => actor.Name, StringComparer.Ordinal).ToArray());
        }

        public string[] GetActorNames()
        {
            lock (_gate) return _actors.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        public bool ContainsActor(string actorName)
        {
            if (actorName == null) return false;
            lock (_gate) return _actors.ContainsKey(actorName);
        }

        public VtkJsScalarSnapshot ScalarSnapshot(IEnumerable<string> actorNames)
        {
            if (actorNames == null) throw new ArgumentNullException("actorNames");
            lock (_gate)
            {
                CapturedActor[] actors = actorNames.Distinct(StringComparer.Ordinal)
                    .Where(name => _actors.ContainsKey(name))
                    .Select(name => _actors[name])
                    .OrderBy(item => item.Name, StringComparer.Ordinal)
                    .ToArray();
                return new VtkJsScalarSnapshot(actors);
            }
        }

        public VtkJsFrameSnapshot FrameSnapshot(IEnumerable<string> actorNames)
        {
            if (actorNames == null) throw new ArgumentNullException("actorNames");
            lock (_gate)
            {
                var requested = new HashSet<string>(actorNames, StringComparer.Ordinal);
                return new VtkJsFrameSnapshot(_actors.Values
                    .Where(actor => requested.Contains(actor.Name))
                    .OrderBy(actor => actor.Name, StringComparer.Ordinal).ToArray());
            }
        }

        public vtkMaxActorData[] ExportActorData(ISet<string> hiddenActorNames,
                                                  IDictionary<string, bool> colorContours)
        {
            lock (_gate)
            {
                return _actors.Values
                    .Where(actor => hiddenActorNames == null || !hiddenActorNames.Contains(actor.Name))
                    .OrderBy(actor => actor.Name, StringComparer.Ordinal)
                    .Select(actor => CreateExportActorData(actor, colorContours))
                    .ToArray();
            }
        }

        public bool TryGetScalarRange(ISet<string> hiddenActorNames, IDictionary<string, bool> colorContours,
                                      out double minimum, out double maximum)
        {
            minimum = double.PositiveInfinity;
            maximum = double.NegativeInfinity;
            lock (_gate)
            {
                foreach (CapturedActor actor in _actors.Values)
                {
                    if (hiddenActorNames != null && hiddenActorNames.Contains(actor.Name)) continue;
                    if (colorContours == null || !colorContours.TryGetValue(actor.Name, out bool contours) || !contours)
                        continue;
                    foreach (float value in actor.Scalars)
                    {
                        if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                        if (value < minimum) minimum = value;
                        if (value > maximum) maximum = value;
                    }
                }
            }
            return !double.IsInfinity(minimum) && !double.IsInfinity(maximum);
        }

        private static vtkMaxActorData CreateExportActorData(CapturedActor actor,
                                                              IDictionary<string, bool> colorContours)
        {
            bool contours = colorContours != null && colorContours.TryGetValue(actor.Name, out bool enabled) && enabled;
            var data = new vtkMaxActorData
            {
                Name = actor.Name,
                IsAPart = true,
                Color = Color.FromArgb(actor.Argb),
                ColorTable = new[] { Color.FromArgb(actor.Argb), Color.Black, Color.Black },
                ColorContours = contours,
                Geometry = new PartExchangeData(),
                ModelEdges = actor.ModelEdges,
                ElementEdges = null,
                CellLocator = CreateLocatorData(actor)
            };
            int pointCount = actor.Coordinates.Length / 3;
            data.Geometry.Nodes.Coor = new double[pointCount][];
            data.Geometry.Nodes.Ids = (int[])actor.SourcePointIds.Clone();
            for (int point = 0; point < pointCount; point++)
            {
                data.Geometry.Nodes.Coor[point] = new[]
                {
                    actor.Coordinates[point * 3],
                    actor.Coordinates[point * 3 + 1],
                    actor.Coordinates[point * 3 + 2]
                };
            }
            data.Geometry.Nodes.Values = contours ? (float[])actor.Scalars.Clone() : null;
            data.Geometry.Cells.CellNodeIds = new int[actor.SourceElementIds.Length][];
            data.Geometry.Cells.Ids = (int[])actor.SourceElementIds.Clone();
            data.Geometry.Cells.Types = Enumerable.Repeat(5, actor.SourceElementIds.Length).ToArray();
            for (int triangle = 0; triangle < actor.SourceElementIds.Length; triangle++)
            {
                data.Geometry.Cells.CellNodeIds[triangle] = new[]
                {
                    checked((int)actor.Connectivity[triangle * 3]),
                    checked((int)actor.Connectivity[triangle * 3 + 1]),
                    checked((int)actor.Connectivity[triangle * 3 + 2])
                };
            }
            return data;
        }

        private static PartExchangeData CreateLocatorData(CapturedActor actor)
        {
            if (actor.LocatorNodeIds.Length == 0) return null;
            var locator = new PartExchangeData();
            locator.Nodes.Ids = (int[])actor.LocatorNodeIds.Clone();
            locator.Nodes.Coor = new double[actor.LocatorNodeIds.Length][];
            for (int point = 0; point < locator.Nodes.Coor.Length; point++)
            {
                locator.Nodes.Coor[point] = new[] { actor.LocatorCoordinates[point * 3],
                    actor.LocatorCoordinates[point * 3 + 1], actor.LocatorCoordinates[point * 3 + 2] };
            }
            locator.Cells.Ids = (int[])actor.LocatorElementIds.Clone();
            locator.Cells.Types = (int[])actor.LocatorCellTypes.Clone();
            locator.Cells.CellNodeIds = actor.LocatorCells.Select(cell => (int[])cell.Clone()).ToArray();
            return locator;
        }

        private static bool TryCapture(vtkMaxActorData actorData, NodesExchangeData frameNodes,
                                       NodesExchangeData locatorFrameNodes,
                                       bool allowNonPart,
                                       out CapturedActor actor, out string diagnostic)
        {
            actor = null;
            diagnostic = null;
            PartExchangeData geometry = actorData == null ? null : actorData.Geometry;
            if (actorData == null) { diagnostic = "ignored null actor"; return false; }
            if (!actorData.IsAPart && !allowNonPart)
            {
                diagnostic = "ignored non-part actor='" + actorData.Name + "'";
                return false;
            }
            if (geometry == null || geometry.Nodes == null || geometry.Nodes.Coor == null ||
                geometry.Cells == null || geometry.Cells.CellNodeIds == null || geometry.Cells.Types == null)
            {
                diagnostic = "rejected part actor='" + actorData.Name + "': incomplete geometry";
                return false;
            }
            if (geometry.Cells.CellNodeIds.Length != geometry.Cells.Types.Length || geometry.Nodes.Coor.Length == 0)
            {
                diagnostic = "rejected part actor='" + actorData.Name + "': points=" + geometry.Nodes.Coor.Length +
                             " cells=" + geometry.Cells.CellNodeIds.Length + " types=" + geometry.Cells.Types.Length;
                return false;
            }
            int[] unsupportedTypes = geometry.Cells.Types.Where(type =>
                type != 5 && type != 6 && type != 8 && type != 9 && type != 22 && type != 23)
                .Distinct().OrderBy(type => type).ToArray();
            if (unsupportedTypes.Length > 0)
            {
                diagnostic = "rejected part actor='" + actorData.Name +
                             "': unsupported render cell types=" + string.Join(",", unsupportedTypes);
                return false;
            }

            NodesExchangeData renderNodes = frameNodes ?? geometry.Nodes;
            if (renderNodes == null || renderNodes.Coor == null ||
                renderNodes.Coor.Length != geometry.Nodes.Coor.Length ||
                (renderNodes.Values != null && renderNodes.Values.Length != renderNodes.Coor.Length))
            {
                diagnostic = "rejected part actor='" + actorData.Name + "': inconsistent frame arrays";
                return false;
            }

            double[] coordinates = new double[checked(renderNodes.Coor.Length * 3)];
            int[] sourcePointIds = new int[renderNodes.Coor.Length];
            float[] scalars = new float[renderNodes.Coor.Length];
            for (int point = 0; point < renderNodes.Coor.Length; point++)
            {
                double[] coordinate = renderNodes.Coor[point];
                if (coordinate == null || coordinate.Length < 3 ||
                    double.IsNaN(coordinate[0]) || double.IsInfinity(coordinate[0]) ||
                    double.IsNaN(coordinate[1]) || double.IsInfinity(coordinate[1]) ||
                    double.IsNaN(coordinate[2]) || double.IsInfinity(coordinate[2]))
                {
                    diagnostic = "rejected part actor='" + actorData.Name + "': invalid point " + point;
                    return false;
                }
                coordinates[point * 3] = coordinate[0];
                coordinates[point * 3 + 1] = coordinate[1];
                coordinates[point * 3 + 2] = coordinate[2];
                sourcePointIds[point] = geometry.Nodes.Ids != null && geometry.Nodes.Ids.Length == renderNodes.Coor.Length
                    ? geometry.Nodes.Ids[point]
                    : point + 1;
                scalars[point] = renderNodes.Values != null
                    ? renderNodes.Values[point]
                    : float.NaN;
            }

            var connectivity = new List<uint>();
            var sourceElementIds = new List<int>();
            for (int cell = 0; cell < geometry.Cells.CellNodeIds.Length; cell++)
            {
                int[] nodes = geometry.Cells.CellNodeIds[cell];
                int sourceElementId = geometry.Cells.Ids != null && geometry.Cells.Ids.Length == geometry.Cells.CellNodeIds.Length
                    ? geometry.Cells.Ids[cell]
                    : cell + 1;
                switch (geometry.Cells.Types[cell])
                {
                    case 5: // VTK_TRIANGLE
                        AddTriangle(nodes, 0, 1, 2, sourceElementId, coordinates, connectivity, sourceElementIds);
                        break;
                    case 6: // VTK_TRIANGLE_STRIP
                        if (nodes == null || nodes.Length < 3)
                            throw new InvalidDataException("A triangle strip must contain at least three points.");
                        for (int index = 0; index < nodes.Length - 2; index++)
                        {
                            if ((index & 1) == 0)
                                AddTriangle(nodes, index, index + 1, index + 2, sourceElementId,
                                            coordinates, connectivity, sourceElementIds);
                            else
                                AddTriangle(nodes, index + 1, index, index + 2, sourceElementId,
                                            coordinates, connectivity, sourceElementIds);
                        }
                        break;
                    case 8: // VTK_PIXEL: 0,1,3,2 is the perimeter order
                        AddTriangle(nodes, 0, 1, 3, sourceElementId, coordinates, connectivity, sourceElementIds);
                        AddTriangle(nodes, 0, 3, 2, sourceElementId, coordinates, connectivity, sourceElementIds);
                        break;
                    case 9: // VTK_QUAD
                        AddTriangle(nodes, 0, 1, 2, sourceElementId, coordinates, connectivity, sourceElementIds);
                        AddTriangle(nodes, 0, 2, 3, sourceElementId, coordinates, connectivity, sourceElementIds);
                        break;
                    case 22: // VTK_QUADRATIC_TRIANGLE
                        AddTriangle(nodes, 0, 3, 5, sourceElementId, coordinates, connectivity, sourceElementIds);
                        AddTriangle(nodes, 3, 4, 5, sourceElementId, coordinates, connectivity, sourceElementIds);
                        AddTriangle(nodes, 3, 1, 4, sourceElementId, coordinates, connectivity, sourceElementIds);
                        AddTriangle(nodes, 5, 4, 2, sourceElementId, coordinates, connectivity, sourceElementIds);
                        break;
                    case 23: // VTK_QUADRATIC_QUAD
                        AddQuadraticQuad(nodes, sourceElementId, coordinates, connectivity, sourceElementIds);
                        break;
                }
            }
            if (sourceElementIds.Count == 0)
            {
                diagnostic = "rejected part actor='" + actorData.Name + "': no supported surface cells; types=" +
                             string.Join(",", geometry.Cells.Types.Distinct().OrderBy(value => value));
                return false;
            }

            double[] locatorCoordinates = new double[0];
            int[] locatorNodeIds = new int[0];
            int[] locatorElementIds = new int[0];
            int[] locatorCellTypes = new int[0];
            int[][] locatorCells = new int[0][];
            PartExchangeData locator = actorData.CellLocator;
            NodesExchangeData activeLocatorNodes = locatorFrameNodes ?? (locator == null ? null : locator.Nodes);
            if (locator != null && locator.Nodes != null && locator.Cells != null &&
                locator.Nodes.Ids != null && activeLocatorNodes != null && activeLocatorNodes.Coor != null && locator.Cells.Ids != null &&
                locator.Cells.Types != null && locator.Cells.CellNodeIds != null)
            {
                if (locator.Nodes.Ids.Length != activeLocatorNodes.Coor.Length ||
                    locator.Cells.Ids.Length != locator.Cells.Types.Length ||
                    locator.Cells.Ids.Length != locator.Cells.CellNodeIds.Length)
                {
                    diagnostic = "rejected part actor='" + actorData.Name + "': inconsistent locator arrays";
                    return false;
                }
                locatorCoordinates = new double[checked(activeLocatorNodes.Coor.Length * 3)];
                locatorNodeIds = (int[])locator.Nodes.Ids.Clone();
                for (int point = 0; point < activeLocatorNodes.Coor.Length; point++)
                {
                    double[] coordinate = activeLocatorNodes.Coor[point];
                    if (coordinate == null || coordinate.Length < 3 || coordinate.Take(3).Any(value =>
                        double.IsNaN(value) || double.IsInfinity(value)))
                    {
                        diagnostic = "rejected part actor='" + actorData.Name + "': invalid locator point";
                        return false;
                    }
                    Array.Copy(coordinate, 0, locatorCoordinates, point * 3, 3);
                }
                locatorElementIds = (int[])locator.Cells.Ids.Clone();
                locatorCellTypes = (int[])locator.Cells.Types.Clone();
                locatorCells = new int[locator.Cells.CellNodeIds.Length][];
                for (int cell = 0; cell < locatorCells.Length; cell++)
                {
                    int[] cellNodes = locator.Cells.CellNodeIds[cell];
                    if (cellNodes == null || cellNodes.Length < 1 || cellNodes.Length > 27 ||
                        cellNodes.Any(node => node < 0 || node >= locatorNodeIds.Length))
                    {
                        diagnostic = "rejected part actor='" + actorData.Name + "': invalid locator connectivity";
                        return false;
                    }
                    locatorCells[cell] = (int[])cellNodes.Clone();
                }
            }

            Color color = actorData.Color.IsEmpty ? Color.FromArgb(255, 178, 178, 191) : actorData.Color;
            actor = new CapturedActor(
                string.IsNullOrWhiteSpace(actorData.Name) ? "PrePoMax part" : actorData.Name,
                color.ToArgb(),
                actorData.Pickable,
                actorData.BackfaceCulling,
                actorData.SmoothShaded,
                actorData.SectionViewPossible,
                ClampUnit(actorData.Ambient),
                ClampUnit(actorData.Diffuse),
                actorData.LineWidth > 0 ? actorData.LineWidth : 1,
                actorData.NodeSize > 0 ? actorData.NodeSize : 1,
                coordinates,
                sourcePointIds,
                scalars,
                connectivity.ToArray(),
                sourceElementIds.ToArray(),
                locatorCoordinates, locatorNodeIds, locatorElementIds, locatorCellTypes, locatorCells,
                actorData.ModelEdges);
            return true;
        }

        private static void AddQuadraticQuad(
            int[] nodes,
            int sourceElementId,
            double[] coordinates,
            List<uint> connectivity,
            List<int> sourceElementIds)
        {
            if (nodes == null || nodes.Length < 8) throw new InvalidDataException("A quadratic quad has fewer than eight points.");
            double diagonal46 = DistanceSquared(nodes[4], nodes[6], coordinates);
            double diagonal57 = DistanceSquared(nodes[5], nodes[7], coordinates);
            int[,] triangles = diagonal46 < diagonal57
                ? new int[,] { { 7, 0, 4 }, { 7, 4, 6 }, { 7, 6, 3 }, { 5, 4, 1 }, { 5, 6, 4 }, { 5, 2, 6 } }
                : new int[,] { { 4, 7, 0 }, { 4, 5, 7 }, { 4, 1, 5 }, { 6, 5, 2 }, { 6, 7, 5 }, { 6, 3, 7 } };
            for (int triangle = 0; triangle < triangles.GetLength(0); triangle++)
                AddTriangle(nodes, triangles[triangle, 0], triangles[triangle, 1], triangles[triangle, 2],
                            sourceElementId, coordinates, connectivity, sourceElementIds);
        }

        private static void AddTriangle(
            int[] nodes,
            int first,
            int second,
            int third,
            int sourceElementId,
            double[] coordinates,
            List<uint> connectivity,
            List<int> sourceElementIds)
        {
            if (nodes == null || first >= nodes.Length || second >= nodes.Length || third >= nodes.Length)
                throw new InvalidDataException("A surface cell has insufficient connectivity.");
            int pointCount = coordinates.Length / 3;
            int a = nodes[first];
            int b = nodes[second];
            int c = nodes[third];
            if (a < 0 || a >= pointCount || b < 0 || b >= pointCount || c < 0 || c >= pointCount)
                throw new InvalidDataException("A surface cell references a missing local point.");
            connectivity.Add((uint)a);
            connectivity.Add((uint)b);
            connectivity.Add((uint)c);
            sourceElementIds.Add(sourceElementId);
        }

        private static double DistanceSquared(int first, int second, double[] coordinates)
        {
            int pointCount = coordinates.Length / 3;
            if (first < 0 || first >= pointCount || second < 0 || second >= pointCount)
                throw new InvalidDataException("A quadratic surface references a missing local point.");
            double dx = coordinates[first * 3] - coordinates[second * 3];
            double dy = coordinates[first * 3 + 1] - coordinates[second * 3 + 1];
            double dz = coordinates[first * 3 + 2] - coordinates[second * 3 + 2];
            return dx * dx + dy * dy + dz * dz;
        }

        private static double ClampUnit(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            return Math.Max(0, Math.Min(1, value));
        }

        internal sealed class CapturedActor
        {
            public CapturedActor(string name, int argb, bool pickable, bool backfaceCulling, bool smoothShading,
                                 bool sectionViewPossible,
                                 double ambient, double diffuse, float lineWidth, float pointSize,
                                 double[] coordinates, int[] sourcePointIds, float[] scalars,
                                 uint[] connectivity, int[] sourceElementIds, double[] locatorCoordinates,
                                 int[] locatorNodeIds, int[] locatorElementIds, int[] locatorCellTypes,
                                 int[][] locatorCells, PartExchangeData modelEdges)
            {
                Name = name;
                Argb = argb;
                Pickable = pickable;
                BackfaceCulling = backfaceCulling;
                SmoothShading = smoothShading;
                SectionViewPossible = sectionViewPossible;
                Ambient = ambient;
                Diffuse = diffuse;
                LineWidth = lineWidth;
                PointSize = pointSize;
                Coordinates = coordinates;
                SourcePointIds = sourcePointIds;
                Scalars = scalars;
                Connectivity = connectivity;
                SourceElementIds = sourceElementIds;
                LocatorCoordinates = locatorCoordinates;
                LocatorNodeIds = locatorNodeIds;
                LocatorElementIds = locatorElementIds;
                LocatorCellTypes = locatorCellTypes;
                LocatorCells = locatorCells;
                ModelEdges = modelEdges;
            }

            public string Name { get; private set; }
            public int Argb { get; private set; }
            public bool Pickable { get; private set; }
            public bool BackfaceCulling { get; private set; }
            public bool SmoothShading { get; private set; }
            public bool SectionViewPossible { get; private set; }
            public double Ambient { get; private set; }
            public double Diffuse { get; private set; }
            public float LineWidth { get; private set; }
            public float PointSize { get; private set; }
            public double[] Coordinates { get; private set; }
            public int[] SourcePointIds { get; private set; }
            public float[] Scalars { get; private set; }
            public uint[] Connectivity { get; private set; }
            public int[] SourceElementIds { get; private set; }
            public double[] LocatorCoordinates { get; private set; }
            public int[] LocatorNodeIds { get; private set; }
            public int[] LocatorElementIds { get; private set; }
            public int[] LocatorCellTypes { get; private set; }
            public int[][] LocatorCells { get; private set; }
            public PartExchangeData ModelEdges { get; private set; }

            public CapturedActor WithPresentation(string name, int argb)
            {
                return new CapturedActor(
                    name, argb, Pickable, BackfaceCulling, SmoothShading, SectionViewPossible,
                    Ambient, Diffuse, LineWidth, PointSize,
                    Coordinates, SourcePointIds, Scalars, Connectivity, SourceElementIds,
                    LocatorCoordinates, LocatorNodeIds, LocatorElementIds, LocatorCellTypes, LocatorCells, ModelEdges);
            }

            public CapturedActor WithScalars(float[] scalars)
            {
                return new CapturedActor(
                    Name, Argb, Pickable, BackfaceCulling, SmoothShading, SectionViewPossible,
                    Ambient, Diffuse, LineWidth, PointSize,
                    Coordinates, SourcePointIds, scalars, Connectivity, SourceElementIds,
                    LocatorCoordinates, LocatorNodeIds, LocatorElementIds, LocatorCellTypes, LocatorCells, ModelEdges);
            }

            public CapturedActor WithFrame(double[] coordinates, float[] scalars, double[] locatorCoordinates)
            {
                return new CapturedActor(
                    Name, Argb, Pickable, BackfaceCulling, SmoothShading, SectionViewPossible,
                    Ambient, Diffuse, LineWidth, PointSize,
                    coordinates, SourcePointIds, scalars, Connectivity, SourceElementIds,
                    locatorCoordinates, LocatorNodeIds, LocatorElementIds, LocatorCellTypes, LocatorCells, ModelEdges);
            }
        }
    }

    internal sealed class VtkJsFrameSnapshot
    {
        private const long MaximumUploadBytes = 256L * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PMXFRM2\0");
        private readonly VtkJsSceneCapture.CapturedActor[] _actors;
        private readonly double[] _origin;

        public VtkJsFrameSnapshot(VtkJsSceneCapture.CapturedActor[] actors)
        {
            _actors = actors;
            if (actors.Length < 1 || actors.Length > 512)
                throw new InvalidOperationException("A frame update must contain 1 through 512 actors.");
            _origin = CalculateOrigin(actors);
            long length = Magic.Length + sizeof(int) + 3 * sizeof(double);
            long renderPoints = 0;
            long locatorPoints = 0;
            foreach (VtkJsSceneCapture.CapturedActor actor in actors)
            {
                int nameBytes = Encoding.UTF8.GetByteCount(actor.Name);
                if (nameBytes < 1 || nameBytes > 1024)
                    throw new InvalidOperationException("A frame actor name exceeds the 1024-byte bound.");
                renderPoints = checked(renderPoints + actor.SourcePointIds.LongLength);
                locatorPoints = checked(locatorPoints + actor.LocatorNodeIds.LongLength);
                length = checked(length + sizeof(int) + nameBytes + sizeof(int) +
                                 actor.SourcePointIds.LongLength * 4 * sizeof(float) +
                                 sizeof(int) + actor.LocatorNodeIds.LongLength * 3 * sizeof(float));
            }
            if (renderPoints > 5_000_000 || locatorPoints > 5_000_000 || length > MaximumUploadBytes)
                throw new InvalidOperationException("The animation frame exceeds the bounded host protocol size.");
            ContentLength = length;
        }

        public int ActorCount { get { return _actors.Length; } }
        public long ContentLength { get; private set; }

        public void WriteTo(Stream destination)
        {
            var buffered = new BufferedStream(destination, 1024 * 1024);
            using (var writer = new BinaryWriter(buffered, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(_actors.Length);
                writer.Write(_origin[0]);
                writer.Write(_origin[1]);
                writer.Write(_origin[2]);
                foreach (VtkJsSceneCapture.CapturedActor actor in _actors)
                {
                    byte[] name = Encoding.UTF8.GetBytes(actor.Name);
                    writer.Write(name.Length);
                    writer.Write(name);
                    writer.Write(actor.SourcePointIds.Length);
                    for (int point = 0; point < actor.SourcePointIds.Length; point++)
                    {
                        writer.Write(OriginRelative(actor.Coordinates[point * 3], 0));
                        writer.Write(OriginRelative(actor.Coordinates[point * 3 + 1], 1));
                        writer.Write(OriginRelative(actor.Coordinates[point * 3 + 2], 2));
                        writer.Write(actor.Scalars[point]);
                    }
                    writer.Write(actor.LocatorNodeIds.Length);
                    for (int point = 0; point < actor.LocatorNodeIds.Length; point++)
                    {
                        writer.Write(OriginRelative(actor.LocatorCoordinates[point * 3], 0));
                        writer.Write(OriginRelative(actor.LocatorCoordinates[point * 3 + 1], 1));
                        writer.Write(OriginRelative(actor.LocatorCoordinates[point * 3 + 2], 2));
                    }
                }
            }
            buffered.Flush();
        }

        private float OriginRelative(double value, int component)
        {
            double relative = value - _origin[component];
            if (relative < -float.MaxValue || relative > float.MaxValue)
                throw new InvalidOperationException("An animation coordinate exceeds the Float32 origin-relative range.");
            return (float)relative;
        }

        private static double[] CalculateOrigin(IEnumerable<VtkJsSceneCapture.CapturedActor> actors)
        {
            var minimum = new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };
            var maximum = new[] { double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };
            foreach (VtkJsSceneCapture.CapturedActor actor in actors)
            {
                foreach (double[] coordinates in new[] { actor.Coordinates, actor.LocatorCoordinates })
                    for (int index = 0; index < coordinates.Length; index++)
                    {
                        int component = index % 3;
                        minimum[component] = Math.Min(minimum[component], coordinates[index]);
                        maximum[component] = Math.Max(maximum[component], coordinates[index]);
                    }
            }
            for (int component = 0; component < 3; component++)
            {
                if (double.IsInfinity(minimum[component])) minimum[component] = maximum[component] = 0;
                minimum[component] += (maximum[component] - minimum[component]) * 0.5;
            }
            return minimum;
        }
    }

    internal sealed class VtkJsScalarSnapshot
    {
        private const long MaximumUploadBytes = 32L * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PMXSCL1\0");
        private readonly VtkJsSceneCapture.CapturedActor[] _actors;

        public VtkJsScalarSnapshot(VtkJsSceneCapture.CapturedActor[] actors)
        {
            _actors = actors;
            if (actors.Length > 512)
                throw new InvalidOperationException("A scalar update exceeds the 512-actor bound.");
            long length = Magic.Length + sizeof(int);
            long totalValues = 0;
            foreach (VtkJsSceneCapture.CapturedActor actor in actors)
            {
                int nameBytes = Encoding.UTF8.GetByteCount(actor.Name);
                if (nameBytes < 1 || nameBytes > 1024)
                    throw new InvalidOperationException("A scalar-update actor name exceeds the 1024-byte bound.");
                totalValues = checked(totalValues + actor.Scalars.LongLength);
                length = checked(length + sizeof(int) + nameBytes + sizeof(int) + actor.Scalars.LongLength * sizeof(float));
            }
            if (totalValues > 5_000_000 || length > MaximumUploadBytes)
                throw new InvalidOperationException("A scalar update exceeds the bounded host protocol size.");
            ContentLength = length;
        }

        public int ActorCount { get { return _actors.Length; } }
        public long ContentLength { get; private set; }

        public void WriteTo(Stream destination)
        {
            var buffered = new BufferedStream(destination, 1024 * 1024);
            using (var writer = new BinaryWriter(buffered, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(_actors.Length);
                foreach (VtkJsSceneCapture.CapturedActor actor in _actors)
                {
                    byte[] name = Encoding.UTF8.GetBytes(actor.Name);
                    writer.Write(name.Length);
                    writer.Write(name);
                    writer.Write(actor.Scalars.Length);
                    foreach (float value in actor.Scalars) writer.Write(value);
                }
            }
            buffered.Flush();
        }
    }

    internal sealed class VtkJsSceneSnapshot
    {
        private const long MaximumUploadBytes = 256L * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PMXSCN2\0");
        private readonly VtkJsSceneCapture.CapturedActor[] _actors;

        public VtkJsSceneSnapshot(VtkJsSceneCapture.CapturedActor[] actors)
        {
            _actors = actors;
            if (actors.Length > 512)
                throw new InvalidOperationException("The captured PrePoMax scene exceeds the 512-actor MVP bound.");
            long length = Magic.Length + sizeof(int);
            long totalPoints = 0;
            long totalTriangles = 0;
            long totalLocatorPoints = 0;
            long totalLocatorCells = 0;
            foreach (VtkJsSceneCapture.CapturedActor actor in actors)
            {
                int nameBytes = Encoding.UTF8.GetByteCount(actor.Name);
                if (nameBytes < 1 || nameBytes > 1024)
                    throw new InvalidOperationException("A captured PrePoMax actor name exceeds the 1024-byte MVP bound.");
                totalPoints = checked(totalPoints + actor.SourcePointIds.LongLength);
                totalTriangles = checked(totalTriangles + actor.SourceElementIds.LongLength);
                totalLocatorPoints = checked(totalLocatorPoints + actor.LocatorNodeIds.LongLength);
                totalLocatorCells = checked(totalLocatorCells + actor.LocatorElementIds.LongLength);
                long locatorReferences = actor.LocatorCells.Sum(cell => (long)cell.Length);
                length = checked(length + sizeof(int) + nameBytes + 4 + 4 + 8 + 8 + 4 + 4 + 4 + 4 + 4 + 4 + 4);
                length = checked(length + actor.SourcePointIds.LongLength * 32);
                length = checked(length + actor.SourceElementIds.LongLength * 16);
                length = checked(length + actor.LocatorNodeIds.LongLength * 28);
                length = checked(length + actor.LocatorElementIds.LongLength * 12 + locatorReferences * 4);
            }
            if (totalPoints > 5_000_000 || totalTriangles > 10_000_000 ||
                totalLocatorPoints > 5_000_000 || totalLocatorCells > 5_000_000)
                throw new InvalidOperationException("The captured PrePoMax scene exceeds the MVP point or triangle bound.");
            if (length > MaximumUploadBytes)
                throw new InvalidOperationException("The captured PrePoMax scene exceeds the 256 MiB MVP upload bound.");
            ContentLength = length;
            LocatorPointCount = totalLocatorPoints;
            LocatorCellCount = totalLocatorCells;
        }

        public int ActorCount { get { return _actors.Length; } }
        public long ContentLength { get; private set; }
        public long LocatorPointCount { get; private set; }
        public long LocatorCellCount { get; private set; }

        public void WriteTo(Stream destination)
        {
            // HttpWebRequest exposes an unbuffered request stream. Buffering here avoids
            // millions of tiny socket writes while retaining bounded, streaming memory use.
            var buffered = new BufferedStream(destination, 1024 * 1024);
            using (var writer = new BinaryWriter(buffered, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(_actors.Length);
                foreach (VtkJsSceneCapture.CapturedActor actor in _actors)
                {
                    byte[] name = Encoding.UTF8.GetBytes(actor.Name);
                    writer.Write(name.Length);
                    writer.Write(name);
                    writer.Write(actor.Argb);
                    writer.Write(actor.Pickable ? (byte)1 : (byte)0);
                    writer.Write(actor.BackfaceCulling ? (byte)1 : (byte)0);
                    writer.Write(actor.SmoothShading ? (byte)1 : (byte)0);
                    // Zero retains the V2 default of section eligibility; one explicitly opts out.
                    writer.Write(actor.SectionViewPossible ? (byte)0 : (byte)1);
                    writer.Write(actor.Ambient);
                    writer.Write(actor.Diffuse);
                    writer.Write(actor.LineWidth);
                    writer.Write(actor.PointSize);
                    writer.Write(actor.SourcePointIds.Length);
                    writer.Write(actor.SourceElementIds.Length);
                    writer.Write(actor.LocatorNodeIds.Length);
                    writer.Write(actor.LocatorElementIds.Length);
                    writer.Write(actor.LocatorCells.Sum(cell => cell.Length));
                    for (int point = 0; point < actor.SourcePointIds.Length; point++)
                    {
                        writer.Write(actor.SourcePointIds[point]);
                        writer.Write(actor.Coordinates[point * 3]);
                        writer.Write(actor.Coordinates[point * 3 + 1]);
                        writer.Write(actor.Coordinates[point * 3 + 2]);
                        writer.Write(actor.Scalars[point]);
                    }
                    for (int triangle = 0; triangle < actor.SourceElementIds.Length; triangle++)
                    {
                        writer.Write(actor.SourceElementIds[triangle]);
                        writer.Write(actor.Connectivity[triangle * 3]);
                        writer.Write(actor.Connectivity[triangle * 3 + 1]);
                        writer.Write(actor.Connectivity[triangle * 3 + 2]);
                    }
                    for (int point = 0; point < actor.LocatorNodeIds.Length; point++)
                    {
                        writer.Write(actor.LocatorNodeIds[point]);
                        writer.Write(actor.LocatorCoordinates[point * 3]);
                        writer.Write(actor.LocatorCoordinates[point * 3 + 1]);
                        writer.Write(actor.LocatorCoordinates[point * 3 + 2]);
                    }
                    for (int cell = 0; cell < actor.LocatorElementIds.Length; cell++)
                    {
                        writer.Write(actor.LocatorElementIds[cell]);
                        writer.Write(actor.LocatorCellTypes[cell]);
                        writer.Write(actor.LocatorCells[cell].Length);
                        foreach (int localPoint in actor.LocatorCells[cell]) writer.Write(localPoint);
                    }
                }
            }
            buffered.Flush();
        }
    }
}
