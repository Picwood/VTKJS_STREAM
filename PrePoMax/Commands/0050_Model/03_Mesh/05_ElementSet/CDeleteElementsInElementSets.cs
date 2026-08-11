// PrePoMax - Copyright (C) 2016-2026 Matej BorovinÅ¡ek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej BorovinÅ¡ek
// Contributors:

using System;
using CaeGlobals;


namespace PrePoMax.Commands
{
    [Serializable]
    class CDeleteElementsInElementSets : PreprocessCommand
    {
        // Variables
        private string[] _elementSetNames;


        // Constructor
        public CDeleteElementsInElementSets(string[] elementSetNames)
            : base("Delete elements in element sets")
        {
            _elementSetNames = elementSetNames;
        }


        // Methods
        public override bool Execute(Controller receiver)
        {
            receiver.DeleteElementsInElementSets(_elementSetNames);
            return true;
        }
        public override string GetCommandString()
        {
            return base.GetCommandString() + GetArrayAsString(_elementSetNames);
        }
    }
}
