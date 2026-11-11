// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;

namespace CaeGlobals
{
    public static partial class ExtensionMethods
    {
        // vtkSelectBy
        public static bool IsGeometryBased(this vtkSelectBy selectBy)
        {
            return selectBy == vtkSelectBy.Geometry ||
                   selectBy == vtkSelectBy.GeometryVertex ||
                   selectBy == vtkSelectBy.GeometryEdge ||
                   selectBy == vtkSelectBy.GeometrySurface ||
                   selectBy == vtkSelectBy.GeometryEdgeAngle ||
                   selectBy == vtkSelectBy.GeometrySurfaceAngle ||
                   selectBy == vtkSelectBy.GeometryPart;
        }
    }
}
