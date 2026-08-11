// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.IO;

namespace CaeModel.CFD.BaramFlow
{
    public class BaramFlowModelsCaseWriter
    {
        private const double MaximumViscosityRatio = 100000;
        private readonly OpenFoamDictionaryWriter _dictionaryWriter;

        public BaramFlowModelsCaseWriter()
        {
            _dictionaryWriter = new OpenFoamDictionaryWriter();
        }

        public string Write(BaramFlowProject project, BaramFlowModelsSettings settings)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (settings == null) throw new ArgumentNullException("settings");
            project.Validate();
            settings = settings.Copy();
            settings.Normalize(project.General);
            settings.Validate(project.General);
            string fileName = Path.Combine(project.CaseDirectory, "constant", "turbulenceProperties");
            _dictionaryWriter.WriteDictionary(fileName, "turbulenceProperties", "constant", Build(settings));
            return fileName;
        }

        public OpenFoamDictionary Build(BaramFlowModelsSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            switch (settings.TurbulenceModel)
            {
                case BaramFlowTurbulenceModel.Inviscid:
                case BaramFlowTurbulenceModel.Laminar:
                    return new OpenFoamDictionary().AddEntry("simulationType", "laminar");
                case BaramFlowTurbulenceModel.SpalartAllmaras:
                    return BuildRas("SpalartAllmaras", settings);
                case BaramFlowTurbulenceModel.KEpsilon:
                    return BuildKEpsilon(settings);
                case BaramFlowTurbulenceModel.KOmegaSst:
                    return BuildRas("kOmegaSST", settings);
                case BaramFlowTurbulenceModel.TransitionSst:
                    return BuildRas("kOmegaSSTLM", settings);
                case BaramFlowTurbulenceModel.Des:
                    return BuildDes(settings);
                case BaramFlowTurbulenceModel.Les:
                    return BuildLes(settings);
                default:
                    throw new InvalidOperationException("The BaramFlow turbulence model is not supported.");
            }
        }

        private static OpenFoamDictionary BuildKEpsilon(BaramFlowModelsSettings settings)
        {
            string model;
            if (settings.KEpsilonModel == BaramFlowKEpsilonModel.Standard) model = "kEpsilon";
            else if (settings.KEpsilonModel == BaramFlowKEpsilonModel.Rng) model = "RNGkEpsilon";
            else if (settings.NearWallTreatment == BaramFlowNearWallTreatment.EnhancedWallTreatment)
                model = "realizableKEtwoLayer";
            else model = "realizableKE";
            OpenFoamDictionary data = BuildRas(model, settings);
            OpenFoamDictionary ras = (OpenFoamDictionary)data[1].Value;
            ras.AddEntry("kEpsilonCoeffs", new OpenFoamDictionary()
                .AddEntry("Prt", settings.EnergyPrandtlNumber)
                .AddEntry("Sct", settings.TurbulentSchmidtNumber));
            if (model == "realizableKEtwoLayer")
            {
                ras.AddEntry("ReyStar", settings.EnhancedWallThreshold);
                ras.AddEntry("deltaRey", settings.EnhancedWallBlendingWidth);
            }
            return data;
        }

        private static OpenFoamDictionary BuildRas(string model, BaramFlowModelsSettings settings)
        {
            return new OpenFoamDictionary()
                .AddEntry("simulationType", "RAS")
                .AddEntry("RAS", new OpenFoamDictionary()
                    .AddEntry("RASModel", model)
                    .AddEntry("turbulence", "on")
                    .AddEntry("printCoeffs", "on")
                    .AddEntry("viscosityRatioMax", MaximumViscosityRatio));
        }

        private static OpenFoamDictionary BuildDes(BaramFlowModelsSettings settings)
        {
            string shielding = settings.DelayedDes
                                   ? (settings.ShieldingFunction == BaramFlowShieldingFunction.Iddes ? "IDDES" : "DDES")
                                   : "DES";
            string model = settings.DesRansModel == BaramFlowDesRansModel.SpalartAllmaras
                               ? "SpalartAllmaras" + shielding
                               : "kOmegaSST" + shielding;
            string delta = settings.DelayedDes && settings.ShieldingFunction == BaramFlowShieldingFunction.Iddes
                               ? "IDDESDelta"
                               : GetLengthScaleName(settings.DesLengthScale);
            OpenFoamDictionary les = new OpenFoamDictionary()
                .AddEntry("LESModel", model)
                .AddEntry("turbulence", "on")
                .AddEntry("delta", delta)
                .AddEntry("viscosityRatioMax", MaximumViscosityRatio);
            if (settings.DesRansModel == BaramFlowDesRansModel.SpalartAllmaras)
            {
                les.AddEntry(model + "Coeffs", new OpenFoamDictionary()
                    .AddEntry("CDES", settings.DesCoefficient)
                    .AddEntry("lowReCorrection", settings.DesLowReDamping ? "yes" : "no")
                    .AddEntry("Sct", settings.TurbulentSchmidtNumber));
            }
            else
            {
                les.AddEntry(model, new OpenFoamDictionary()
                    .AddEntry("CDESkom", settings.DesKOmegaCoefficient)
                    .AddEntry("CDESkeps", settings.DesKEpsilonCoefficient)
                    .AddEntry("Sct", settings.TurbulentSchmidtNumber));
            }
            AddLengthScaleCoefficients(les, delta);
            return new OpenFoamDictionary().AddEntry("simulationType", "LES").AddEntry("LES", les);
        }

