// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeModel.CFD.BaramFlow;
using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CSetBaramFlowSection : PreprocessCommand
    {
        private readonly BaramFlowSection _section;

        public CSetBaramFlowSection(BaramFlowSection section)
            : base("Set BaramFlow section")
        {
            _section = section;
        }

        public override bool Execute(Controller receiver)
        {
            receiver.SetBaramFlowSection(_section);
            return true;
        }

        public override string GetCommandString()
        {
            return base.GetCommandString() + _section;
        }
    }
}
