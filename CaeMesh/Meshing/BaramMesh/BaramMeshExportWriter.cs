// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CaeMesh.Meshing.BaramMesh
{
    public class BaramMeshExportWriter
    {
        public const string FsiInterfacesFileName = "fsiInterfaces";

        public string ExportSingleRegionProject(string projectDirectory, string sourcePolyMeshPath,
                                                BaramMeshConfiguration configuration = null)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("The export project directory is required.", "projectDirectory");
            if (!IsPolyMesh(sourcePolyMeshPath))
                throw new InvalidOperationException("The source OpenFOAM polyMesh is incomplete.");
            //
            projectDirectory = Path.GetFullPath(projectDirectory);
            sourcePolyMeshPath = Path.GetFullPath(sourcePolyMeshPath);
            if (PathsOverlap(projectDirectory, sourcePolyMeshPath))
                throw new InvalidOperationException("The export project directory must be outside the source case.");
            if (Directory.Exists(projectDirectory) &&
                (Directory.EnumerateDirectories(projectDirectory).Any() ||
                 Directory.EnumerateFiles(projectDirectory).Any()))
                throw new InvalidOperationException("The export project directory must be new or empty.");
            Directory.CreateDirectory(projectDirectory);
            //
            string caseRoot = Path.Combine(projectDirectory, BaramCaseFileSystem.CaseDirectoryName);
            string systemPath = Path.Combine(caseRoot, BaramCaseFileSystem.SystemDirectoryName);
            string constantPath = Path.Combine(caseRoot, BaramCaseFileSystem.ConstantDirectoryName);
            string targetPolyMeshPath = Path.Combine(constantPath, BaramCaseFileSystem.PolyMeshDirectoryName);
            Directory.CreateDirectory(systemPath);
            Directory.CreateDirectory(constantPath);
            Directory.CreateDirectory(Path.Combine(constantPath, BaramCaseFileSystem.TriSurfaceDirectoryName));
            Directory.CreateDirectory(Path.Combine(caseRoot, "boundaryConditions"));
            //
            CopyDirectory(sourcePolyMeshPath, targetPolyMeshPath, true);
            WriteAscii(Path.Combine(caseRoot, BaramCaseFileSystem.FoamFileName), "");
            WriteAscii(Path.Combine(systemPath, "controlDict"), ControlDict);
            WriteAscii(Path.Combine(systemPath, "fvSchemes"), FvSchemes);
            WriteAscii(Path.Combine(systemPath, "fvSolution"), FvSolution);
            WriteFsiInterfaces(constantPath, configuration);
            return caseRoot;
        }

        public static string[] GetFsiInterfaceNames(BaramMeshConfiguration configuration)
        {
            if (configuration == null || configuration.Geometry == null) return new string[0];
            return configuration.Geometry.Values
                .Where(g => g != null &&
                            g.GeometryType == BaramGeometryType.Surface &&
                            g.CfdType == BaramCfdType.Boundary &&
                            g.BoundaryRole == BaramBoundaryRole.FsiInterface)
                .Select(g => BaramGeometryFactory.SanitizeGeometryName(g.Name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static bool IsPolyMesh(string polyMeshPath)
        {
            return !string.IsNullOrWhiteSpace(polyMeshPath) &&
                   File.Exists(Path.Combine(polyMeshPath, "points")) &&
                   File.Exists(Path.Combine(polyMeshPath, "faces")) &&
                   File.Exists(Path.Combine(polyMeshPath, "owner")) &&
                   File.Exists(Path.Combine(polyMeshPath, "boundary"));
        }

        private static bool PathsOverlap(string firstPath, string secondPath)
        {
            string first = NormalizeDirectory(firstPath);
            string second = NormalizeDirectory(secondPath);
            return first.StartsWith(second, StringComparison.OrdinalIgnoreCase) ||
                   second.StartsWith(first, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectory(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        }

        private static void CopyDirectory(string source, string target, bool skipSets)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);
            foreach (string directory in Directory.GetDirectories(source))
            {
                if (skipSets && string.Equals(Path.GetFileName(directory), "sets",
                                              StringComparison.OrdinalIgnoreCase)) continue;
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)), false);
            }
        }

        private static void WriteAscii(string fileName, string text)
        {
            File.WriteAllText(fileName, text, Encoding.ASCII);
        }

        private static void WriteFsiInterfaces(string constantPath, BaramMeshConfiguration configuration)
        {
            string[] names = GetFsiInterfaceNames(configuration);
            if (names.Length == 0) return;
            // This metadata is consumed by the BaramFlow/FEA coupling layer; OpenFOAM ignores it.
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FoamFile");
            sb.AppendLine("{");
            sb.AppendLine("    version     2.0;");
            sb.AppendLine("    format      ascii;");
            sb.AppendLine("    class       dictionary;");
            sb.AppendLine("    location    \"constant\";");
            sb.AppendLine("    object      fsiInterfaces;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("interfaces");
            sb.AppendLine("(");
            foreach (string name in names) sb.Append("    ").AppendLine(name);
            sb.AppendLine(");");
            WriteAscii(Path.Combine(constantPath, FsiInterfacesFileName), sb.ToString());
        }

        private const string ControlDict =
@"FoamFile
{
    version     2.0;
    format      ascii;
    class       dictionary;
    location    ""system"";
    object      controlDict;
}

deltaT          1;
writeInterval   1000;
writePrecision  12;
";

        private const string FvSchemes =
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
    default     Gauss linear;
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

wallDist
{
    method      meshWave;
}
";

        private const string FvSolution =
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
    p
    {
        solver          PCG;
        preconditioner
        {
            preconditioner  GAMG;
            smoother        DIC;
            tolerance       1e-05;
            relTol          0.1;
        }
        tolerance       1e-16;
        relTol          0.1;
        minIter         1;
        maxIter         5;
    }

    U
    {
        solver          PBiCGStab;
        preconditioner  DILU;
        tolerance       1e-16;
        relTol          0.1;
        minIter         1;
        maxIter         5;
    }
}

SIMPLE
{
    consistent      no;
    nNonOrthogonalCorrectors 0;
    pRefCell        0;
    pRefValue       0;
    residualControl
    {
        p           1e-160;
        U           1e-16;
    }
}

relaxationFactors
{
    fields
    {
        p           0.3;
    }
    equations
    {
        U           0.7;
    }
}
";
    }
}
