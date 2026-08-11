// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Collections.Generic;

namespace CaeMesh.Meshing.BaramMesh
{
    public interface IBaramOpenFoamRunner
    {
        bool IsValidOpenFoamRoot(string openFoamRoot, out string message);
        int Run(string utilityName, string arguments, string workingDirectory, IDictionary<string, string> environment,
                Action<string> appendOutput);
        void Cancel();
    }

    public interface IBaramCaseFileSystem
    {
        string CaseRoot { get; }
        string SystemPath { get; }
        string ConstantPath { get; }
        string TriSurfacePath { get; }

        void CreateCaseDirectories();
        string GetPolyMeshPath(string regionName = null);
        string GetBoundaryFilePath(string regionName = null);
        string GetFoamFilePath();
        string GetProcessorPath(int processorNumber);
        string GetTimePath(string time, int? processorNumber = null);
    }

    public interface IBaramDictionaryWriter
    {
        string BuildDictionaryText(string objectName, string location, IDictionary<string, object> data);
        void WriteDictionary(string fileName, string objectName, string location, IDictionary<string, object> data);
        void WriteDictionary(string fileName, string objectName, string location,
                             IEnumerable<KeyValuePair<string, object>> data);
    }

    public interface IBaramGeometryExporter
    {
        void ExportGeometry(BaramMeshProject project, string triSurfaceDirectory);
    }

    public interface IBaramMeshWorkflow
    {
        void RunStep(BaramMeshProject project, BaramStep step, Action<string> appendOutput);
        void Cancel();
    }
}
