// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.IO;

namespace CaeModel.CFD.BaramFlow
{
    public class BaramFlowMaterialCaseWriter
    {
        private readonly OpenFoamDictionaryWriter _dictionaryWriter;

        public BaramFlowMaterialCaseWriter()
        {
            _dictionaryWriter = new OpenFoamDictionaryWriter();
        }

        public string Write(BaramFlowProject project, BaramFlowMaterialSettings settings)
        {
            if (project == null) throw new ArgumentNullException("project");
            return Write(project, settings, project.General, project.Models);
        }

        public string Write(BaramFlowProject project, BaramFlowMaterialSettings settings,
                            BaramFlowGeneralSettings general, BaramFlowModelsSettings models)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (settings == null) throw new ArgumentNullException("settings");
            if (general == null) throw new ArgumentNullException("general");
            if (models == null) throw new ArgumentNullException("models");
            project.Validate();
            general = general.Copy();
            general.Normalize();
            general.Validate();
            models = models.Copy();
            models.Normalize(general);
            models.Validate(general);
            settings = settings.Copy();
            settings.Normalize(general);
            settings.Validate(general);
            string fileName = Path.Combine(project.CaseDirectory, "constant", "thermophysicalProperties");
            _dictionaryWriter.WriteDictionary(fileName, "thermophysicalProperties", "constant",
                                              Build(settings, general, models));
            return fileName;
        }

        public OpenFoamDictionary Build(BaramFlowMaterialSettings settings, BaramFlowGeneralSettings general,
                                        BaramFlowModelsSettings models)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (general == null) throw new ArgumentNullException("general");
            if (models == null) throw new ArgumentNullException("models");
            general = general.Copy();
            general.Normalize();
            general.Validate();
            settings = settings.Copy();
            settings.Normalize(general);
            settings.Validate(general);
            models = models.Copy();
            models.Normalize(general);
            models.Validate(general);
            bool compressible = general.FlowType == BaramFlowFlowType.Compressible;
            bool inviscid = models.TurbulenceModel == BaramFlowTurbulenceModel.Inviscid;
            OpenFoamDictionary thermoType = new OpenFoamDictionary()
                .AddEntry("type", compressible ? "hePsiThermo" : "heRhoThermo")
                .AddEntry("mixture", "pureMixture")
                .AddEntry("transport", "const")
                .AddEntry("thermo", "hConst")
                .AddEntry("equationOfState",
                          settings.DensityModel == BaramFlowDensityModel.IdealGas ? "perfectGas" : "rhoConst")
                .AddEntry("specie", "specie")
                .AddEntry("energy", "sensibleEnthalpy");
            OpenFoamDictionary mixture = new OpenFoamDictionary();
            if (settings.DensityModel == BaramFlowDensityModel.Constant)
                mixture.AddEntry("equationOfState", new OpenFoamDictionary().AddEntry("rho", settings.Density));
            OpenFoamDictionary thermodynamics = new OpenFoamDictionary()
                .AddEntry("Cp", settings.SpecificHeat)
                .AddEntry("Hf", 0);
            if (general.SolverType == BaramFlowSolverType.DensityBased)
                thermodynamics.AddEntry("Tref", 0);
            mixture.AddEntry("thermodynamics", thermodynamics)
                   .AddEntry("transport", new OpenFoamDictionary()
                       .AddEntry("mu", inviscid ? 0.0 : settings.DynamicViscosity)
                       .AddEntry("Pr", settings.GetPrandtlNumber(models)))
                   .AddEntry("specie", new OpenFoamDictionary()
                       .AddEntry("nMoles", 1)
                       .AddEntry("molWeight", settings.MolecularWeight));
            return new OpenFoamDictionary()
                .AddEntry("thermoType", thermoType)
                .AddEntry("mixture", mixture)
                .AddEntry("includeViscousDissipation", "false")
                .AddEntry("includeKineticEnergy", "false")
                .AddEntry("includePressureWork", "false");
        }
    }
}
