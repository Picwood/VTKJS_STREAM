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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CaeMesh;
using CaeGlobals;
using CaeResults;
using System.IO;
using System.Runtime.Serialization;

namespace CaeModel
{
    public partial class ImportedSTLoad : IPreviewable
    {
        public FeResults GetPreview(FeModel model, string resultName, UnitSystem unitSystem)
        {
            ImportLoad();
            //
            FeMesh targetMesh = model.Mesh;
            //
            PartExchangeData allData = new PartExchangeData();
            targetMesh.GetAllNodesAndCells(out allData.Nodes.Ids, out allData.Nodes.Coor, out allData.Cells.Ids,
                                           out allData.Cells.CellNodeIds, out allData.Cells.Types);
            //
            FeSurface surface = targetMesh.Surfaces[SurfaceName];
            FeNodeSet nodeSet = targetMesh.NodeSets[surface.NodeSetName];
            HashSet<int> nodeIds = new HashSet<int>(nodeSet.Labels);
            //
            float[] distancesAll = new float[allData.Nodes.Coor.Length];
            float[] distances1 = new float[allData.Nodes.Coor.Length];
            float[] distances2 = new float[allData.Nodes.Coor.Length];
            float[] distances3 = new float[allData.Nodes.Coor.Length];
            float[] forcesAll = new float[allData.Nodes.Coor.Length];
            float[] forces1 = new float[allData.Nodes.Coor.Length];
            float[] forces2 = new float[allData.Nodes.Coor.Length];
            float[] forces3 = new float[allData.Nodes.Coor.Length];
            //
            Parallel.For(0, forcesAll.Length, i =>
            //for (int i = 0; i < forcesAll.Length; i++)
            {
                double[] distance;
                double[] force;
                //
                if (nodeIds.Contains(allData.Nodes.Ids[i]))
                {
                    GetForcePerAreaAndDistanceForPoint(allData.Nodes.Coor[i], out distance, out force);
                    //
                    distances1[i] = (float)distance[0];
                    distances2[i] = (float)distance[1];
                    distances3[i] = (float)distance[2];
                    distancesAll[i] = (float)Math.Sqrt(distance[0] * distance[0] +
                                                       distance[1] * distance[1] +
                                                       distance[2] * distance[2]);
                    forces1[i] = (float)force[0];
                    forces2[i] = (float)force[1];
                    forces3[i] = (float)force[2];
                    forcesAll[i] = (float)Math.Sqrt(force[0] * force[0] +
                                                    force[1] * force[1] +
                                                    force[2] * force[2]);
                }
                else
                {
                    distances1[i] = float.NaN;
                    distances2[i] = float.NaN;
                    distances3[i] = float.NaN;
                    distancesAll[i] = float.NaN;
                    forces1[i] = float.NaN;
                    forces2[i] = float.NaN;
                    forces3[i] = float.NaN;
                    forcesAll[i] = float.NaN;
                }
            }
            );
            //
            Dictionary<int, int> nodeIdsLookUp = new Dictionary<int, int>();
            for (int i = 0; i < allData.Nodes.Coor.Length; i++) nodeIdsLookUp.Add(allData.Nodes.Ids[i], i);
            FeResults results = new FeResults(resultName, unitSystem);
            results.SetMesh(targetMesh, nodeIdsLookUp);
            // Add distances
            FieldData fieldData = new FieldData(FOFieldNames.Distance);
            fieldData.GlobalIncrementId = 1;
            fieldData.StepType = StepTypeEnum.Static;
            fieldData.Time = 1;
            fieldData.MethodId = 1;
            fieldData.StepId = 1;
            fieldData.StepIncrementId = 1;
            // Distances
            Field field = new Field(fieldData.Name);
            field.AddComponent(FOComponentNames.All, distancesAll);
            field.AddComponent(FOComponentNames.D1, distances1);
            field.AddComponent(FOComponentNames.D2, distances2);
            field.AddComponent(FOComponentNames.D3, distances3);
            results.AddField(fieldData, field);
            // Add forces
            fieldData = new FieldData(fieldData);
            fieldData.Name = FOFieldNames.ForcePerArea;
            //
            field = new Field(fieldData.Name);
            field.AddComponent(FOComponentNames.All, forcesAll);
            field.AddComponent(FOComponentNames.F1, forces1);
            field.AddComponent(FOComponentNames.F2, forces2);
            field.AddComponent(FOComponentNames.F3, forces3);
            fieldData.Unit = unitSystem.PressureUnitAbbreviation;
            results.AddField(fieldData, field);
            //
            return results;
        }
        public void GetForcePerAreaAndDistanceForPoint(double[] point, out double[] distance, out double[] values)
        {
            ImportLoad();
            //
            _interpolator.InterpolateAt(point, _interpolatorType, _interpolatorRadius.Value,
                                        out distance, out values);
            for (int i = 0; i < values.Length; i++) values[i] *= _magnitudeFactor.Value;
            
        }
        public double[] GetForcePerAreaForPoint(double[] point)
        {
            ImportLoad();
            //
            _interpolator.InterpolateAt(point, _interpolatorType, _interpolatorRadius.Value, 
                                        out double[] distance, out double[] values);
            for (int i = 0; i < values.Length; i++) values[i] *= _magnitudeFactor.Value;
            return values;
        }
        
    }
}
