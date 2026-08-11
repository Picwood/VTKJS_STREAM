// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace CaeMesh.Meshing.BaramMesh
{
    [Serializable]
    public class BaramMeshProject
    {
        public const int CurrentConfigurationVersion = 6;

        public BaramMeshConfiguration Configuration { get; set; }
        public string ProjectDirectory { get; set; }
        public string CaseDirectory { get; set; }
        public BaramStep WorkingStep { get; set; }

        public BaramMeshProject()
        {
            Configuration = new BaramMeshConfiguration();
            WorkingStep = BaramStep.Geometry;
        }
    }
}
