// PrePoMax - Copyright (C) 2016-2026 Matej BorovinÅ¡ek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej BorovinÅ¡ek
// Contributors:

using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CRemoveModelCylinders : PreprocessCommand
    {
        private string[] _cylinderNames;

        public CRemoveModelCylinders(string[] cylinderNames)
            : base("Remove model cylinders")
        {
            _cylinderNames = cylinderNames;
        }

        public override bool Execute(Controller receiver)
        {
            receiver.RemoveModelCylinders(_cylinderNames);
            return true;
        }
        public override string GetCommandString()
        {
            return base.GetCommandString() + GetArrayAsString(_cylinderNames);
        }
    }
}
