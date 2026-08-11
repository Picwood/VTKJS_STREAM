// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.IO;

namespace CaeModel.CFD.BaramFlow
{
    public class BaramFlowRunCaseWriter
    {
        private readonly OpenFoamDictionaryWriter _writer;

        public BaramFlowRunCaseWriter()
        {
            _writer = new OpenFoamDictionaryWriter();
        }

        public string Write(BaramFlowProject project, BaramFlowRunSettings settings)
        {
            if (project == null) throw new ArgumentNullException("project");
            return Write(project, settings, project.General);
        }

        public string Write(BaramFlowProject project, BaramFlowRunSettings settings,
                            BaramFlowGeneralSettings general)
        {
            if (project == null) throw new ArgumentNullException("project");
            project.Validate();
            OpenFoamDictionary dictionary = BuildControlDict(settings, general);
            string system = Path.Combine(project.CaseDirectory, "system");
            Directory.CreateDirectory(system);
            string fileName = Path.Combine(system, "controlDict");
            _writer.WriteDictionary(fileName, "controlDict", "system", dictionary);
            string foamFile = Path.Combine(project.CaseDirectory, BaramCaseFileSystem.FoamFileName);
            if (!File.Exists(foamFile)) File.WriteAllText(foamFile, "");
            return fileName;
        }

        public OpenFoamDictionary BuildControlDict(BaramFlowRunSettings settings, BaramFlowGeneralSettings general)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (general == null) throw new ArgumentNullException("general");
            settings = settings.Copy();
            general = general.Copy();
            general.Normalize();
            general.Validate();
            settings.Normalize(general);
            settings.Validate(general);
            bool transient = general.TimeMode == BaramFlowTimeMode.Transient;
            bool adaptive = transient && settings.TimeSteppingMethod == BaramFlowTimeSteppingMethod.Adaptive;
            return new OpenFoamDictionary()
                .AddEntry("application", BaramFlowGeneralCaseWriter.GetApplication(general))
                .AddEntry("startFrom", "latestTime")
                .AddEntry("startTime", 0)
                .AddEntry("stopAt", "endTime")
                .AddEntry("endTime", transient ? settings.EndTime : settings.NumberOfIterations)
                .AddEntry("deltaT", transient ? (adaptive ? 0.001 : settings.TimeStepSize) : 1)
                .AddEntry("writeControl", adaptive ? "adjustableRunTime" : "runTime")
                .AddEntry("writeInterval", transient ? settings.ReportIntervalSeconds : settings.ReportIntervalSteps)
                .AddEntry("purgeWrite", settings.RetainOnlyMostRecentFiles ? settings.MaximumNumberOfDataFiles : 0)
                .AddEntry("writeFormat", settings.DataWriteFormat == BaramFlowDataWriteFormat.Binary ? "binary" : "ascii")
                .AddEntry("writePrecision", settings.DataWritePrecision)
                .AddEntry("writeCompression", "off")
                .AddEntry("writeAtEnd", "true")
                .AddEntry("timeFormat", "general")
                .AddEntry("timePrecision", settings.TimePrecision)
                .AddEntry("runTimeModifiable", "yes")
                .AddEntry("adjustTimeStep", adaptive ? "yes" : "no")
                .AddEntry("maxCo", settings.MaximumCourantNumber)
                .AddEntry("maxDi", settings.MaximumDiffusionNumber)
                .AddEntry("functions", new OpenFoamDictionary());
        }
    }
}
