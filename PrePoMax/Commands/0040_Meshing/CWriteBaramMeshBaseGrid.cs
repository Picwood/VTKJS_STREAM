// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CWriteBaramMeshBaseGrid : PreprocessCommand
    {
        public CWriteBaramMeshBaseGrid()
            : base("Write BaramMesh base grid")
        {
        }

        public override bool Execute(Controller receiver)
        {
            receiver.WriteBaramMeshBaseGrid();
            return true;
        }
    }
}
