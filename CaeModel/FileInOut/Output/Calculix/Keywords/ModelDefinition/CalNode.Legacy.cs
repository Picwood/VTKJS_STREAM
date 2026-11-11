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
using CaeModel;
using CaeMesh;

namespace FileInOut.Output.Calculix
{
    internal partial class CalNode
    {
        public CalNode(FeModel model, List<FeNode> additionalNodes,
                       IDictionary<string, int[]> referencePointsNodeIds,
                       Dictionary<int, double[]> deformations)
            : this(model.Mesh.Nodes, model.Mesh.ReferencePoints, model.Properties.ModelSpace,
                   additionalNodes, referencePointsNodeIds, deformations)
        {
        }
    }
}
