// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

// Legacy Windows process handoff. Keep BinaryFormatter-backed persistence outside the portable data contract.
using CaeGlobals;

namespace CaeMesh
{
    public partial class GmshData
    {
        public void WriteToFile(string fileName)
        {
            this.DumpToFile(fileName);
        }
    }
}
