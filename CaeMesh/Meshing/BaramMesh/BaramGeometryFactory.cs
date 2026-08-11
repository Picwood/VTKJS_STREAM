// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace CaeMesh.Meshing.BaramMesh
{
    public static class BaramGeometryFactory
    {
        public const string PrePoMaxModelPath = "prepomax://model";
        public const string PrePoMaxGeometryPartPathPrefix = "prepomax://geometry/";
        public const string ExternalStlPathPrefix = "file://";

        public static int SeedModelGeometry(BaramMeshConfiguration configuration, string baseName = "PrePoMax_Model")
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            int volumeId = GetNextGeometryId(configuration);
            string volumeName = GetUniqueGeometryName(configuration, baseName);
            configuration.Geometry.Add(volumeId, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Volume,
                Name = volumeName,
                Shape = BaramShape.TriSurfaceMesh,
                CfdType = BaramCfdType.None,
                Path = PrePoMaxModelPath
            });

            int surfaceId = GetNextGeometryId(configuration);
            configuration.Geometry.Add(surfaceId, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Surface,
                VolumeId = volumeId,
                Name = GetUniqueGeometryName(configuration, volumeName + "_surface"),
                Shape = BaramShape.TriSurfaceMesh,
                CfdType = BaramCfdType.Boundary,
                Path = PrePoMaxModelPath
            });

            return volumeId;
        }

        public static int AddPrePoMaxGeometryPart(BaramMeshConfiguration configuration, string partName)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (string.IsNullOrWhiteSpace(partName)) throw new ArgumentException("Part name is required.", "partName");

            string volumeName = GetUniqueGeometryName(configuration, SanitizeGeometryName(partName));
            int volumeId = GetNextGeometryId(configuration);
            configuration.Geometry.Add(volumeId, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Volume,
                Name = volumeName,
                Shape = BaramShape.TriSurfaceMesh,
                CfdType = BaramCfdType.None,
                Path = GetPrePoMaxGeometryPartPath(partName)
            });

            int surfaceId = GetNextGeometryId(configuration);
            configuration.Geometry.Add(surfaceId, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Surface,
                VolumeId = volumeId,
                Name = GetUniqueGeometryName(configuration, volumeName + "_surface"),
                Shape = BaramShape.TriSurfaceMesh,
                CfdType = BaramCfdType.Boundary,
                Path = GetPrePoMaxGeometryPartPath(partName)
            });

            return volumeId;
        }

        public static int AddImportedStlSurface(BaramMeshConfiguration configuration, string fileName)
        {
            return AddImportedStlSurface(configuration, fileName, System.IO.Path.GetFileNameWithoutExtension(fileName));
        }

        public static int AddImportedStlSurface(BaramMeshConfiguration configuration, string fileName, string geometryName)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("STL file name is required.", "fileName");

            int surfaceId = GetNextGeometryId(configuration);
            configuration.Geometry.Add(surfaceId, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Surface,
                Name = GetUniqueGeometryName(configuration, SanitizeGeometryName(geometryName)),
                Shape = BaramShape.TriSurfaceMesh,
                CfdType = BaramCfdType.Boundary,
                Path = ExternalStlPathPrefix + fileName
            });
            return surfaceId;
        }

        public static int AddHex6FromBounds(BaramMeshConfiguration configuration, BaramBounds bounds, string baseName = "Hex6")
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            int volumeId = GetNextGeometryId(configuration);
            string name = GetUniqueGeometryName(configuration, baseName);
            configuration.Geometry.Add(volumeId, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Volume,
                Name = name,
                Shape = BaramShape.Hex6,
                CfdType = BaramCfdType.None,
                Point1 = new BaramVector(bounds.XMin, bounds.YMin, bounds.ZMin),
                Point2 = new BaramVector(bounds.XMax, bounds.YMax, bounds.ZMax)
            });

            AddHex6Face(configuration, volumeId, name, BaramShape.XMin);
            AddHex6Face(configuration, volumeId, name, BaramShape.XMax);
            AddHex6Face(configuration, volumeId, name, BaramShape.YMin);
            AddHex6Face(configuration, volumeId, name, BaramShape.YMax);
            AddHex6Face(configuration, volumeId, name, BaramShape.ZMin);
            AddHex6Face(configuration, volumeId, name, BaramShape.ZMax);

            configuration.BaseGrid.BoundingHex6 = volumeId;
            return volumeId;
        }

        public static int SetBaseGridBounds(BaramMeshConfiguration configuration, BaramBounds bounds, string baseName = "BaseGrid")
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            BaramGeometry boundingHex6;
            if (configuration.BaseGrid.BoundingHex6.HasValue &&
                configuration.Geometry.TryGetValue(configuration.BaseGrid.BoundingHex6.Value, out boundingHex6) &&
                boundingHex6.GeometryType == BaramGeometryType.Volume &&
                boundingHex6.Shape == BaramShape.Hex6)
            {
                boundingHex6.Point1 = new BaramVector(bounds.XMin, bounds.YMin, bounds.ZMin);
                boundingHex6.Point2 = new BaramVector(bounds.XMax, bounds.YMax, bounds.ZMax);
                configuration.Geometry[configuration.BaseGrid.BoundingHex6.Value] = boundingHex6;
                EnsureHex6Faces(configuration, configuration.BaseGrid.BoundingHex6.Value, boundingHex6.Name);
                return configuration.BaseGrid.BoundingHex6.Value;
            }

            return AddHex6FromBounds(configuration, bounds, baseName);
        }

        public static int AddPlane(BaramMeshConfiguration configuration, BaramBounds bounds, string baseName = "Plane")
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            double xSize = Math.Abs(bounds.XMax - bounds.XMin);
            double ySize = Math.Abs(bounds.YMax - bounds.YMin);
            int id = GetNextGeometryId(configuration);
            configuration.Geometry.Add(id, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Surface,
                Name = GetUniqueGeometryName(configuration, baseName),
                Shape = BaramShape.Plane,
                CfdType = BaramCfdType.Boundary,
                Point1 = new BaramVector((bounds.XMin + bounds.XMax) / 2,
                                         (bounds.YMin + bounds.YMax) / 2,
                                         (bounds.ZMin + bounds.ZMax) / 2),
                PlaneNormal = new BaramVector(0, 0, 1),
                PlaneWidth = xSize > 0 ? xSize : 1,
                PlaneHeight = ySize > 0 ? ySize : 1
            });
            return id;
        }

        public static int AddRefinementBox(BaramMeshConfiguration configuration, BaramBounds bounds,
                                           string baseName = "RefinementBox")
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            int id = GetNextGeometryId(configuration);
            configuration.Geometry.Add(id, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Volume,
                Name = GetUniqueGeometryName(configuration, baseName),
                Shape = BaramShape.Hex,
                CfdType = BaramCfdType.None,
                Point1 = new BaramVector(bounds.XMin, bounds.YMin, bounds.ZMin),
                Point2 = new BaramVector(bounds.XMax, bounds.YMax, bounds.ZMax)
            });
            return id;
        }

        public static int AddRefinementSphere(BaramMeshConfiguration configuration, BaramBounds bounds,
                                              string baseName = "RefinementSphere")
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            double size = Math.Max(Math.Abs(bounds.XMax - bounds.XMin),
                                   Math.Max(Math.Abs(bounds.YMax - bounds.YMin),
                                            Math.Abs(bounds.ZMax - bounds.ZMin)));
            int id = GetNextGeometryId(configuration);
            configuration.Geometry.Add(id, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Volume,
                Name = GetUniqueGeometryName(configuration, baseName),
                Shape = BaramShape.Sphere,
                CfdType = BaramCfdType.None,
                Point1 = new BaramVector((bounds.XMin + bounds.XMax) / 2,
                                         (bounds.YMin + bounds.YMax) / 2,
                                         (bounds.ZMin + bounds.ZMax) / 2),
                Radius = size > 0 ? size / 4 : 1
            });
            return id;
        }

        public static int AddRefinementCylinder(BaramMeshConfiguration configuration, BaramBounds bounds,
                                                string baseName = "RefinementCylinder")
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            double xSize = Math.Abs(bounds.XMax - bounds.XMin);
            double ySize = Math.Abs(bounds.YMax - bounds.YMin);
            double zSize = Math.Abs(bounds.ZMax - bounds.ZMin);
            double radiusSize = Math.Min(xSize > 0 ? xSize : double.MaxValue,
                                         ySize > 0 ? ySize : double.MaxValue);
            if (double.IsInfinity(radiusSize) || radiusSize == double.MaxValue) radiusSize = Math.Max(xSize, ySize);
            int id = GetNextGeometryId(configuration);
            configuration.Geometry.Add(id, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Volume,
                Name = GetUniqueGeometryName(configuration, baseName),
                Shape = BaramShape.Cylinder,
                CfdType = BaramCfdType.None,
                Point1 = new BaramVector((bounds.XMin + bounds.XMax) / 2,
                                         (bounds.YMin + bounds.YMax) / 2,
                                         bounds.ZMin),
                Point2 = new BaramVector((bounds.XMin + bounds.XMax) / 2,
                                         (bounds.YMin + bounds.YMax) / 2,
                                         zSize > 0 ? bounds.ZMax : bounds.ZMin + 1),
                Radius = radiusSize > 0 ? radiusSize / 4 : 1
            });
            return id;
        }

        public static int GetNextGeometryId(BaramMeshConfiguration configuration)
        {
            int nextId = 1;
            foreach (int id in configuration.Geometry.Keys)
            {
                if (id >= nextId) nextId = id + 1;
            }
            return nextId;
        }

        public static string GetUniqueGeometryName(BaramMeshConfiguration configuration, string baseName)
        {
            baseName = SanitizeGeometryName(baseName);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "Geometry";

            string name = baseName;
            int suffix = 1;
            while (GeometryNameExists(configuration, name))
            {
                name = baseName + "_" + suffix.ToString();
                suffix++;
            }
            return name;
        }

        public static string GetPrePoMaxGeometryPartPath(string partName)
        {
            return PrePoMaxGeometryPartPathPrefix + Uri.EscapeDataString(partName);
        }

        public static bool TryGetPrePoMaxGeometryPartName(string path, out string partName)
        {
            partName = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!path.StartsWith(PrePoMaxGeometryPartPathPrefix, StringComparison.OrdinalIgnoreCase)) return false;
            partName = Uri.UnescapeDataString(path.Substring(PrePoMaxGeometryPartPathPrefix.Length));
            return !string.IsNullOrWhiteSpace(partName);
        }

        public static bool TryGetExternalStlFileName(string path, out string fileName)
        {
            fileName = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!path.StartsWith(ExternalStlPathPrefix, StringComparison.OrdinalIgnoreCase)) return false;
            fileName = path.Substring(ExternalStlPathPrefix.Length);
            return !string.IsNullOrWhiteSpace(fileName);
        }

        public static string SanitizeGeometryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Geometry";

            char[] chars = name.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_') chars[i] = '_';
            }
            return new string(chars);
        }

        private static void AddHex6Face(BaramMeshConfiguration configuration, int volumeId, string volumeName,
                                        BaramShape shape)
        {
            int id = GetNextGeometryId(configuration);
            configuration.Geometry.Add(id, new BaramGeometry
            {
                GeometryType = BaramGeometryType.Surface,
                VolumeId = volumeId,
                Name = GetUniqueGeometryName(configuration, volumeName + "_" + shape.ToBaramString()),
                Shape = shape,
                CfdType = BaramCfdType.Boundary
            });
        }

        private static void EnsureHex6Faces(BaramMeshConfiguration configuration, int volumeId, string volumeName)
        {
            EnsureHex6Face(configuration, volumeId, volumeName, BaramShape.XMin);
            EnsureHex6Face(configuration, volumeId, volumeName, BaramShape.XMax);
            EnsureHex6Face(configuration, volumeId, volumeName, BaramShape.YMin);
            EnsureHex6Face(configuration, volumeId, volumeName, BaramShape.YMax);
            EnsureHex6Face(configuration, volumeId, volumeName, BaramShape.ZMin);
            EnsureHex6Face(configuration, volumeId, volumeName, BaramShape.ZMax);
        }

        private static void EnsureHex6Face(BaramMeshConfiguration configuration, int volumeId, string volumeName,
                                           BaramShape shape)
        {
            foreach (BaramGeometry geometry in configuration.Geometry.Values)
            {
                if (geometry.GeometryType == BaramGeometryType.Surface &&
                    geometry.VolumeId.HasValue &&
                    geometry.VolumeId.Value == volumeId &&
                    geometry.Shape == shape)
                    return;
            }
            AddHex6Face(configuration, volumeId, volumeName, shape);
        }

        private static bool GeometryNameExists(BaramMeshConfiguration configuration, string name)
        {
            foreach (BaramGeometry geometry in configuration.Geometry.Values)
            {
                if (string.Equals(geometry.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
