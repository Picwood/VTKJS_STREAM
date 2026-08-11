// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CExportBaramMeshCase : PreprocessCommand
    {
        private readonly string _projectDirectory;

        public CExportBaramMeshCase(string projectDirectory)
            : base("Export BaramMesh case")
        {
            _projectDirectory = projectDirectory;
        }

        public override bool Execute(Controller receiver)
        {
            receiver.ExportBaramMeshCase(_projectDirectory);
            return true;
        }
    }
}
