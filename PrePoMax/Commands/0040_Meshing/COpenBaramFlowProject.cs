// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace PrePoMax.Commands
{
    [Serializable]
    class COpenBaramFlowProject : PreprocessCommand
    {
        private readonly string _projectDirectory;

        public COpenBaramFlowProject(string projectDirectory)
            : base("Open BaramFlow project")
        {
            _projectDirectory = projectDirectory;
        }

        public override bool Execute(Controller receiver)
        {
            receiver.OpenBaramFlowProject(_projectDirectory);
            return true;
        }

        public override string GetCommandString()
        {
            return base.GetCommandString() + _projectDirectory;
        }
    }
}
