// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;
using System.Collections.Generic;
using CaeGlobals;
using CaeMesh;

namespace CaeModel
{
    // Shared FeModel surface integration. Shell edge thickness is supplied by the model owner.
    public static class SurfaceTractionConverter
    {
        public static CLoad[] GetNodalCLoadsFromConstantSurfaceTraction(FeMesh mesh, STLoad load,
                                                                                Func<int, double> getShellThickness = null)
        {
            List<CLoad> loads = new List<CLoad>();
            //
            if (load.FMagnitude.Value != 0)
            {
                double area;
                Dictionary<int, double> nodalForces;
                GetDistributedNodalValuesFromSurface(mesh, load.SurfaceName, out nodalForces, out area, getShellThickness);
                if (area <= 0 || double.IsNaN(area) || double.IsInfinity(area))
                    throw new CaeException("Surface traction requires a finite, positive surface area.");
                //
                double f1ByArea = load.F1.Value / area;
                double f2ByArea = load.F2.Value / area;
                double f3ByArea = load.F3.Value / area;
                //
                CLoad cLoad;
                double phaseDeg = load.PhaseDeg.Value;
                foreach (var entry in nodalForces)
                {
                    if (entry.Value != 0)
                    {
                        cLoad = new CLoad("_CLoad_" + entry.Key.ToString(), entry.Key,
                                          f1ByArea * entry.Value,
                                          f2ByArea * entry.Value,
                                          f3ByArea * entry.Value, load.TwoD, load.Complex, phaseDeg, true);
                        cLoad.AmplitudeName = load.AmplitudeName;
                        loads.Add(cLoad);
                    }
                }
            }
            //
            return loads.ToArray();
        }
        public static void GetDistributedNodalValuesFromSurface(FeMesh mesh, string surfaceName,
                                                         out Dictionary<int, double> nodalValues,
                                                         out double area, Func<int, double> getShellThickness = null)
        {
            nodalValues = new Dictionary<int, double>();
            area = 0;
            //
            int nodeId;
            int[] nodeIds;
            double A;
            double thickness;
            double[] equValue;
            FeElement element;
            FeSurface surface = mesh.Surfaces[surfaceName];
            if (surface.ElementFaces == null) return;
            //
            foreach (var entry in surface.ElementFaces)
            {
                foreach (var elementId in mesh.ElementSets[entry.Value].Labels)
                {
                    element = mesh.Elements[elementId];
                    A = element.GetArea(entry.Key, mesh.Nodes);
                    // Is shell edge face
                    if (element is FeElement2D && entry.Key != FeFaceName.S1 && entry.Key != FeFaceName.S2)
                    {
                        if (getShellThickness == null)
                            throw new CaeException("Shell edge traction requires a section thickness at element " + elementId + ".");
                        thickness = getShellThickness(elementId);
                        if (thickness <= 0 || double.IsNaN(thickness) || double.IsInfinity(thickness))
                            throw new CaeException("Shell edge traction requires a finite, positive thickness.");
                        A *= thickness;
                    }
                    area += A;
                    nodeIds = element.GetNodeIdsFromFaceName(entry.Key);
                    equValue = element.GetEquivalentForcesFromFaceName(entry.Key);
                    //
                    for (int i = 0; i < nodeIds.Length; i++)
                    {
                        nodeId = nodeIds[i];
                        if (nodalValues.ContainsKey(nodeId)) nodalValues[nodeId] += A * equValue[i];
                        else nodalValues.Add(nodeId, A * equValue[i]);
                    }
                }
            }
        }
    }
}
