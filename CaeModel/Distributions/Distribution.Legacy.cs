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
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using CaeGlobals;
using CaeMesh;
using CaeResults;
using DynamicTypeDescriptor;

namespace CaeModel
{
    public abstract partial class Distribution
    {
        public abstract void GetMagnitudesAndDistancesForPoints(FeModel model, double[][] points, out double[][] magnitudes,
                                                                out double[][] distances);
    }
}
