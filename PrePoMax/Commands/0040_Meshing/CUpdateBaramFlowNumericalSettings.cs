// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeModel.CFD.BaramFlow;
using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CUpdateBaramFlowNumericalSettings : PreprocessCommand
    {
        private readonly BaramFlowNumericalSettings _settings;

        public CUpdateBaramFlowNumericalSettings(BaramFlowNumericalSettings settings)
            : base("Update BaramFlow Numerical Conditions")
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _settings = settings.Copy();
        }

        public override bool Execute(Controller receiver)
        {
            receiver.UpdateBaramFlowNumericalSettings(_settings.Copy());
            return true;
        }

        public override string GetCommandString()
        {
            return base.GetCommandString() + _settings.PressureVelocityCoupling;
        }
    }
}
