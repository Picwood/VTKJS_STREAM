// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace CaeMesh.Meshing.BaramMesh
{
    [Serializable]
    public struct BaramBounds
    {
        public double XMin;
        public double XMax;
        public double YMin;
        public double YMax;
        public double ZMin;
        public double ZMax;

        public BaramBounds(double xMin, double xMax, double yMin, double yMax, double zMin, double zMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
            ZMin = zMin;
            ZMax = zMax;
        }
    }

    public class BaramBlockMeshDictBuilder
    {
        public OpenFoamDictionary Build(BaramMeshConfiguration configuration, BaramBounds bounds,
                                        IDictionary<BaramShape, string> boundaryNames = null)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            configuration.BaseGrid.Validate();

            Dictionary<BaramShape, string> names = CreateDefaultBoundaryNames();
            if (boundaryNames != null)
            {
                foreach (KeyValuePair<BaramShape, string> entry in boundaryNames)
                    names[entry.Key] = entry.Value;
            }

            double xMin = bounds.XMin;
            double xMax = bounds.XMax;
            double yMin = bounds.YMin;
            double yMax = bounds.YMax;
            double zMin = bounds.ZMin;
            double zMax = bounds.ZMax;

            if (configuration.BaseGrid.BoundingHex6.HasValue)
            {
                BaramGeometry boundingHex6;
                if (configuration.Geometry.TryGetValue(configuration.BaseGrid.BoundingHex6.Value, out boundingHex6) &&
                    boundingHex6.GeometryType == BaramGeometryType.Volume &&
                    boundingHex6.Shape == BaramShape.Hex6)
                {
                    xMin = Math.Min(boundingHex6.Point1.X, boundingHex6.Point2.X);
                    xMax = Math.Max(boundingHex6.Point1.X, boundingHex6.Point2.X);
                    yMin = Math.Min(boundingHex6.Point1.Y, boundingHex6.Point2.Y);
                    yMax = Math.Max(boundingHex6.Point1.Y, boundingHex6.Point2.Y);
                    zMin = Math.Min(boundingHex6.Point1.Z, boundingHex6.Point2.Z);
                    zMax = Math.Max(boundingHex6.Point1.Z, boundingHex6.Point2.Z);
                }
            }

            double padding = GetPadding(configuration.BaseGrid, bounds);
            if (!configuration.BaseGrid.BoundingHex6.HasValue)
            {
                xMin -= padding;
                xMax += padding;
                yMin -= padding;
                yMax += padding;
                zMin -= padding;
                zMax += padding;
            }

            OpenFoamDictionary data = new OpenFoamDictionary();
            data.AddEntry("scale", 1.0);
            data.AddEntry("vertices", new object[]
            {
                new object[] { xMin, yMin, zMin },
                new object[] { xMax, yMin, zMin },
                new object[] { xMax, yMax, zMin },
                new object[] { xMin, yMax, zMin },
                new object[] { xMin, yMin, zMax },
                new object[] { xMax, yMin, zMax },
                new object[] { xMax, yMax, zMax },
                new object[] { xMin, yMax, zMax }
            });
            data.AddEntry("blocks", new object[]
            {
                new OpenFoamRawValue(
                    "hex (0 1 2 3 4 5 6 7) (" +
                    configuration.BaseGrid.NumCellsX.ToString(CultureInfo.InvariantCulture) + " " +
                    configuration.BaseGrid.NumCellsY.ToString(CultureInfo.InvariantCulture) + " " +
                    configuration.BaseGrid.NumCellsZ.ToString(CultureInfo.InvariantCulture) + ") simpleGrading (1 1 1)")
            });
            data.AddEntry("edges", new object[0]);
            data.AddEntry("boundary", new OpenFoamRawValue(BuildBoundary(names)));
            data.AddEntry("mergePatchPairs", new object[0]);
            return data;
        }

        private static Dictionary<BaramShape, string> CreateDefaultBoundaryNames()
        {
            return new Dictionary<BaramShape, string>
            {
                { BaramShape.XMin, "xMin" },
                { BaramShape.XMax, "xMax" },
                { BaramShape.YMin, "yMin" },
                { BaramShape.YMax, "yMax" },
                { BaramShape.ZMin, "zMin" },
                { BaramShape.ZMax, "zMax" }
            };
        }

        private static double GetPadding(BaramBaseGrid baseGrid, BaramBounds bounds)
        {
            double dx = Math.Abs(bounds.XMax - bounds.XMin) / baseGrid.NumCellsX;
            double dy = Math.Abs(bounds.YMax - bounds.YMin) / baseGrid.NumCellsY;
            double dz = Math.Abs(bounds.ZMax - bounds.ZMin) / baseGrid.NumCellsZ;
            return Math.Min(dx, Math.Min(dy, dz)) / 100.0;
        }

        private static string BuildBoundary(Dictionary<BaramShape, string> names)
        {
            return "(" + Environment.NewLine +
                   BuildPatch(names[BaramShape.XMin], "0 3 7 4") +
                   BuildPatch(names[BaramShape.XMax], "1 2 6 5") +
                   BuildPatch(names[BaramShape.YMin], "0 1 5 4") +
                   BuildPatch(names[BaramShape.YMax], "3 7 6 2") +
                   BuildPatch(names[BaramShape.ZMin], "0 1 2 3") +
                   BuildPatch(names[BaramShape.ZMax], "4 5 6 7") +
                   ")";
        }

        private static string BuildPatch(string name, string face)
        {
            return "    " + name + Environment.NewLine +
                   "    {" + Environment.NewLine +
                   "        type patch;" + Environment.NewLine +
                   "        faces ((" + face + "));" + Environment.NewLine +
                   "    }" + Environment.NewLine;
        }
    }
}
