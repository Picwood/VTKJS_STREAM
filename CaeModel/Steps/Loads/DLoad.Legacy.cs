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
using System.Runtime.Serialization;
using System.Drawing;

namespace CaeModel
{
    public partial class DLoad : IPreviewable
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
            FeSurface surface = targetMesh.Surfaces[_surfaceName];
            FeNodeSet nodeSet = targetMesh.NodeSets[surface.NodeSetName];
            HashSet<int> nodeIds = new HashSet<int>(nodeSet.Labels);
            //
            float[] distancesAll = new float[allData.Nodes.Coor.Length];
            float[] distances1 = new float[allData.Nodes.Coor.Length];
            float[] distances2 = new float[allData.Nodes.Coor.Length];
            float[] distances3 = new float[allData.Nodes.Coor.Length];
            float[] pressuresAll = new float[allData.Nodes.Coor.Length];
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
            double[] pressures;
            GetPressuresAndDistancesForPoints(model, coor, out distances, out pressures);
            //
            Parallel.For(0, pressuresAll.Length, i =>
            //for (int i = 0; i < forcesAll.Length; i++)
            {
                int nId;
                int arrayId;
                double[] distance;
                double pressure;
                //
                nId = allData.Nodes.Ids[i];
                if (nodeIds.Contains(nId))
                {
                    arrayId = nodeIdArrayId[nId];
                    pressure = pressures[arrayId];
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
                    pressuresAll[i] = (float)pressure;
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
                    pressuresAll[i] = float.NaN;
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
            // Add pressure
            fieldData = new FieldData(fieldData);
            fieldData.Name = FOFieldNames.Pressure;
            //
            field = new Field(fieldData.Name);
            field.AddComponent(FOComponentNames.PRESS, pressuresAll);
            results.AddField(fieldData, field);
            //
            return results;
        }
        public void GetPressuresAndDistancesForPoints(FeModel model, double[][] points,
                                                      out double[][] distances, out double[] values)
        {
            distances = new double[points.Length][];
            values = new double[points.Length];
            double magnitude = _magnitude.Value;
            //
            if (_distributionName == Distribution.DefaultDistributionName)
            {
                for (int i = 0; i < points.Length; i++)
                {
                    distances[i] = null;
                    values[i] = magnitude;
                }
            }
            else
            {
                double[][] magnitudes;
                Distribution distribution = model.Distributions[_distributionName];
                //
                distribution.GetMagnitudesAndDistancesForPoints(model, points, out magnitudes, out distances);
                //
                for (int i = 0; i < points.Length; i++)
                {
                    if (magnitudes[i].Length == 1)
                        values[i] = magnitudes[i][0] * magnitude;
                    else if (magnitudes[i].Length == 3)
                        throw new CaeException("The selected distribution is not a scalar type distribution.");
                    else throw new NotSupportedException();
                }
            }
        }
        // Variable pressure
        public override double GetPressureForPoint(FeModel model, double[] point)
        {
            double[][] points = new double[][] { point };
            double[] values;
            GetPressuresAndDistancesForPoints(model, points, out _, out values);
            return values[0];
        }
        public override double[] GetPressuresForPoints(FeModel model, double[][] points)
        {
            double[] values;
            GetPressuresAndDistancesForPoints(model, points, out _, out values);
            return values;
        }
    }
}
