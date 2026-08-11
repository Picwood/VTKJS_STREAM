// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.Globalization;
using System.IO;

namespace CaeModel.CFD.BaramFlow
{
    public class BaramFlowGeneralCaseWriter
    {
        private readonly OpenFoamDictionaryWriter _dictionaryWriter;

        public BaramFlowGeneralCaseWriter()
        {
            _dictionaryWriter = new OpenFoamDictionaryWriter();
        }

        public string[] Write(BaramFlowProject project, BaramFlowGeneralSettings settings)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (settings == null) throw new ArgumentNullException("settings");
            project.Validate();
            settings = settings.Copy();
            settings.Normalize();
            settings.Validate();
            //
            string systemDirectory = Path.Combine(project.CaseDirectory, "system");
            string constantDirectory = Path.Combine(project.CaseDirectory, "constant");
            Directory.CreateDirectory(systemDirectory);
            Directory.CreateDirectory(constantDirectory);
            //
            string gravity = Path.Combine(constantDirectory, "g");
            string operatingConditions = Path.Combine(constantDirectory, "operatingConditions");
            //
            _dictionaryWriter.WriteDictionary(gravity, "g", "constant", BuildGravity(settings));
            _dictionaryWriter.WriteDictionary(operatingConditions, "operatingConditions", "constant",
                                              BuildOperatingConditions(settings));
            string foamFile = Path.Combine(project.CaseDirectory, BaramCaseFileSystem.FoamFileName);
            if (!File.Exists(foamFile)) File.WriteAllText(foamFile, "");
            return new string[] { gravity, operatingConditions };
        }

        public OpenFoamDictionary BuildGravity(BaramFlowGeneralSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            settings = settings.Copy();
            settings.Normalize();
            settings.Validate();
            string vector = "(" + Format(settings.GravityX) + " " + Format(settings.GravityY) + " " +
                            Format(settings.GravityZ) + ")";
            return new OpenFoamDictionary()
                .AddEntry("dimensions", new OpenFoamRawValue("[0 1 -2 0 0 0 0]"))
                .AddEntry("value", new OpenFoamRawValue(vector));
        }

        public OpenFoamDictionary BuildOperatingConditions(BaramFlowGeneralSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            settings = settings.Copy();
            settings.Normalize();
            settings.Validate();
            string value = "operatingPressure [1 -1 -2 0 0 0 0] " + Format(settings.OperatingPressure);
            return new OpenFoamDictionary()
                .AddEntry("operatingPressure", new OpenFoamRawValue(value));
        }

        public static string GetApplication(BaramFlowGeneralSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (settings.SolverType == BaramFlowSolverType.DensityBased)
                return settings.TimeMode == BaramFlowTimeMode.Transient ? "UTSLAeroFoam" : "TSLAeroFoam";
            return settings.TimeMode == BaramFlowTimeMode.Transient ? "buoyantPimpleNFoam" : "buoyantSimpleNFoam";
        }

        private static string Format(double value)
        {
            return value.ToString("G15", CultureInfo.InvariantCulture);
        }
    }
}
