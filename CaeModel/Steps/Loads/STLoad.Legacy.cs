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
using System.Runtime.Serialization;
using CaeResults;
using System.IO;
using System.Runtime.InteropServices;

namespace CaeModel
{
    public partial class STLoad : IPreviewable
    {
        public FeResults GetPreview(FeModel model, string resultName, UnitSystem unitSystem)
        {
            FeMesh targetMesh = model.Mesh;
            PartExchangeData allData = new PartExchangeData();
            targetMesh.GetAllNodesAndCells(out allData.Nodes.Ids, out allData.Nodes.Coor, out allData.Cells.Ids,
                                           out allData.Cells.CellNodeIds, out allData.Cells.Types);
            //
            bool addDistances = _distributionName != Distribution.DefaultDistributionName &&
                model.Distributions[_distributionName] is MappedDistribution md &&
                md.InterpolatorType == CloudInterpolatorEnum.ClosestPoint;
            //
            double area = model.GetAreaForSTLoad(this);
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
            int count = 0;
            int nodeId;
            double[][] coor = new double[nodeSet.Labels.Length][];
            Dictionary<int, int> nodeIdArrayId = new Dictionary<int, int>();
            for (int i = 0; i < allData.Nodes.Coor.Length; i++)
            {
                nodeId = allData.Nodes.Ids[i];
                if (nodeIds.Contains(nodeId))
                {
                    coor[count] = allData.Nodes.Coor[i];
                    nodeIdArrayId[nodeId] = count;
                    count++;
                }
            }
            double[][] distances;
            double[][] forces;
            GetForcesPerAreaAndDistancesForPoints(model, coor, out distances, out forces);
            //
            CoordinateSystem coordinateSystem;
            model.Mesh.CoordinateSystems.TryGetValue(CoordinateSystemName, out coordinateSystem);
            //
            Parallel.For(0, forcesAll.Length, i =>
            //for (int i = 0; i < forcesAll.Length; i++)
            {
                int nId;
                int arrayId;
                double[] distance;
                double[] force;
                //
                nId = allData.Nodes.Ids[i];
                if (nodeIds.Contains(nId))
                {
                    arrayId = nodeIdArrayId[nId];
                    // Direction
                    if (coordinateSystem != null)
                        force = coordinateSystem.GetOrientedVectorAtPoint(forces[arrayId], coor[arrayId]);
                    else
                        force = forces[arrayId];
                    //
                    if (addDistances)
                    {
                        distance = distances[arrayId];
                        distances1[i] = (float)distance[0];
                        distances2[i] = (float)distance[1];
                        distances3[i] = (float)distance[2];
                        distancesAll[i] = (float)Math.Sqrt(distance[0] * distance[0] +
                                                           distance[1] * distance[1] +
                                                           distance[2] * distance[2]);
                    }
                    forces1[i] = (float)force[0];
                    forces2[i] = (float)force[1];
                    forces3[i] = (float)force[2];
                    forcesAll[i] = (float)Math.Sqrt(force[0] * force[0] +
                                                    force[1] * force[1] +
                                                    force[2] * force[2]);
                }
                else
                {
                    if (addDistances)
                    {
                        distances1[i] = float.NaN;
                        distances2[i] = float.NaN;
                        distances3[i] = float.NaN;
                        distancesAll[i] = float.NaN;
                    }
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
            // Prepare field data
            Field field;
            FieldData fieldData = new FieldData(FOFieldNames.Distance);
            fieldData.GlobalIncrementId = 1;
            fieldData.StepType = StepTypeEnum.Static;
            fieldData.Time = 1;
            fieldData.MethodId = 1;
            fieldData.StepId = 1;
            fieldData.StepIncrementId = 1;
            //
            if (addDistances)
            {
                // Distances
                field = new Field(fieldData.Name);
                field.AddComponent(FOComponentNames.All, distancesAll);
                field.AddComponent(FOComponentNames.D1, distances1);
                field.AddComponent(FOComponentNames.D2, distances2);
                field.AddComponent(FOComponentNames.D3, distances3);
                results.AddField(fieldData, field);
            }
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
        public void GetForcesPerAreaAndDistancesForPoints(FeModel model, double[][] points,
                                                          out double[][] distances, out double[][] values)
        {
            double area = model.GetAreaForSTLoad(this);
            distances = new double[points.Length][];
            values = new double[points.Length][];
            //
            if (_distributionName == Distribution.DefaultDistributionName)
            {
                double fpa1 = _f1.Value / area;
                double fpa2 = _f2.Value / area;
                double fpa3 = _f3.Value / area;
                for (int i = 0; i < points.Length; i++)
                {
                    distances[i] = null;
                    values[i] = new double[] { fpa1, fpa2, fpa3 };
                }
            }
            else
            {
                double p1 = _p1.Value;
                double p2 = _p2.Value;
                double p3 = _p3.Value;
                double[][] magnitudes;
                Distribution distribution = model.Distributions[_distributionName];
                //
                distribution.GetMagnitudesAndDistancesForPoints(model, points, out magnitudes, out distances);
                //
                for (int i = 0; i < points.Length; i++)
                {
                    if (magnitudes[i].Length == 1)
                        values[i] = new double[] { magnitudes[i][0] * p1, magnitudes[i][0] * p2, magnitudes[i][0] * p3 };
                    else if (magnitudes[i].Length == 3)
                        values[i] = new double[] { magnitudes[i][0] * p1, magnitudes[i][1] * p2, magnitudes[i][2] * p3 };
                    else throw new NotSupportedException();
                }
            }
        }
        public double[] GetForcePerAreaForPoint(FeModel model, double[] point)
        {
            double[][] points = new double[][] { point };
            double[][] values;
            GetForcesPerAreaAndDistancesForPoints(model, points, out _, out values);
            return values[0];
        }
    }
}
