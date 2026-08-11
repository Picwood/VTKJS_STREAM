// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeGlobals;
using CaeMesh.Meshing.BaramMesh;
using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CUpdateBaramMeshConfiguration : PreprocessCommand
    {
        private BaramMeshConfiguration _configuration;

        public CUpdateBaramMeshConfiguration(BaramMeshConfiguration configuration)
            : base("Update BaramMesh configuration")
        {
            _configuration = configuration.DeepClone();
        }

        public override bool Execute(Controller receiver)
        {
            receiver.UpdateBaramMeshConfiguration(_configuration.DeepClone());
            return true;
        }

        public override string GetCommandString()
        {
            return base.GetCommandString() + "version " + _configuration.Version + ", step " + _configuration.Step;
        }
    }
}
