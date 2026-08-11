// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CaeMesh.Meshing.BaramMesh
{
    public class BaramSnappyHexMeshDictBuilder
    {
        public OpenFoamDictionary Build(BaramMeshConfiguration configuration, BaramBounds bounds,
                                        bool castellatedMesh, bool snap, bool addLayers)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            configuration.Validate();

            OpenFoamDictionary data = new OpenFoamDictionary();
            if (snap && !configuration.Snap.BufferLayer.Disabled)
                data.AddEntry("type", "castellatedBufferLayer");
            data.AddEntry("castellatedMesh", castellatedMesh);
            data.AddEntry("snap", snap);
            data.AddEntry("addLayers", addLayers);
            data.AddEntry("geometry", BuildGeometry(configuration));
            data.AddEntry("castellatedMeshControls", BuildCastellatedMeshControls(configuration, bounds, snap));
            data.AddEntry("snapControls", BuildSnapControls(configuration, snap));
            data.AddEntry("addLayersControls", BuildAddLayersControls(configuration));
            data.AddEntry("meshQualityControls", BuildMeshQualityControls(configuration));
            data.AddEntry("mergeTolerance", configuration.MeshQuality.MergeTolerance);
            return data;
        }

        private static OpenFoamDictionary BuildGeometry(BaramMeshConfiguration configuration)
        {
            OpenFoamDictionary data = new OpenFoamDictionary();
            foreach (KeyValuePair<int, BaramGeometry> entry in configuration.Geometry)
            {
                BaramGeometry geometry = entry.Value;
                if (!ShouldIncludeGeometry(entry.Key, geometry, configuration)) continue;
                if (geometry.Shape == BaramShape.TriSurfaceMesh || geometry.Shape == BaramShape.Plane)
                {
                    OpenFoamDictionary surface = new OpenFoamDictionary();
                    surface.AddEntry("type", "triSurfaceMesh");
                    surface.AddEntry("name", BaramGeometryFactory.SanitizeGeometryName(geometry.Name));
                    data.AddEntry(BaramGeometryFactory.SanitizeGeometryName(geometry.Name) + ".stl", surface);
                }
                else if (geometry.Shape == BaramShape.Hex || geometry.Shape == BaramShape.Hex6)
                {
                    OpenFoamDictionary box = new OpenFoamDictionary();
                    box.AddEntry("type", "searchableBox");
                    box.AddEntry("min", Vector(Math.Min(geometry.Point1.X, geometry.Point2.X),
                                               Math.Min(geometry.Point1.Y, geometry.Point2.Y),
                                               Math.Min(geometry.Point1.Z, geometry.Point2.Z)));
                    box.AddEntry("max", Vector(Math.Max(geometry.Point1.X, geometry.Point2.X),
                                               Math.Max(geometry.Point1.Y, geometry.Point2.Y),
                                               Math.Max(geometry.Point1.Z, geometry.Point2.Z)));
                    data.AddEntry(BaramGeometryFactory.SanitizeGeometryName(geometry.Name), box);
                }
                else if (geometry.Shape == BaramShape.Sphere)
                {
                    OpenFoamDictionary sphere = new OpenFoamDictionary();
                    sphere.AddEntry("type", "searchableSphere");
                    sphere.AddEntry("centre", Vector(geometry.Point1.X, geometry.Point1.Y, geometry.Point1.Z));
                    sphere.AddEntry("radius", geometry.Radius);
                    data.AddEntry(BaramGeometryFactory.SanitizeGeometryName(geometry.Name), sphere);
                }
                else if (geometry.Shape == BaramShape.Cylinder)
                {
                    OpenFoamDictionary cylinder = new OpenFoamDictionary();
                    cylinder.AddEntry("type", "searchableCylinder");
                    cylinder.AddEntry("point1", Vector(geometry.Point1.X, geometry.Point1.Y, geometry.Point1.Z));
                    cylinder.AddEntry("point2", Vector(geometry.Point2.X, geometry.Point2.Y, geometry.Point2.Z));
                    cylinder.AddEntry("radius", geometry.Radius);
                    data.AddEntry(BaramGeometryFactory.SanitizeGeometryName(geometry.Name), cylinder);
                }
            }
            return data;
        }

        private static OpenFoamDictionary BuildCastellatedMeshControls(BaramMeshConfiguration configuration,
                                                                       BaramBounds bounds, bool snap)
        {
            BaramCastellationSettings settings = configuration.Castellation;
            OpenFoamDictionary data = new OpenFoamDictionary();
            data.AddEntry("maxLocalCells", settings.MaxLocalCells);
            data.AddEntry("maxGlobalCells", settings.MaxGlobalCells);
            data.AddEntry("minRefinementCells", settings.MinRefinementCells);
            data.AddEntry("maxLoadUnbalance", settings.MaxLoadUnbalance);
            data.AddEntry("nCellsBetweenLevels", settings.NCellsBetweenLevels);
            data.AddEntry("features", new OpenFoamRawValue(BuildFeatures(configuration, snap)));
            data.AddEntry("refinementSurfaces", BuildRefinementSurfaces(configuration, snap));
            data.AddEntry("resolveFeatureAngle", settings.ResolveFeatureAngle);
            data.AddEntry("refinementRegions", BuildRefinementRegions(configuration));
            data.AddEntry("allowFreeStandingZoneFaces", settings.AllowFreeStandingZoneFaces);
            data.AddEntry("locationInMesh", Vector((bounds.XMin + bounds.XMax) / 2,
                                                   (bounds.YMin + bounds.YMax) / 2,
                                                   (bounds.ZMin + bounds.ZMax) / 2));
            return data;
        }

        private static string BuildFeatures(BaramMeshConfiguration configuration, bool snap)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(").AppendLine();
            foreach (KeyValuePair<int, BaramGeometry> entry in configuration.Geometry)
            {
                BaramGeometry geometry = entry.Value;
                if (geometry == null || geometry.GeometryType != BaramGeometryType.Surface) continue;
                if (ShouldSkipBaseGridGeometry(entry.Key, geometry, configuration)) continue;
                int level = 0;
                BaramSurfaceRefinement refinement;
                if (geometry.CastellationGroup.HasValue &&
                    configuration.Castellation.RefinementSurfaces.TryGetValue(geometry.CastellationGroup.Value, out refinement))
                    level = refinement.FeatureEdgeRefinementLevel;
                if (!snap && level <= 0) continue;
                //
                string name = BaramGeometryFactory.SanitizeGeometryName(geometry.Name);
                sb.AppendLine("    {");
                sb.Append("        file \"").Append(name).AppendLine(".obj\";");
                sb.Append("        level ").Append(level.ToString(CultureInfo.InvariantCulture)).AppendLine(";");
                sb.AppendLine("    }");
            }
            sb.Append(")");
            return sb.ToString();
        }

        private static OpenFoamDictionary BuildRefinementSurfaces(BaramMeshConfiguration configuration, bool snap)
        {
            OpenFoamDictionary data = new OpenFoamDictionary();
            foreach (KeyValuePair<int, BaramGeometry> entry in configuration.Geometry)
            {
                BaramGeometry geometry = entry.Value;
                if (geometry == null || geometry.GeometryType != BaramGeometryType.Surface) continue;
                if (ShouldSkipBaseGridGeometry(entry.Key, geometry, configuration)) continue;
                if (geometry.CfdType == BaramCfdType.None && !geometry.CastellationGroup.HasValue) continue;
                //
                string name = BaramGeometryFactory.SanitizeGeometryName(geometry.Name);
                OpenFoamDictionary surface = new OpenFoamDictionary();
                if (geometry.CfdType == BaramCfdType.None)
                {
                    surface.AddEntry("faceZone", name);
                    surface.AddEntry("faceType", "internal");
                }
                else if (geometry.CfdType == BaramCfdType.Boundary)
                {
                    OpenFoamDictionary patchInfo = new OpenFoamDictionary();
                    string patchType = geometry.BoundaryRole == BaramBoundaryRole.Wall ||
                                       geometry.BoundaryRole == BaramBoundaryRole.FsiInterface
                                           ? "wall"
                                           : "patch";
                    patchInfo.AddEntry("type", patchType);
                    surface.AddEntry("patchInfo", patchInfo);
                }
                else
                {
                    surface.AddEntry("faceZone", name);
                    surface.AddEntry("faceType", geometry.NonConformal ? "boundary" : "baffle");
                    OpenFoamDictionary patchInfo = new OpenFoamDictionary();
                    patchInfo.AddEntry("type", "patch");
                    surface.AddEntry("patchInfo", patchInfo);
                }
                //
                BaramSurfaceRefinement refinement;
                if (geometry.CastellationGroup.HasValue &&
                    configuration.Castellation.RefinementSurfaces.TryGetValue(geometry.CastellationGroup.Value, out refinement))
                {
                    surface.AddEntry("level", new object[] { refinement.MinimumLevel, refinement.MaximumLevel });
                    if (!refinement.CurvatureRefinementDisabled)
                    {
                        int minimumCurvatureLevel = GetMinimumCurvatureLevel(configuration, refinement);
                        surface.AddEntry("curvatureLevel", new object[]
                        {
                            refinement.CurvatureNumberOfCells,
                            0,
                            refinement.CurvatureMaxLevel,
                            minimumCurvatureLevel
                        });
                    }
                }
                else
                {
                    surface.AddEntry("level", new object[] { 0, 0 });
                }
                if (snap) surface.AddEntry("addBufferLayers", geometry.AddBufferLayers);
                data.AddEntry(name, surface);
            }
            return data;
        }

        private static int GetMinimumCurvatureLevel(BaramMeshConfiguration configuration,
                                                    BaramSurfaceRefinement refinement)
        {
            if (!refinement.CurvatureExcludeSharpSurface) return -1;
            if (configuration.BaseGrid == null || !configuration.BaseGrid.BoundingHex6.HasValue) return -1;
            BaramGeometry bounds;
            if (!configuration.Geometry.TryGetValue(configuration.BaseGrid.BoundingHex6.Value, out bounds)) return -1;
            if (configuration.BaseGrid.NumCellsX <= 0 || configuration.BaseGrid.NumCellsY <= 0 ||
                configuration.BaseGrid.NumCellsZ <= 0) return -1;
            //
            double x = Math.Abs(bounds.Point2.X - bounds.Point1.X) / configuration.BaseGrid.NumCellsX;
            double y = Math.Abs(bounds.Point2.Y - bounds.Point1.Y) / configuration.BaseGrid.NumCellsY;
            double z = Math.Abs(bounds.Point2.Z - bounds.Point1.Z) / configuration.BaseGrid.NumCellsZ;
            double minimumBaseCellSize = Math.Min(x, Math.Min(y, z));
            double radius = Math.Min(refinement.CurvatureMinRadius, minimumBaseCellSize);
            if (minimumBaseCellSize <= 0 || radius <= 0) return -1;
            return Math.Min((int)Math.Round(Math.Log(minimumBaseCellSize / radius, 2)), 100);
        }

        private static OpenFoamDictionary BuildRefinementRegions(BaramMeshConfiguration configuration)
        {
            OpenFoamDictionary data = new OpenFoamDictionary();
            foreach (KeyValuePair<int, BaramGeometry> entry in configuration.Geometry)
            {
                BaramGeometry geometry = entry.Value;
                if (geometry == null || geometry.GeometryType != BaramGeometryType.Volume) continue;
                if (ShouldSkipBaseGridGeometry(entry.Key, geometry, configuration)) continue;
                if (!geometry.CastellationGroup.HasValue) continue;
                //
                BaramVolumeRefinement refinement;
                if (!configuration.Castellation.RefinementVolumes.TryGetValue(geometry.CastellationGroup.Value, out refinement))
                    continue;
                //
                OpenFoamDictionary region = new OpenFoamDictionary();
                region.AddEntry("mode", "inside");
                region.AddEntry("levels", new object[]
                {
                    new object[] { 1E15, refinement.VolumeRefinementLevel }
                });
                if (refinement.GapDirection != BaramGapRefinementMode.None)
                {
                    region.AddEntry("gapLevel", new object[]
                    {
                        refinement.GapMinCellLayers,
                        refinement.GapDetectionStartLevel,
                        refinement.GapMaxRefinementLevel
                    });
                    region.AddEntry("gapMode", refinement.GapDirection.ToBaramString());
                    region.AddEntry("gapSelf", refinement.GapSelf);
                }
                if (!refinement.LevelIncrementDisabled)
                {
                    region.AddEntry("levelIncrement", new object[]
                    {
                        refinement.MinLevel,
                        refinement.MaxLevel,
                        new object[] { refinement.SplitCountX, refinement.SplitCountY, refinement.SplitCountZ }
                    });
                }
                data.AddEntry(BaramGeometryFactory.SanitizeGeometryName(geometry.Name), region);
            }
            return data;
        }

        private static OpenFoamDictionary BuildSnapControls(BaramMeshConfiguration configuration, bool snap)
        {
            BaramSnapSettings settings = configuration.Snap;
            OpenFoamDictionary data = new OpenFoamDictionary();
            data.AddEntry("nSmoothPatch", settings.NSmoothPatch);
            data.AddEntry("nSmoothInternal", settings.NSmoothInternal);
            data.AddEntry("tolerance", settings.Tolerance);
            data.AddEntry("nSolveIter", settings.NSolveIter);
            data.AddEntry("nRelaxIter", settings.NRelaxIter);
            data.AddEntry("implicitFeatureSnap", settings.FeatureSnapType == BaramFeatureSnapType.Implicit);
            data.AddEntry("explicitFeatureSnap", settings.FeatureSnapType == BaramFeatureSnapType.Explicit);
            data.AddEntry("nFeatureSnapIter", settings.NFeatureSnapIter);
            data.AddEntry("multiRegionFeatureSnap", settings.MultiRegionFeatureSnap);
            data.AddEntry("concaveAngle", settings.ConcaveAngle);
            data.AddEntry("minAreaRatio", settings.MinAreaRatio);
            if (snap && !settings.BufferLayer.Disabled)
            {
                data.AddEntry("solver", "displacementPointSmoothing");
                OpenFoamDictionary coefficients = new OpenFoamDictionary();
                coefficients.AddEntry("pointSmoother", settings.BufferLayer.PointSmoothingMethod.ToBaramString());
                coefficients.AddEntry("nPointSmootherIter",
                                      settings.BufferLayer.NumberOfPointSmoothingIteration);
                if (settings.BufferLayer.PointSmoothingMethod ==
                    BaramBufferLayerPointSmoothingMethod.GeometricElementTransform)
                    coefficients.AddEntry("transformationParameter",
                                          settings.BufferLayer.GetMeTransformationParameter);
                data.AddEntry("displacementPointSmoothingCoeffs", coefficients);
            }
            return data;
        }

        private static OpenFoamDictionary BuildAddLayersControls(BaramMeshConfiguration configuration)
        {
            BaramBoundaryLayerSettings settings = configuration.BoundaryLayer;
            OpenFoamDictionary data = new OpenFoamDictionary();
            data.AddEntry("layers", new OpenFoamDictionary());
            data.AddEntry("nGrow", settings.NGrow);
            data.AddEntry("featureAngle", settings.FeatureAngle);
            data.AddEntry("maxFaceThicknessRatio", settings.MaxFaceThicknessRatio);
            data.AddEntry("nSmoothSurfaceNormals", settings.NSmoothSurfaceNormals);
            data.AddEntry("nSmoothThickness", settings.NSmoothThickness);
            data.AddEntry("minMedialAxisAngle", settings.MinMedialAxisAngle);
            data.AddEntry("maxThicknessToMedialRatio", settings.MaxThicknessToMedialRatio);
            data.AddEntry("nSmoothNormals", settings.NSmoothNormals);
            data.AddEntry("nRelaxIter", settings.NRelaxIter);
            data.AddEntry("nBufferCellsNoExtrude", settings.NBufferCellsNoExtrude);
            data.AddEntry("nLayerIter", settings.NLayerIter);
            data.AddEntry("nRelaxedIter", settings.NRelaxedIter);
            data.AddEntry("thicknessModel", "finalAndExpansion");
            data.AddEntry("relativeSizes", "on");
            data.AddEntry("finalLayerThickness", 0.5);
            data.AddEntry("expansionRatio", 1.2);
            data.AddEntry("minThickness", 0.3);
            return data;
        }

        private static OpenFoamDictionary BuildMeshQualityControls(BaramMeshConfiguration configuration)
        {
            BaramMeshQualitySettings settings = configuration.MeshQuality;
            OpenFoamDictionary relaxed = new OpenFoamDictionary();
            relaxed.AddEntry("maxNonOrtho", settings.RelaxedMaxNonOrtho);
            OpenFoamDictionary data = new OpenFoamDictionary();
            data.AddEntry("maxNonOrtho", settings.MaxNonOrtho);
            data.AddEntry("maxBoundarySkewness", settings.MaxBoundarySkewness);
            data.AddEntry("maxInternalSkewness", settings.MaxInternalSkewness);
            data.AddEntry("maxConcave", settings.MaxConcave);
            data.AddEntry("minVol", settings.MinVol);
            data.AddEntry("minTetQuality", settings.MinTetQuality);
            data.AddEntry("minVolCollapseRatio", settings.MinVolCollapseRatio);
            data.AddEntry("minArea", settings.MinArea);
            data.AddEntry("minTwist", settings.MinTwist);
            data.AddEntry("minDeterminant", settings.MinDeterminant);
            data.AddEntry("minFaceWeight", settings.MinFaceWeight);
            data.AddEntry("minFaceFlatness", settings.MinFaceFlatness);
            data.AddEntry("minVolRatio", settings.MinVolRatio);
            data.AddEntry("minTriangleTwist", settings.MinTriangleTwist);
            data.AddEntry("relaxed", relaxed);
            data.AddEntry("nSmoothScale", settings.NSmoothScale);
            data.AddEntry("errorReduction", settings.ErrorReduction);
            return data;
        }

        private static bool ShouldIncludeGeometry(int id, BaramGeometry geometry, BaramMeshConfiguration configuration)
        {
            if (geometry == null) return false;
            if (ShouldSkipBaseGridGeometry(id, geometry, configuration)) return false;
            if (geometry.CfdType != BaramCfdType.None || geometry.CastellationGroup.HasValue) return true;
            //
            BaramGeometry volume;
            if (geometry.GeometryType == BaramGeometryType.Surface &&
                geometry.VolumeId.HasValue &&
                configuration.Geometry.TryGetValue(geometry.VolumeId.Value, out volume))
                return volume.CfdType != BaramCfdType.None || volume.CastellationGroup.HasValue;
            //
            return false;
        }

        private static bool ShouldSkipBaseGridGeometry(int id, BaramGeometry geometry, BaramMeshConfiguration configuration)
        {
            if (configuration.BaseGrid.BoundingHex6.HasValue)
            {
                int boundingHex6 = configuration.BaseGrid.BoundingHex6.Value;
                if (id == boundingHex6) return true;
                if (geometry.VolumeId.HasValue && geometry.VolumeId.Value == boundingHex6) return true;
            }
            return false;
        }

        private static object[] Vector(double x, double y, double z)
        {
            return new object[] { x, y, z };
        }
    }
}
