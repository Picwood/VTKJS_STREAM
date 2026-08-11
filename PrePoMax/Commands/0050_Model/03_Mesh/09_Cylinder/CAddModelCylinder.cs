// PrePoMax - Copyright (C) 2016-2026 Matej BorovinÅ¡ek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej BorovinÅ¡ek
// Contributors:

using CaeModel;
using CaeGlobals;
using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CAddModelCylinder : PreprocessCommand
    {
        private FeCylinder _cylinder;

        public CAddModelCylinder(FeCylinder cylinder)
            : base("Add model cylinder")
        {
            _cylinder = cylinder.DeepClone();
        }

        public override bool Execute(Controller receiver)
        {
            receiver.AddModelCylinder(_cylinder.DeepClone());
            return true;
        }
        public override string GetCommandString()
        {
            return base.GetCommandString() + _cylinder;
        }
    }
}
