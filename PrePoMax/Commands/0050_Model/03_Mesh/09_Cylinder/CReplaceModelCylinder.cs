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
    class CReplaceModelCylinder : PreprocessCommand
    {
        private string _oldCylinderName;
        private FeCylinder _newCylinder;

        public CReplaceModelCylinder(string oldCylinderName, FeCylinder newCylinder)
            : base("Edit model cylinder")
        {
            _oldCylinderName = oldCylinderName;
            _newCylinder = newCylinder.DeepClone();
        }

        public override bool Execute(Controller receiver)
        {
            receiver.ReplaceModelCylinder(_oldCylinderName, _newCylinder.DeepClone(), true);
            return true;
        }
        public override string GetCommandString()
        {
            return base.GetCommandString() + _oldCylinderName + ", " + _newCylinder;
        }
    }
}
