// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CCreateBaramMeshCase : PreprocessCommand
    {
        private string _projectDirectory;

        public CCreateBaramMeshCase(string projectDirectory)
            : base("Create BaramMesh case")
        {
            _projectDirectory = projectDirectory;
        }

        public override bool Execute(Controller receiver)
        {
            receiver.CreateBaramMeshCase(_projectDirectory);
            return true;
        }

        public override string GetCommandString()
        {
            return base.GetCommandString() + _projectDirectory;
        }
    }
}
