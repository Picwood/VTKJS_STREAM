// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeModel.CFD.BaramFlow;
using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class CUpdateBaramFlowMaterialSettings : PreprocessCommand
    {
        private readonly BaramFlowMaterialSettings _settings;

        public CUpdateBaramFlowMaterialSettings(BaramFlowMaterialSettings settings)
            : base("Update BaramFlow Material settings")
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _settings = settings.Copy();
        }

        public override bool Execute(Controller receiver)
        {
            receiver.UpdateBaramFlowMaterialSettings(_settings.Copy());
            return true;
        }

        public override string GetCommandString()
        {
            return base.GetCommandString() + _settings.Name + ", " + _settings.Phase;
        }
    }
}
