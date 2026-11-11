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
using CaeGlobals;

namespace CaeModel
{
    public partial class StepCollection
    {
        public void AddStep(Step step, bool copyBCsAndLoads)
        {
            if (copyBCsAndLoads && _steps.Count >= 1)
            {
                Step lastStep = _steps.Last();
                //
                foreach (var entry in lastStep.BoundaryConditions)
                {
                    step.AddBoundaryCondition(entry.Value.DeepClone());
                }
                foreach (var entry in lastStep.Loads)
                {
                    step.AddLoad(entry.Value.DeepClone());
                }
                foreach (var entry in lastStep.DefinedFields)
                {
                    step.AddDefinedField(entry.Value.DeepClone());
                }
            }
            _steps.Add(step);
        }
        public Dictionary<int, bool[]> GetAllZeroDisplacements(FeModel model)
        {
            int[] nodeIds;
            bool isZero;
            bool[] bcDsplacement;
            bool[] displacement;
            string nodeSetName;
            Dictionary<int, bool[]> nodeIdZeroDisplacements = new Dictionary<int, bool[]>();
            //
            foreach (var step in _steps)
            {
                foreach (var entry in step.BoundaryConditions)
                {
                    if (entry.Value.RegionType == RegionTypeEnum.NodeSetName)
                    {
                        nodeIds = model.Mesh.NodeSets[entry.Value.RegionName].Labels;
                    }
                    else if (entry.Value.RegionType == RegionTypeEnum.SurfaceName)
                    {
                        nodeSetName = model.Mesh.Surfaces[entry.Value.RegionName].NodeSetName;
                        nodeIds = model.Mesh.NodeSets[nodeSetName].Labels;
                    }
                    else if (entry.Value.RegionType == RegionTypeEnum.ReferencePointName)
                    {
                        nodeIds = new int[0];
                    }
                    else throw new NotSupportedException();
                    //
                    if (entry.Value is FixedBC fbc)
                    {
                        foreach (var nodeId in nodeIds)
                        {
                            if (nodeIdZeroDisplacements.TryGetValue(nodeId, out displacement))
                            {
                                displacement[0] = true;
                                displacement[1] = true;
                                displacement[2] = true;
                            }
                            else
                            {
                                nodeIdZeroDisplacements.Add(nodeId, new bool[] { true, true, true });
                            }
                        }
                    }
                    else if (entry.Value is DisplacementRotation dr)
                    {
                        isZero = false;
                        bcDsplacement = new bool[3];
                        for (int i = 0; i < 3; i++)
                        {
                            bcDsplacement[i] = dr.GetDofType(i + 1) == DOFType.Zero; // || dr.GetDofType(i + 1) == DOFType.Fixed;
                            isZero |= bcDsplacement[i];
                        }
                        if (isZero)
                        {
                            foreach (var nodeId in nodeIds)
                            {
                                if (nodeIdZeroDisplacements.TryGetValue(nodeId, out displacement))
                                {
                                    if (bcDsplacement[0]) displacement[0] = true;
                                    if (bcDsplacement[1]) displacement[1] = true;
                                    if (bcDsplacement[2]) displacement[2] = true;
                                }
                                else
                                {
                                    nodeIdZeroDisplacements.Add(nodeId, bcDsplacement.ToArray());   // copy
                                }
                            }
                        }
                    }
                }
            }
            //
            return nodeIdZeroDisplacements;
        }
        // Load
    }
}
