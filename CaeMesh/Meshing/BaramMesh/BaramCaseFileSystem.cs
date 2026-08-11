// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Globalization;
using System.IO;

namespace CaeMesh.Meshing.BaramMesh
{
    public class BaramCaseFileSystem : IBaramCaseFileSystem
    {
        public const string CaseDirectoryName = "case";
        public const string SystemDirectoryName = "system";
        public const string ConstantDirectoryName = "constant";
        public const string TriSurfaceDirectoryName = "triSurface";
        public const string PolyMeshDirectoryName = "polyMesh";
        public const string BoundaryFileName = "boundary";
        public const string FoamFileName = "case.foam";

        public string CaseRoot { get; private set; }
        public string SystemPath { get { return Path.Combine(CaseRoot, SystemDirectoryName); } }
        public string ConstantPath { get { return Path.Combine(CaseRoot, ConstantDirectoryName); } }
        public string TriSurfacePath { get { return Path.Combine(ConstantPath, TriSurfaceDirectoryName); } }

        public BaramCaseFileSystem(string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("Project directory is required.", "projectDirectory");

            CaseRoot = Path.Combine(projectDirectory, CaseDirectoryName);
        }

        public void CreateCaseDirectories()
        {
            Directory.CreateDirectory(CaseRoot);
            Directory.CreateDirectory(SystemPath);
            Directory.CreateDirectory(ConstantPath);
            Directory.CreateDirectory(TriSurfacePath);
        }

        public string GetPolyMeshPath(string regionName = null)
        {
            if (string.IsNullOrWhiteSpace(regionName)) return Path.Combine(ConstantPath, PolyMeshDirectoryName);
            return Path.Combine(ConstantPath, regionName, PolyMeshDirectoryName);
        }

        public string GetBoundaryFilePath(string regionName = null)
        {
            return Path.Combine(GetPolyMeshPath(regionName), BoundaryFileName);
        }

        public string GetFoamFilePath()
        {
            return Path.Combine(CaseRoot, FoamFileName);
        }

        public string GetProcessorPath(int processorNumber)
        {
            if (processorNumber < 0) throw new ArgumentOutOfRangeException("processorNumber");
            return Path.Combine(CaseRoot, "processor" + processorNumber.ToString(CultureInfo.InvariantCulture));
        }

        public string GetTimePath(string time, int? processorNumber = null)
        {
            if (string.IsNullOrWhiteSpace(time)) throw new ArgumentException("Time directory is required.", "time");
            if (processorNumber.HasValue) return Path.Combine(GetProcessorPath(processorNumber.Value), time);
            return Path.Combine(CaseRoot, time);
        }
    }
}
