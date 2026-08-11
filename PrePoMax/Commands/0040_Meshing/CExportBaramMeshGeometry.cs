// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CExportBaramMeshGeometry : PreprocessCommand
    {
        public CExportBaramMeshGeometry()
            : base("Export BaramMesh geometry")
        {
        }

        public override bool Execute(Controller receiver)
        {
            receiver.ExportBaramMeshGeometry();
            return true;
        }
    }
}
