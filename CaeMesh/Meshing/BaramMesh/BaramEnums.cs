// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace CaeMesh.Meshing.BaramMesh
{
    [Serializable]
    public enum BaramStep
    {
        None = -1,
        Geometry = 0,
        Region = 1,
        BaseGrid = 2,
        Castellation = 3,
        Snap = 4,
        BoundaryLayer = 5,
        Export = 6
    }

    [Serializable]
    public enum BaramGeometryType
    {
        Surface,
        Volume
    }

    [Serializable]
    public enum BaramShape
    {
        TriSurfaceMesh,
        Hex,
        Cylinder,
        Sphere,
        Hex6,
        XMin,
        XMax,
        YMin,
        YMax,
        ZMin,
        ZMax,
        Plane
    }

    [Serializable]
    public enum BaramCfdType
    {
        None,
        CellZone,
        Boundary,
        Interface
    }

    [Serializable]
    public enum BaramBoundaryRole
    {
        Unassigned,
        Inlet,
        Outlet,
        Symmetry,
        Wall,
        FsiInterface
    }

    [Serializable]
    public enum BaramRegionType
    {
        Fluid,
        Solid
    }

    [Serializable]
    public enum BaramThicknessModel
    {
        FirstAndOverall,
        FirstAndExpansion,
        FinalAndOverall,
        FinalAndExpansion,
        OverallAndExpansion,
        FirstAndRelativeFinal
    }

    [Serializable]
    public enum BaramFeatureSnapType
    {
        Explicit,
        Implicit
    }

    [Serializable]
    public enum BaramGapRefinementMode
    {
        None,
        Inside,
        Outside,
        Mixed
    }

    [Serializable]
    public enum BaramBufferLayerPointSmoothingMethod
    {
        Laplacian,
        GeometricElementTransform
    }

    public static class BaramEnumExtensions
    {
        public static string ToBaramString(this BaramGeometryType value)
        {
            switch (value)
            {
                case BaramGeometryType.Surface: return "surface";
                case BaramGeometryType.Volume: return "volume";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }

        public static string ToBaramString(this BaramShape value)
        {
            switch (value)
            {
                case BaramShape.TriSurfaceMesh: return "triSurfaceMesh";
                case BaramShape.Hex: return "hex";
                case BaramShape.Cylinder: return "cylinder";
                case BaramShape.Sphere: return "sphere";
                case BaramShape.Hex6: return "hex6";
                case BaramShape.XMin: return "xMin";
                case BaramShape.XMax: return "xMax";
                case BaramShape.YMin: return "yMin";
                case BaramShape.YMax: return "yMax";
                case BaramShape.ZMin: return "zMin";
                case BaramShape.ZMax: return "zMax";
                case BaramShape.Plane: return "plane";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }

        public static string ToBaramString(this BaramCfdType value)
        {
            switch (value)
            {
                case BaramCfdType.None: return "none";
                case BaramCfdType.CellZone: return "cellZone";
                case BaramCfdType.Boundary: return "boundary";
                case BaramCfdType.Interface: return "interface";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }

        public static string ToBaramString(this BaramBoundaryRole value)
        {
            switch (value)
            {
                case BaramBoundaryRole.Unassigned: return "unassigned";
                case BaramBoundaryRole.Inlet: return "inlet";
                case BaramBoundaryRole.Outlet: return "outlet";
                case BaramBoundaryRole.Symmetry: return "symmetry";
                case BaramBoundaryRole.Wall: return "wall";
                case BaramBoundaryRole.FsiInterface: return "fsiInterface";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }

        public static string ToBaramString(this BaramRegionType value)
        {
            switch (value)
            {
                case BaramRegionType.Fluid: return "fluid";
                case BaramRegionType.Solid: return "solid";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }

        public static string ToBaramString(this BaramThicknessModel value)
        {
            switch (value)
            {
                case BaramThicknessModel.FirstAndOverall: return "firstAndOverall";
                case BaramThicknessModel.FirstAndExpansion: return "firstAndExpansion";
                case BaramThicknessModel.FinalAndOverall: return "finalAndOverall";
                case BaramThicknessModel.FinalAndExpansion: return "finalAndExpansion";
                case BaramThicknessModel.OverallAndExpansion: return "overallAndExpansion";
                case BaramThicknessModel.FirstAndRelativeFinal: return "firstAndRelativeFinal";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }

        public static string ToBaramString(this BaramFeatureSnapType value)
        {
            switch (value)
            {
                case BaramFeatureSnapType.Explicit: return "explicit";
                case BaramFeatureSnapType.Implicit: return "implicit";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }

        public static string ToBaramString(this BaramGapRefinementMode value)
        {
            switch (value)
            {
                case BaramGapRefinementMode.None: return "none";
                case BaramGapRefinementMode.Inside: return "inside";
                case BaramGapRefinementMode.Outside: return "outside";
                case BaramGapRefinementMode.Mixed: return "mixed";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }

        public static string ToBaramString(this BaramBufferLayerPointSmoothingMethod value)
        {
            switch (value)
            {
                case BaramBufferLayerPointSmoothingMethod.Laplacian: return "laplacian";
                case BaramBufferLayerPointSmoothingMethod.GeometricElementTransform: return "geometricElementTransform";
                default: throw new ArgumentOutOfRangeException("value");
            }
        }
    }
}
