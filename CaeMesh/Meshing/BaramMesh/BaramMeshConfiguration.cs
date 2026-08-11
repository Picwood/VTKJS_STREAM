// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CaeMesh.Meshing.BaramMesh
{
    [Serializable]
    public class BaramMeshConfiguration
    {
        public int Version { get; set; }
        public BaramStep Step { get; set; }
        public Dictionary<int, BaramGeometry> Geometry { get; private set; }
        public Dictionary<int, BaramRegion> Regions { get; private set; }
        public BaramBaseGrid BaseGrid { get; set; }
        public BaramCastellationSettings Castellation { get; set; }
        public BaramSnapSettings Snap { get; set; }
        public BaramBoundaryLayerSettings BoundaryLayer { get; set; }
        public BaramMeshQualitySettings MeshQuality { get; set; }

        public BaramMeshConfiguration()
        {
            Version = BaramMeshProject.CurrentConfigurationVersion;
            Step = BaramStep.Geometry;
            Geometry = new Dictionary<int, BaramGeometry>();
            Regions = new Dictionary<int, BaramRegion>();
            BaseGrid = new BaramBaseGrid();
            Castellation = new BaramCastellationSettings();
            Snap = new BaramSnapSettings();
            BoundaryLayer = new BaramBoundaryLayerSettings();
            MeshQuality = new BaramMeshQualitySettings();
        }

        public void Validate()
        {
            if (Version < 0 || Version > BaramMeshProject.CurrentConfigurationVersion)
                throw new InvalidOperationException("Unsupported BaramMesh configuration version: " + Version + ".");

            BaseGrid.Validate();
            Castellation.Validate();
            Snap.Validate();
            BoundaryLayer.Validate();
            MeshQuality.Validate();

            foreach (var entry in Geometry) entry.Value.Validate();
            foreach (var entry in Regions) entry.Value.Validate();
        }
    }

    [Serializable]
    public struct BaramVector
    {
        public double X;
        public double Y;
        public double Z;

        public BaramVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    [Serializable]
    public class BaramGeometry
    {
        [OptionalField]
        private BaramBoundaryRole _boundaryRole;

        public BaramGeometryType GeometryType { get; set; }
        public int? VolumeId { get; set; }
        public string Name { get; set; }
        public BaramShape Shape { get; set; }
        public BaramCfdType CfdType { get; set; }
        public BaramBoundaryRole BoundaryRole
        {
            get { return _boundaryRole; }
            set { _boundaryRole = value; }
        }
        public bool NonConformal { get; set; }
        public bool InterRegion { get; set; }
        public string Path { get; set; }
        public BaramVector Point1 { get; set; }
        public BaramVector Point2 { get; set; }
        public BaramVector PlaneNormal { get; set; }
        public double PlaneWidth { get; set; }
        public double PlaneHeight { get; set; }
        public double Radius { get; set; }
        public int? CastellationGroup { get; set; }
        public int? LayerGroup { get; set; }
        public int? SlaveLayerGroup { get; set; }
        public bool AddBufferLayers { get; set; }

        public BaramGeometry()
        {
            Name = "Geometry";
            Shape = BaramShape.TriSurfaceMesh;
            CfdType = BaramCfdType.None;
            Point2 = new BaramVector(1, 1, 1);
            PlaneNormal = new BaramVector(0, 0, 1);
            PlaneWidth = 1;
            PlaneHeight = 1;
            Radius = 1;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("A BaramMesh geometry name is required.");
            if (!Enum.IsDefined(typeof(BaramBoundaryRole), BoundaryRole))
                throw new InvalidOperationException("Unsupported BaramMesh boundary role: " + BoundaryRole + ".");
            if (BoundaryRole != BaramBoundaryRole.Unassigned &&
                (GeometryType != BaramGeometryType.Surface || CfdType != BaramCfdType.Boundary))
                throw new InvalidOperationException("A BaramMesh boundary role can only be assigned to a boundary surface.");
            if (PlaneWidth <= 0) throw new InvalidOperationException("Plane width must be greater than zero.");
            if (PlaneHeight <= 0) throw new InvalidOperationException("Plane height must be greater than zero.");
        }
    }

    [Serializable]
    public class BaramRegion
    {
        public string Name { get; set; }
        public BaramRegionType Type { get; set; }
        public BaramVector Point { get; set; }

        public BaramRegion()
        {
            Name = "Region";
            Type = BaramRegionType.Fluid;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("A BaramMesh region name is required.");
        }
    }

    [Serializable]
    public class BaramBaseGrid
    {
        public int NumCellsX { get; set; }
        public int NumCellsY { get; set; }
        public int NumCellsZ { get; set; }
        public int? BoundingHex6 { get; set; }

        public BaramBaseGrid()
        {
            NumCellsX = 10;
            NumCellsY = 10;
            NumCellsZ = 10;
        }

        public void Validate()
        {
            if (NumCellsX < 1 || NumCellsY < 1 || NumCellsZ < 1)
                throw new InvalidOperationException("Base grid cell counts must be greater than zero.");
        }
    }

    [Serializable]
    public class BaramCastellationSettings
    {
        public bool VtkNonManifoldEdges { get; set; }
        public bool VtkBoundaryEdges { get; set; }
        public int NCellsBetweenLevels { get; set; }
        public double ResolveFeatureAngle { get; set; }
        public int MaxGlobalCells { get; set; }
        public int MaxLocalCells { get; set; }
        public int MinRefinementCells { get; set; }
        public double MaxLoadUnbalance { get; set; }
        public bool AllowFreeStandingZoneFaces { get; set; }
        public Dictionary<int, BaramSurfaceRefinement> RefinementSurfaces { get; private set; }
        public Dictionary<int, BaramVolumeRefinement> RefinementVolumes { get; private set; }

        public BaramCastellationSettings()
        {
            VtkBoundaryEdges = true;
            NCellsBetweenLevels = 3;
            ResolveFeatureAngle = 30;
            MaxGlobalCells = 100000000;
            MaxLocalCells = 10000000;
            MaxLoadUnbalance = 0.5;
            AllowFreeStandingZoneFaces = true;
            RefinementSurfaces = new Dictionary<int, BaramSurfaceRefinement>();
            RefinementVolumes = new Dictionary<int, BaramVolumeRefinement>();
        }

        public void Validate()
        {
            if (NCellsBetweenLevels < 1) throw new InvalidOperationException("nCellsBetweenLevels must be greater than zero.");
            foreach (var entry in RefinementSurfaces) entry.Value.Validate();
            foreach (var entry in RefinementVolumes) entry.Value.Validate();
        }
    }

    [Serializable]
    public class BaramSurfaceRefinement
    {
        public string GroupName { get; set; }
        public int MinimumLevel { get; set; }
        public int MaximumLevel { get; set; }
        public int FeatureEdgeRefinementLevel { get; set; }
        public bool CurvatureRefinementDisabled { get; set; }
        public int CurvatureNumberOfCells { get; set; }
        public int CurvatureMaxLevel { get; set; }
        public bool CurvatureExcludeSharpSurface { get; set; }
        public double CurvatureMinRadius { get; set; }

        public BaramSurfaceRefinement()
        {
            GroupName = "Surface";
            MinimumLevel = 1;
            MaximumLevel = 1;
            FeatureEdgeRefinementLevel = 1;
            CurvatureRefinementDisabled = true;
            CurvatureNumberOfCells = 10;
            CurvatureMaxLevel = 7;
        }

        public void Validate()
        {
            ValidateLevel(MinimumLevel, 0, 10, "minimumLevel");
            ValidateLevel(MaximumLevel, 1, 10, "maximumLevel");
            ValidateLevel(FeatureEdgeRefinementLevel, 1, 10, "featureEdgeRefinementLevel");
            if (MinimumLevel > MaximumLevel)
                throw new InvalidOperationException("minimumLevel cannot exceed maximumLevel.");
            if (!CurvatureRefinementDisabled)
            {
                if (CurvatureNumberOfCells < 1)
                    throw new InvalidOperationException("curvature numberOfCells must be greater than zero.");
                ValidateLevel(CurvatureMaxLevel, 1, 10, "curvature maxLevel");
                if (CurvatureExcludeSharpSurface && CurvatureMinRadius <= 0)
                    throw new InvalidOperationException("curvature minRadius must be greater than zero when sharp surfaces are excluded.");
            }
        }

        internal static void ValidateLevel(int value, int min, int max, string name)
        {
            if (value < min || value > max) throw new InvalidOperationException(name + " must be between " + min + " and " + max + ".");
        }
    }

    [Serializable]
    public class BaramVolumeRefinement
    {
        public string GroupName { get; set; }
        public int VolumeRefinementLevel { get; set; }
        public int GapMinCellLayers { get; set; }
        public int GapDetectionStartLevel { get; set; }
        public int GapMaxRefinementLevel { get; set; }
        public BaramGapRefinementMode GapDirection { get; set; }
        public bool GapSelf { get; set; }
        public bool LevelIncrementDisabled { get; set; }
        public int SplitCountX { get; set; }
        public int SplitCountY { get; set; }
        public int SplitCountZ { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }

        public BaramVolumeRefinement()
        {
            GroupName = "Volume";
            VolumeRefinementLevel = 1;
            GapMinCellLayers = 4;
            GapDetectionStartLevel = 1;
            GapMaxRefinementLevel = 1;
            GapSelf = true;
            LevelIncrementDisabled = true;
            SplitCountX = 1;
            MaxLevel = 10;
        }

        public void Validate()
        {
            BaramSurfaceRefinement.ValidateLevel(VolumeRefinementLevel, 0, 10, "volumeRefinementLevel");
            BaramSurfaceRefinement.ValidateLevel(GapDetectionStartLevel, 0, 10, "gapDetectionStartLevel");
            BaramSurfaceRefinement.ValidateLevel(GapMaxRefinementLevel, 1, 10, "gapMaxRefinementLevel");
            if (GapMinCellLayers < 4) throw new InvalidOperationException("gap minCellLayers must be 4 or greater.");
            if (GapDirection != BaramGapRefinementMode.None && GapDetectionStartLevel >= GapMaxRefinementLevel)
                throw new InvalidOperationException("gap maxRefinementLevel must exceed detectionStartLevel.");
            BaramSurfaceRefinement.ValidateLevel(MinLevel, 0, 10, "levelIncrement minLevel");
            BaramSurfaceRefinement.ValidateLevel(MaxLevel, 0, 10, "levelIncrement maxLevel");
            if (!LevelIncrementDisabled && MinLevel > MaxLevel)
                throw new InvalidOperationException("levelIncrement minLevel cannot exceed maxLevel.");
            ValidateSplitCount(SplitCountX, "splitCountX");
            ValidateSplitCount(SplitCountY, "splitCountY");
            ValidateSplitCount(SplitCountZ, "splitCountZ");
        }

        private static void ValidateSplitCount(int value, string name)
        {
            if (value < 0 || value > 10)
                throw new InvalidOperationException(name + " must be between 0 and 10.");
        }
    }

    [Serializable]
    public class BaramSnapSettings
    {
        public int NSmoothPatch { get; set; }
        public int NSmoothInternal { get; set; }
        public int NSolveIter { get; set; }
        public int NRelaxIter { get; set; }
        public int NFeatureSnapIter { get; set; }
        public BaramFeatureSnapType FeatureSnapType { get; set; }
        public bool MultiRegionFeatureSnap { get; set; }
        public double Tolerance { get; set; }
        public double ConcaveAngle { get; set; }
        public double MinAreaRatio { get; set; }
        public BaramBufferLayerSettings BufferLayer { get; set; }

        public BaramSnapSettings()
        {
            NSolveIter = 30;
            NRelaxIter = 5;
            NFeatureSnapIter = 15;
            FeatureSnapType = BaramFeatureSnapType.Explicit;
            Tolerance = 3;
            ConcaveAngle = 45;
            MinAreaRatio = 0.3;
            BufferLayer = new BaramBufferLayerSettings();
        }

        public void Validate()
        {
            ValidateNonNegative(NSmoothPatch, "nSmoothPatch");
            ValidateNonNegative(NSmoothInternal, "nSmoothInternal");
            ValidateNonNegative(NSolveIter, "nSolveIter");
            ValidateNonNegative(NRelaxIter, "nRelaxIter");
            ValidateNonNegative(NFeatureSnapIter, "nFeatureSnapIter");
            if (Tolerance <= 0) throw new InvalidOperationException("Snap tolerance must be greater than zero.");
            if (ConcaveAngle < 0 || ConcaveAngle > 180)
                throw new InvalidOperationException("Snap concave angle must be between 0 and 180 degrees.");
            if (MinAreaRatio < 0 || MinAreaRatio > 1)
                throw new InvalidOperationException("Snap minimum area ratio must be between 0 and 1.");
            BufferLayer.Validate();
        }

        private static void ValidateNonNegative(int value, string name)
        {
            if (value < 0) throw new InvalidOperationException(name + " cannot be negative.");
        }
    }

    [Serializable]
    public class BaramBufferLayerSettings
    {
        public bool Disabled { get; set; }
        public BaramBufferLayerPointSmoothingMethod PointSmoothingMethod { get; set; }
        public int NumberOfPointSmoothingIteration { get; set; }
        public double GetMeTransformationParameter { get; set; }

        public BaramBufferLayerSettings()
        {
            Disabled = true;
            PointSmoothingMethod = BaramBufferLayerPointSmoothingMethod.Laplacian;
            NumberOfPointSmoothingIteration = 10;
            GetMeTransformationParameter = 0.667;
        }

        public void Validate()
        {
            if (NumberOfPointSmoothingIteration < 10 || NumberOfPointSmoothingIteration > 100)
                throw new InvalidOperationException("Buffer layer point smoothing iterations must be between 10 and 100.");
            if (GetMeTransformationParameter <= 0 || GetMeTransformationParameter > 1)
                throw new InvalidOperationException("GETMe transformation parameter must be greater than 0 and less than or equal to 1.");
        }
    }

    [Serializable]
    public class BaramBoundaryLayerSettings
    {
        public Dictionary<int, BaramLayer> Layers { get; private set; }
        public int NGrow { get; set; }
        public double FeatureAngle { get; set; }
        public double MaxFaceThicknessRatio { get; set; }
        public int NSmoothSurfaceNormals { get; set; }
        public double NSmoothThickness { get; set; }
        public double MinMedialAxisAngle { get; set; }
        public double MaxThicknessToMedialRatio { get; set; }
        public int NSmoothNormals { get; set; }
        public int NRelaxIter { get; set; }
        public int NBufferCellsNoExtrude { get; set; }
        public int NLayerIter { get; set; }
        public int NRelaxedIter { get; set; }

        public BaramBoundaryLayerSettings()
        {
            Layers = new Dictionary<int, BaramLayer>();
            FeatureAngle = 60;
            MaxFaceThicknessRatio = 0.5;
            NSmoothSurfaceNormals = 1;
            NSmoothThickness = 10;
            MinMedialAxisAngle = 90;
            MaxThicknessToMedialRatio = 0.3;
            NSmoothNormals = 3;
            NRelaxIter = 10;
            NLayerIter = 50;
            NRelaxedIter = 20;
        }

        public void Validate()
        {
            foreach (var entry in Layers) entry.Value.Validate();
        }
    }

    [Serializable]
    public class BaramLayer
    {
        public string GroupName { get; set; }
        public int NSurfaceLayers { get; set; }
        public BaramThicknessModel ThicknessModel { get; set; }
        public bool RelativeSizes { get; set; }
        public double FirstLayerThickness { get; set; }
        public double FinalLayerThickness { get; set; }
        public double Thickness { get; set; }
        public double ExpansionRatio { get; set; }
        public double MinThickness { get; set; }

        public BaramLayer()
        {
            GroupName = "Layer";
            NSurfaceLayers = 1;
            ThicknessModel = BaramThicknessModel.FinalAndExpansion;
            RelativeSizes = true;
            FirstLayerThickness = 0.3;
            FinalLayerThickness = 0.5;
            Thickness = 0.5;
            ExpansionRatio = 1.2;
            MinThickness = 0.3;
        }

        public void Validate()
        {
            if (NSurfaceLayers < 1) throw new InvalidOperationException("nSurfaceLayers must be greater than zero.");
        }
    }

    [Serializable]
    public class BaramMeshQualitySettings
    {
        public double MaxNonOrtho { get; set; }
        public double MaxBoundarySkewness { get; set; }
        public double MaxInternalSkewness { get; set; }
        public double MaxConcave { get; set; }
        public double MinVol { get; set; }
        public double MinTetQuality { get; set; }
        public double MinVolCollapseRatio { get; set; }
        public double MinArea { get; set; }
        public double MinTwist { get; set; }
        public double MinDeterminant { get; set; }
        public double MinFaceWeight { get; set; }
        public double MinFaceFlatness { get; set; }
        public double MinVolRatio { get; set; }
        public double MinTriangleTwist { get; set; }
        public int NSmoothScale { get; set; }
        public double ErrorReduction { get; set; }
        public double MergeTolerance { get; set; }
        public double RelaxedMaxNonOrtho { get; set; }

        public BaramMeshQualitySettings()
        {
            MaxNonOrtho = 65;
            MaxBoundarySkewness = 20;
            MaxInternalSkewness = 4;
            MaxConcave = 80;
            MinVol = 1e-13;
            MinTetQuality = 1e-9;
            MinVolCollapseRatio = -1;
            MinArea = -1;
            MinTwist = 0.02;
            MinDeterminant = 0.001;
            MinFaceWeight = 0.05;
            MinFaceFlatness = -1;
            MinVolRatio = 0.01;
            MinTriangleTwist = -1;
            NSmoothScale = 4;
            ErrorReduction = 0.75;
            MergeTolerance = 1e-6;
            RelaxedMaxNonOrtho = 75;
        }

        public void Validate()
        {
            if (MergeTolerance <= 0) throw new InvalidOperationException("mergeTolerance must be greater than zero.");
        }
    }
}
