// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.IO;

namespace CaeMesh.Meshing.BaramMesh
{
    public class BaramOpenFoamCaseControlsWriter
    {
        public void WriteMinimalCaseControls(BaramMeshProject project)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (string.IsNullOrWhiteSpace(project.ProjectDirectory))
                throw new InvalidOperationException("The BaramMesh project directory is not set.");

            BaramCaseFileSystem fileSystem = new BaramCaseFileSystem(project.ProjectDirectory);
            fileSystem.CreateCaseDirectories();

            WriteIfMissing(Path.Combine(fileSystem.SystemPath, "controlDict"), GetControlDict());
            WriteIfMissing(Path.Combine(fileSystem.SystemPath, "fvSchemes"), GetFvSchemes());
            WriteIfMissing(Path.Combine(fileSystem.SystemPath, "fvSolution"), GetFvSolution());
        }

        private static void WriteIfMissing(string fileName, string text)
        {
            if (File.Exists(fileName)) return;
            File.WriteAllText(fileName, text);
        }

        private static string GetControlDict()
        {
            return
@"FoamFile
{
    version     2.0;
    format      ascii;
    class       dictionary;
    location    ""system"";
    object      controlDict;
}

application     baramMesh;
startFrom       startTime;
startTime       0;
stopAt          endTime;
endTime         3;
deltaT          1;
writeControl    timeStep;
writeInterval   1;
purgeWrite      0;
writeFormat     ascii;
writePrecision  10;
writeCompression off;
timeFormat      general;
timePrecision   6;
runTimeModifiable true;
functions       {};
";
        }

        private static string GetFvSchemes()
        {
            return
@"FoamFile
{
    version     2.0;
    format      ascii;
    class       dictionary;
    location    ""system"";
    object      fvSchemes;
}

ddtSchemes
{
    default     steadyState;
}

gradSchemes
{
    default     Gauss linear;
}

divSchemes
{
    default     none;
}

laplacianSchemes
{
    default     Gauss linear corrected;
}

interpolationSchemes
{
    default     linear;
}

snGradSchemes
{
    default     corrected;
}
";
        }

        private static string GetFvSolution()
        {
            return
@"FoamFile
{
    version     2.0;
    format      ascii;
    class       dictionary;
    location    ""system"";
    object      fvSolution;
}

solvers
{
}

SIMPLE
{
}

PISO
{
}

PIMPLE
{
}

relaxationFactors
{
}
";
        }
    }
}