        private static OpenFoamDictionary BuildLes(BaramFlowModelsSettings settings)
        {
            string model = GetLesModelName(settings.LesSubgridScaleModel);
            string delta = GetLengthScaleName(settings.LesLengthScale);
            OpenFoamDictionary les = new OpenFoamDictionary()
                .AddEntry("LESModel", model)
                .AddEntry("turbulence", "on")
                .AddEntry("printCoeffs", "off")
                .AddEntry("delta", delta)
                .AddEntry("viscosityRatioMax", MaximumViscosityRatio);
            if (settings.LesSubgridScaleModel == BaramFlowLesSubgridScaleModel.DynamicKEqn)
            {
                les.AddEntry("dynamicKEqnCoeffs", new OpenFoamDictionary()
                    .AddEntry("filter", "simple")
                    .AddEntry("Sct", settings.TurbulentSchmidtNumber));
            }
            else
            {
                OpenFoamDictionary coefficients = new OpenFoamDictionary()
                    .AddEntry("Ck", settings.LesCk)
                    .AddEntry("Ce", settings.LesCe);
                if (settings.LesSubgridScaleModel == BaramFlowLesSubgridScaleModel.Wale)
                    coefficients.AddEntry("Cw", settings.LesCw);
                coefficients.AddEntry("Sct", settings.TurbulentSchmidtNumber);
                les.AddEntry(model + "Coeffs", coefficients);
            }
            AddLengthScaleCoefficients(les, delta);
            return new OpenFoamDictionary().AddEntry("simulationType", "LES").AddEntry("LES", les);
        }

        private static void AddLengthScaleCoefficients(OpenFoamDictionary les, string delta)
        {
            if (delta == "IDDESDelta")
            {
                les.AddEntry("IDDESDeltaCoeffs", new OpenFoamDictionary()
                    .AddEntry("hmax", "maxDeltaxyzCubeRoot")
                    .AddEntry("maxDeltaxyzCubeRootCoeffs", new OpenFoamDictionary()));
            }
            else if (delta == "vanDriest")
            {
                les.AddEntry("vanDriestCoeffs", new OpenFoamDictionary()
                    .AddEntry("delta", "cubeRootVol")
                    .AddEntry("cubeRootVolCoeffs", new OpenFoamDictionary().AddEntry("deltaCoeff", 2.0))
                    .AddEntry("kappa", 0.41)
                    .AddEntry("Aplus", 26)
                    .AddEntry("Cdelta", 0.158)
                    .AddEntry("calcInterval", 1));
            }
            else if (delta == "smooth")
            {
                les.AddEntry("smoothCoeffs", new OpenFoamDictionary()
                    .AddEntry("delta", "cubeRootVol")
                    .AddEntry("cubeRootVolCoeffs", new OpenFoamDictionary().AddEntry("deltaCoeff", 1))
                    .AddEntry("maxDeltaRatio", 1.1));
            }
            else
            {
                les.AddEntry("cubeRootVolCoeffs", new OpenFoamDictionary().AddEntry("deltaCoeff", 1));
            }
        }

        private static string GetLengthScaleName(BaramFlowLengthScaleModel model)
        {
            if (model == BaramFlowLengthScaleModel.VanDriest) return "vanDriest";
            if (model == BaramFlowLengthScaleModel.Smooth) return "smooth";
            return "cubeRootVol";
        }

        private static string GetLesModelName(BaramFlowLesSubgridScaleModel model)
        {
            if (model == BaramFlowLesSubgridScaleModel.Wale) return "WALE";
            if (model == BaramFlowLesSubgridScaleModel.DynamicKEqn) return "dynamicKEqn";
            if (model == BaramFlowLesSubgridScaleModel.KEqn) return "kEqn";
            return "Smagorinsky";
        }
    }
}
