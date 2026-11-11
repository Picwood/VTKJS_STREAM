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
using CaeGlobals;

namespace FileInOut.Output.Calculix
{
    internal partial class CalVariablePressureLoad
    {
        public CalVariablePressureLoad(FeModel model, VariablePressure load, ComplexLoadTypeEnum complexLoadType)
        {
            _load = load;
            _dLoads = model.GetElementDLoadsFromVariablePressureLoad(_load);
            _complexLoadType = complexLoadType;
            //
            _surfaceFaceType = model.Mesh.Surfaces[load.SurfaceName].SurfaceFaceTypes;
        }


    }
}
