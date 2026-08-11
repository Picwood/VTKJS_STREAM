// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using QuantumConcepts.Formats.StereoLithography;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PrePoMax.Forms
{
    class BaramStlFeatureSplitter
    {
        private readonly List<Triangle> _triangles;
        private readonly Dictionary<EdgeKey, List<int>> _edgeTriangles;
        private readonly string _baseName;

        public BaramStlFeatureSplitter(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("STL file name is required.", "fileName");
            if (!File.Exists(fileName)) throw new FileNotFoundException("STL file does not exist.", fileName);

            _triangles = new List<Triangle>();
            _edgeTriangles = new Dictionary<EdgeKey, List<int>>();
            _baseName = BaramGeometryFactory.SanitizeGeometryName(Path.GetFileNameWithoutExtension(fileName));

            Load(fileName);
        }

        public List<BaramStlSplitSegment> Split(double featureAngleDegrees, double minAreaPercent)
        {
            featureAngleDegrees = Math.Max(1, Math.Min(90, featureAngleDegrees));
            minAreaPercent = Math.Max(0, Math.Min(100, minAreaPercent));

            int[] parent = new int[_triangles.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            double cosLimit = Math.Cos(featureAngleDegrees * Math.PI / 180.0);
            foreach (List<int> triangleIds in _edgeTriangles.Values)
            {
                if (triangleIds.Count != 2) continue;
                Triangle a = _triangles[triangleIds[0]];
                Triangle b = _triangles[triangleIds[1]];
                double dot = Math.Abs(Dot(a.Normal, b.Normal));
                dot = Math.Max(-1, Math.Min(1, dot));
                if (dot >= cosLimit) Union(parent, triangleIds[0], triangleIds[1]);
            }

            Dictionary<int, SegmentBuild> builds = BuildSegments(parent);
            AssimilateSmallSegments(builds, minAreaPercent);

            List<BaramStlSplitSegment> segments = new List<BaramStlSplitSegment>();
            int id = 0;
            double totalArea = _triangles.Sum(t => t.Area);
            foreach (SegmentBuild build in builds.Values.Where(b => b.TriangleIds.Count > 0).OrderByDescending(b => b.Area))
            {
                string defaultName = BaramGeometryFactory.SanitizeGeometryName(_baseName + "_" + id.ToString(CultureInfo.InvariantCulture));
                segments.Add(new BaramStlSplitSegment(id, defaultName,
                                                       totalArea > 0 ? 100.0 * build.Area / totalArea : 0,
                                                       build.TriangleIds.Select(i => _triangles[i].Facet).ToList()));
                id++;
            }
            return segments;
        }

        public static List<AssignedSplitStlFile> WriteAssignedSegments(IEnumerable<BaramStlSplitSegment> segments, string outputDirectory)
        {
            if (segments == null) throw new ArgumentNullException("segments");
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", "outputDirectory");

            Directory.CreateDirectory(outputDirectory);
            Dictionary<string, List<Facet>> nameFacets = new Dictionary<string, List<Facet>>(StringComparer.OrdinalIgnoreCase);
            foreach (BaramStlSplitSegment segment in segments)
            {
                string name = BaramGeometryFactory.SanitizeGeometryName(segment.SurfaceName);
                if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Every segment must have a surface name.");
                List<Facet> facets;
                if (!nameFacets.TryGetValue(name, out facets))
                {
                    facets = new List<Facet>();
                    nameFacets.Add(name, facets);
                }
                facets.AddRange(segment.Facets);
            }

            List<AssignedSplitStlFile> files = new List<AssignedSplitStlFile>();
            foreach (KeyValuePair<string, List<Facet>> entry in nameFacets)
            {
                string fileName = Path.Combine(outputDirectory, entry.Key + ".stl");
                STLDocument stl = new STLDocument(entry.Key, entry.Value);
                stl.SaveAsText(fileName);
                files.Add(new AssignedSplitStlFile(entry.Key, fileName));
            }
            return files;
        }
        public static List<AssignedSplitStlFile> WriteSplitSegments(IEnumerable<BaramStlSplitSegment> segments, string outputDirectory)
        {
            if (segments == null) throw new ArgumentNullException("segments");
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", "outputDirectory");
            //
            Directory.CreateDirectory(outputDirectory);
            List<AssignedSplitStlFile> files = new List<AssignedSplitStlFile>();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BaramStlSplitSegment segment in segments)
            {
                string name = BaramGeometryFactory.SanitizeGeometryName(segment.SurfaceName);
                if (string.IsNullOrWhiteSpace(name)) name = "segment_" + segment.Id.ToString(CultureInfo.InvariantCulture);
                name = GetUniqueFileBaseName(name, names);
                //
                string fileName = Path.Combine(outputDirectory, name + ".stl");
                STLDocument stl = new STLDocument(name, segment.Facets);
                stl.SaveAsText(fileName);
                files.Add(new AssignedSplitStlFile(name, fileName));
            }
            return files;
        }
        public static AssignedSplitStlFile WriteMergedStlFiles(IEnumerable<string> fileNames, string name, string outputDirectory)
        {
            if (fileNames == null) throw new ArgumentNullException("fileNames");
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", "outputDirectory");
            //
            name = BaramGeometryFactory.SanitizeGeometryName(name);
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Surface name is required.");
            Directory.CreateDirectory(outputDirectory);
            //
            List<Facet> facets = new List<Facet>();
            foreach (string fileName in fileNames)
            {
                if (!File.Exists(fileName)) throw new FileNotFoundException("The STL file does not exist.", fileName);
                STLDocument document;
                using (Stream stream = File.OpenRead(fileName)) document = STLDocument.Read(stream, true);
                if (document != null && document.Facets != null) facets.AddRange(document.Facets);
            }
            if (facets.Count == 0) throw new InvalidOperationException("Selected STL files do not contain facets.");
            //
            string target = Path.Combine(outputDirectory, name + ".stl");
            STLDocument stl = new STLDocument(name, facets);
            stl.SaveAsText(target);
            return new AssignedSplitStlFile(name, target);
        }

        private static string GetUniqueFileBaseName(string baseName, HashSet<string> names)
        {
            string name = baseName;
            int suffix = 1;
            while (names.Contains(name))
            {
                name = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            names.Add(name);
            return name;
        }

        private void Load(string fileName)
        {
            STLDocument document;
            using (Stream stream = File.OpenRead(fileName))
                document = STLDocument.Read(stream, true);
            if (document == null || document.Facets == null || document.Facets.Count == 0)
                throw new InvalidOperationException("The STL file does not contain facets.");

            Dictionary<VertexKey, int> pointIds = new Dictionary<VertexKey, int>();
            foreach (Facet facet in document.Facets)
            {
                if (facet.Vertices == null || facet.Vertices.Count != 3) continue;
                int[] ids = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    Vertex vertex = facet.Vertices[i];
                    VertexKey key = new VertexKey(vertex.X, vertex.Y, vertex.Z);
                    int id;
                    if (!pointIds.TryGetValue(key, out id))
                    {
                        id = pointIds.Count;
                        pointIds.Add(key, id);
                    }
                    ids[i] = id;
                }

                Triangle triangle = new Triangle(facet, ids);
                if (triangle.Area <= 0) continue;
                int triangleId = _triangles.Count;
                _triangles.Add(triangle);
                AddEdge(ids[0], ids[1], triangleId);
                AddEdge(ids[1], ids[2], triangleId);
                AddEdge(ids[2], ids[0], triangleId);
            }

            if (_triangles.Count == 0) throw new InvalidOperationException("The STL file does not contain valid triangles.");
        }

        private void AddEdge(int a, int b, int triangleId)
        {
            EdgeKey key = new EdgeKey(a, b);
            List<int> ids;
            if (!_edgeTriangles.TryGetValue(key, out ids))
            {
                ids = new List<int>();
                _edgeTriangles.Add(key, ids);
            }
            ids.Add(triangleId);
        }

        private Dictionary<int, SegmentBuild> BuildSegments(int[] parent)
        {
            Dictionary<int, SegmentBuild> builds = new Dictionary<int, SegmentBuild>();
            for (int i = 0; i < _triangles.Count; i++)
            {
                int root = Find(parent, i);
                SegmentBuild build;
                if (!builds.TryGetValue(root, out build))
                {
                    build = new SegmentBuild();
                    builds.Add(root, build);
                }
                build.TriangleIds.Add(i);
                build.Area += _triangles[i].Area;
            }
            return builds;
        }

        private void AssimilateSmallSegments(Dictionary<int, SegmentBuild> builds, double minAreaPercent)
        {
            if (minAreaPercent <= 0) return;
            double totalArea = builds.Values.Sum(b => b.Area);
            if (totalArea <= 0) return;
            double threshold = totalArea * minAreaPercent / 100.0;

            bool changed;
            do
            {
                changed = false;
                foreach (KeyValuePair<int, SegmentBuild> entry in builds.ToList())
                {
                    SegmentBuild small = entry.Value;
                    if (small.TriangleIds.Count == 0 || small.Area >= threshold) continue;
                    SegmentBuild target = FindLargestNeighbor(builds, entry.Key);
                    if (target == null) continue;
                    target.TriangleIds.AddRange(small.TriangleIds);
                    target.Area += small.Area;
                    small.TriangleIds.Clear();
                    small.Area = 0;
                    changed = true;
                }
            } while (changed);
        }

        private SegmentBuild FindLargestNeighbor(Dictionary<int, SegmentBuild> builds, int segmentKey)
        {
            HashSet<int> triangleIds = new HashSet<int>(builds[segmentKey].TriangleIds);
            SegmentBuild best = null;
            foreach (List<int> edgeTriangleIds in _edgeTriangles.Values)
            {
                bool touchesSmall = edgeTriangleIds.Any(id => triangleIds.Contains(id));
                if (!touchesSmall) continue;
                foreach (int triangleId in edgeTriangleIds)
                {
                    if (triangleIds.Contains(triangleId)) continue;
                    SegmentBuild candidate = builds.Values.FirstOrDefault(b => b.TriangleIds.Contains(triangleId));
                    if (candidate != null && candidate.TriangleIds.Count > 0 && (best == null || candidate.Area > best.Area))
                        best = candidate;
                }
            }
            return best;
        }

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }

        private static double Dot(double[] a, double[] b)
        {
            return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        private class SegmentBuild
        {
            public List<int> TriangleIds = new List<int>();
            public double Area;
        }

        private class Triangle
        {
            public Facet Facet;
            public int[] NodeIds;
            public double[] Normal;
            public double Area;

            public Triangle(Facet facet, int[] nodeIds)
            {
                Facet = facet;
                NodeIds = nodeIds;
                Normal = ComputeNormal(facet, out Area);
            }
        }

        private static double[] ComputeNormal(Facet facet, out double area)
        {
            Vertex a = facet.Vertices[0];
            Vertex b = facet.Vertices[1];
            Vertex c = facet.Vertices[2];
            double ux = b.X - a.X;
            double uy = b.Y - a.Y;
            double uz = b.Z - a.Z;
            double vx = c.X - a.X;
            double vy = c.Y - a.Y;
            double vz = c.Z - a.Z;
            double nx = uy * vz - uz * vy;
            double ny = uz * vx - ux * vz;
            double nz = ux * vy - uy * vx;
            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            area = length / 2.0;
            if (length <= 0) return new double[] { 0, 0, 0 };
            return new double[] { nx / length, ny / length, nz / length };
        }

        private struct VertexKey
        {
            private readonly float _x;
            private readonly float _y;
            private readonly float _z;

            public VertexKey(float x, float y, float z)
            {
                _x = x;
                _y = y;
                _z = z;
            }
        }

        private struct EdgeKey
        {
            private readonly int _a;
            private readonly int _b;

            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    _a = a;
                    _b = b;
                }
                else
                {
                    _a = b;
                    _b = a;
                }
            }
        }
    }

    class BaramStlSplitSegment
    {
        public int Id { get; private set; }
        public string SurfaceName { get; set; }
        public double AreaPercent { get; private set; }
        public List<Facet> Facets { get; private set; }
        public bool Assigned { get; set; }

        public BaramStlSplitSegment(int id, string surfaceName, double areaPercent, List<Facet> facets)
        {
            Id = id;
            SurfaceName = surfaceName;
            AreaPercent = areaPercent;
            Facets = facets;
        }
    }

    class AssignedSplitStlFile
    {
        public string Name { get; private set; }
        public string FileName { get; private set; }

        public AssignedSplitStlFile(string name, string fileName)
        {
            Name = name;
            FileName = fileName;
        }
    }
}
