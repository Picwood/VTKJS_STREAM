// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using CaeGlobals;
using CaeMesh;

namespace CaeResults
{
    public partial class FeResults
    {
        public void CopyMeshItemsFromMesh(FeMesh mesh)
        {
            foreach (var entry in mesh.NodeSets)
            {
                _mesh.NodeSets.Add(entry.Key, entry.Value.DeepClone());
            }
            //
            string newName;
            FeElementSet newElementSet;
            foreach (var entry in mesh.ElementSets)
            {
                if (_mesh.ElementSets.ContainsKey(entry.Key))
                    newName = _mesh.ElementSets.GetNextNumberedKey(entry.Key);
                else newName = entry.Key;
                //
                newElementSet = entry.Value.DeepClone();
                newElementSet.Name = newName;
                //
                _mesh.ElementSets.Add(newElementSet.Name, newElementSet);
            }
            //
            foreach (var entry in mesh.Surfaces)
            {
                _mesh.Surfaces.Add(entry.Key, entry.Value.DeepClone());
            }
        }
        public void CopyFeatureItemsFromMesh(FeMesh mesh, int resultViewId)
        {
            FeReferencePoint referencePoint;
            foreach (var entry in mesh.ReferencePoints)
            {
                referencePoint = entry.Value.DeepClone();
                referencePoint.CreationData = null;
                referencePoint.CreationIds = null;
                referencePoint.CreatedFrom = FeReferencePointCreatedFrom.Coordinates;
                //
                _mesh.ReferencePoints.Add(entry.Key, referencePoint);
            }
            CoordinateSystem coordinateSystem;
            foreach (var entry in mesh.CoordinateSystems)
            {
                coordinateSystem = entry.Value.DeepClone();
                //
                coordinateSystem.CenterCreationData = null;
                coordinateSystem.CenterCreationIds = null;
                coordinateSystem.CenterCreatedFrom = CsPointCreatedFromEnum.Coordinates;
                coordinateSystem.PointXCreationData = null;
                coordinateSystem.PointXCreationIds = null;
                coordinateSystem.PointXCreatedFrom = CsPointCreatedFromEnum.Coordinates;
                coordinateSystem.PointXYCreationData = null;
                coordinateSystem.PointXYCreationIds = null;
                coordinateSystem.PointXYCreatedFrom = CsPointCreatedFromEnum.Coordinates;
                //
                _mesh.CoordinateSystems.Add(entry.Key, coordinateSystem);
            }
        }
    }
}
