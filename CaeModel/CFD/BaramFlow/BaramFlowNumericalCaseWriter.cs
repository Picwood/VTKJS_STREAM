// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CaeModel.CFD.BaramFlow
{
    public class BaramFlowNumericalCaseWriter
    {
        private readonly OpenFoamDictionaryWriter _writer;

        public BaramFlowNumericalCaseWriter()
        {
            _writer = new OpenFoamDictionaryWriter();
        }

        public string[] Write(BaramFlowProject project, BaramFlowNumericalSettings settings)
        {
            if (project == null) throw new ArgumentNullException("project");
            return Write(project, settings, project.General, project.Models);
        }

        public string[] Write(BaramFlowProject project, BaramFlowNumericalSettings settings,
                              BaramFlowGeneralSettings general, BaramFlowModelsSettings models)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (settings == null) throw new ArgumentNullException("settings");
            project.Validate();
            settings = settings.Copy();
            general = general.Copy();
            general.Normalize();
            general.Validate();
            models = models.Copy();
            models.Normalize(general);
            models.Validate(general);
            settings.Validate(general, models);
            string system = Path.Combine(project.CaseDirectory, "system");
            Directory.CreateDirectory(system);
            string fvSolution = Path.Combine(system, "fvSolution");
            string fvSchemes = Path.Combine(system, "fvSchemes");
            _writer.WriteDictionary(fvSolution, "fvSolution", "system", BuildFvSolution(settings, general, models));
            _writer.WriteDictionary(fvSchemes, "fvSchemes", "system", BuildFvSchemes(settings, general, models));
            return new string[] { fvSolution, fvSchemes };
        }

        public OpenFoamDictionary BuildFvSolution(BaramFlowNumericalSettings settings,
                                                   BaramFlowGeneralSettings general,
                                                   BaramFlowModelsSettings models)
        {
            Validate(settings, general, models);
            bool compressible = general.FlowType == BaramFlowFlowType.Compressible;
            bool densityBased = general.SolverType == BaramFlowSolverType.DensityBased;
            bool transient = general.TimeMode == BaramFlowTimeMode.Transient;
            bool energy = models.EnergyEnabled;
            bool turbulence = IsTurbulent(models);
            string turbulencePattern = "(k|epsilon|omega|nuTilda|gammaInt|ReThetat)";
            string solvedPattern = turbulence
                                       ? "(U|k|epsilon|omega|nuTilda|gammaInt|ReThetat)"
                                       : "U";

            OpenFoamDictionary solvers = new OpenFoamDictionary();
            OpenFoamDictionary pressureSolver = BuildPressureSolver(compressible);
            solvers.AddEntry("\"(p|pcorr)\"", pressureSolver)
                   .AddEntry("\"(p|pcorr)Final\"", pressureSolver);
            if (energy)
            {
                OpenFoamDictionary energySolver = BuildEnergySolver();
                solvers.AddEntry("h", energySolver).AddEntry("hFinal", energySolver);
            }
            OpenFoamDictionary otherSolver = BuildOtherSolver();
            solvers.AddEntry(turbulence ? QuotePattern(solvedPattern) : solvedPattern, otherSolver)
                   .AddEntry(turbulence ? QuotePattern(solvedPattern + "Final") : solvedPattern + "Final", otherSolver);

            OpenFoamDictionary simpleResidual = new OpenFoamDictionary()
                .AddEntry("p", settings.PressureAbsoluteTolerance)
                .AddEntry("U", settings.MomentumAbsoluteTolerance);
            if (energy) simpleResidual.AddEntry("h", settings.EnergyAbsoluteTolerance);
            if (turbulence) simpleResidual.AddEntry(QuotePattern(turbulencePattern), settings.TurbulenceAbsoluteTolerance);
            OpenFoamDictionary simple = new OpenFoamDictionary()
                .AddEntry("consistent", settings.PressureVelocityCoupling == BaramFlowPressureVelocityCoupling.SimpleC ? "yes" : "no")
                .AddEntry("nNonOrthogonalCorrectors", settings.NumberOfNonOrthogonalCorrectors)
                .AddEntry("pRefCell", 0)
                .AddEntry("pRefValue", 0)
                .AddEntry("solveFlow", "yes")
                .AddEntry("solveEnergy", energy ? "yes" : "no")
                .AddEntry("solveSpecies", "no")
                .AddEntry("residualControl", simpleResidual);

            OpenFoamDictionary pimpleResidual = new OpenFoamDictionary()
                .AddEntry("p", Residual(settings.PressureAbsoluteTolerance, settings.PressureRelativeTolerance))
                .AddEntry("U", Residual(settings.MomentumAbsoluteTolerance, settings.MomentumRelativeTolerance));
            if (energy) pimpleResidual.AddEntry("h", Residual(settings.EnergyAbsoluteTolerance, settings.EnergyRelativeTolerance));
            if (turbulence)
                pimpleResidual.AddEntry(QuotePattern(turbulencePattern),
                                        Residual(settings.TurbulenceAbsoluteTolerance, settings.TurbulenceRelativeTolerance));
            OpenFoamDictionary pimple = new OpenFoamDictionary()
                .AddEntry("consistent", settings.PressureVelocityCoupling == BaramFlowPressureVelocityCoupling.SimpleC ? "yes" : "no")
                .AddEntry("momentumPredictor", settings.UseMomentumPredictor ? "on" : "off")
                .AddEntry("turbOnFinalIterOnly", "false")
                .AddEntry("nNonOrthogonalCorrectors", settings.NumberOfNonOrthogonalCorrectors)
                .AddEntry("nCorrectors", settings.NumberOfCorrectors)
                .AddEntry("nOuterCorrectors", settings.MaximumIterationsPerTimeStep)
                .AddEntry("moveMeshOuterCorrectors", "yes")
                .AddEntry("pRefCell", 0)
                .AddEntry("pRefValue", 0)
                .AddEntry("solveFlow", "yes")
                .AddEntry("solveEnergy", energy ? "yes" : "no")
                .AddEntry("solveSpecies", "no")
                .AddEntry("residualControl", pimpleResidual);

            OpenFoamDictionary fields = new OpenFoamDictionary();
            if (!densityBased)
                fields.AddEntry("p", settings.PressureRelaxation)
                      .AddEntry("pFinal", settings.PressureFinalRelaxation)
                      .AddEntry("rho", settings.DensityRelaxation)
                      .AddEntry("rhoFinal", settings.DensityFinalRelaxation);
            OpenFoamDictionary equations = new OpenFoamDictionary()
                .AddEntry("U", settings.MomentumRelaxation)
                .AddEntry("UFinal", settings.MomentumFinalRelaxation);
            if (energy)
                equations.AddEntry("h", settings.EnergyRelaxation)
                         .AddEntry("hFinal", settings.EnergyFinalRelaxation);
            if (turbulence)
                equations.AddEntry(QuotePattern(turbulencePattern), settings.TurbulenceRelaxation)
                         .AddEntry(QuotePattern(turbulencePattern + "Final"), settings.TurbulenceFinalRelaxation);

            OpenFoamDictionary result = new OpenFoamDictionary()
                .AddEntry("solvers", solvers)
                .AddEntry("SIMPLE", simple)
                .AddEntry("PIMPLE", pimple)
                .AddEntry("relaxationFactors", new OpenFoamDictionary().AddEntry("fields", fields).AddEntry("equations", equations));
            if (densityBased)
            {
                result.AddEntry("LU-SGS", BuildLuSgs(settings, transient, turbulence))
                      .AddEntry("Riemann", BuildRiemann(settings))
                      .AddEntry("fieldBounds", new OpenFoamDictionary()
                          .AddEntry("p", new OpenFoamRawValue("1e-06 1e+10"))
                          .AddEntry("rho", new OpenFoamRawValue("1e-06 1e+10"))
                          .AddEntry("h", new OpenFoamRawValue("1e-06 1e+10"))
                          .AddEntry("e", new OpenFoamRawValue("1e-06 1e+10"))
                          .AddEntry("rhoE", new OpenFoamRawValue("1e-06 1e+10"))
                          .AddEntry("T", new OpenFoamRawValue("1e-06 3e+4"))
                          .AddEntry("U", new OpenFoamRawValue("3e+4")));
            }
            return result;
        }

        public OpenFoamDictionary BuildFvSchemes(BaramFlowNumericalSettings settings,
                                                  BaramFlowGeneralSettings general,
                                                  BaramFlowModelsSettings models)
        {
            Validate(settings, general, models);
            if (general.SolverType == BaramFlowSolverType.DensityBased)
                return BuildDensityFvSchemes(settings, general, models);
            string bounded = general.TimeMode == BaramFlowTimeMode.Steady ? "bounded " : "";
            OpenFoamDictionary div = new OpenFoamDictionary()
                .AddEntry("default", "Gauss linear")
                .AddEntry("div(((rho*nuEff)*dev2(T(grad(U)))))", "Gauss linear");
            AddUpwind(div, "div(phi,U)", settings.MomentumDiscretization, bounded, "momentumReconGrad");
            AddUpwind(div, "div(rhoPhi,U)", settings.MomentumDiscretization, bounded, "momentumReconGrad");
            if (IsTurbulent(models))
                foreach (string field in GetTurbulenceFields(models, general))
                    AddUpwind(div, "div(phi," + field + ")", settings.TurbulenceDiscretization,
                              bounded, "turbulenceReconGrad");
            if (models.EnergyEnabled)
            {
                AddUpwind(div, "div(phi,h)", settings.EnergyDiscretization, bounded, "energyReconGrad");
                AddUpwind(div, "div(phi,K)", settings.EnergyDiscretization, bounded, "energyReconGrad");
            }
            string ddt = general.TimeMode == BaramFlowTimeMode.Steady
                             ? "steadyState"
                             : settings.TimeDiscretization == BaramFlowTimeDiscretization.FirstOrderImplicit
                                   ? "Euler" : "backward";
            return new OpenFoamDictionary()
                .AddEntry("ddtSchemes", new OpenFoamDictionary().AddEntry("default", ddt))
                .AddEntry("gradSchemes", new OpenFoamDictionary()
                    .AddEntry("default", "Gauss linear")
                    .AddEntry("momentumReconGrad", "VKLimited Gauss linear 1.0")
                    .AddEntry("energyReconGrad", "VKLimited Gauss linear 1.0")
                    .AddEntry("turbulenceReconGrad", "VKLimited Gauss linear 1.0"))
                .AddEntry("divSchemes", div)
                .AddEntry("laplacianSchemes", BuildLaplacian(settings))
                .AddEntry("interpolationSchemes", new OpenFoamDictionary()
                    .AddEntry("default", "linear")
                    .AddEntry("interpolate(p)", GetPressureInterpolation(settings.PressureInterpolation))
                    .AddEntry("reconstruct(psi)", "Minmod")
                    .AddEntry("reconstruct(p)", "Minmod")
                    .AddEntry("reconstruct(U)", "MinmodV")
                    .AddEntry("reconstruct(Dp)", "Minmod"))
                .AddEntry("snGradSchemes", new OpenFoamDictionary().AddEntry("default", "corrected"))
                .AddEntry("wallDist", new OpenFoamDictionary().AddEntry("method", "meshWave"));
        }

        private static OpenFoamDictionary BuildDensityFvSchemes(BaramFlowNumericalSettings settings,
                                                                 BaramFlowGeneralSettings general,
                                                                 BaramFlowModelsSettings models)
        {
            OpenFoamDictionary gradients = new OpenFoamDictionary()
                .AddEntry("default", "Gauss linear")
                .AddEntry("reconGrad", "VKLimited Gauss linear 0.5");
            OpenFoamDictionary div = new OpenFoamDictionary().AddEntry("default", "Gauss linear");
            foreach (string field in GetTurbulenceFields(models, general))
            {
                gradients.AddEntry("grad(" + field + ")", "VKLimited Gauss linear 0.5");
                AddUpwind(div, "div(phi," + field + ")", settings.TurbulenceDiscretization, "", "reconGrad");
            }
            return new OpenFoamDictionary()
                .AddEntry("ddtSchemes", new OpenFoamDictionary().AddEntry("default",
                    general.TimeMode == BaramFlowTimeMode.Transient ? "backwardDualTime" : "localEuler"))
                .AddEntry("gradSchemes", gradients)
                .AddEntry("divSchemes", div)
                .AddEntry("laplacianSchemes", BuildLaplacian(settings))
                .AddEntry("interpolationSchemes", new OpenFoamDictionary()
                    .AddEntry("default", "linear")
                    .AddEntry("interpolate(rho)", "linearUpwind phi grad(rho)"))
                .AddEntry("snGradSchemes", new OpenFoamDictionary().AddEntry("default", "corrected"))
                .AddEntry("wallDist", new OpenFoamDictionary().AddEntry("method", "meshWave"));
        }

        private static OpenFoamDictionary BuildPressureSolver(bool compressible)
        {
            if (compressible) return BuildOtherSolver();
            return new OpenFoamDictionary()
                .AddEntry("solver", "PCG")
                .AddEntry("preconditioner", new OpenFoamDictionary()
                    .AddEntry("preconditioner", "GAMG")
                    .AddEntry("smoother", "DIC")
                    .AddEntry("tolerance", new OpenFoamRawValue("1e-5"))
                    .AddEntry("relTol", 0.1))
                .AddEntry("tolerance", new OpenFoamRawValue("1e-16"))
                .AddEntry("relTol", 0.1)
                .AddEntry("minIter", 1)
                .AddEntry("maxIter", 5);
        }

        private static OpenFoamDictionary BuildEnergySolver()
        {
            return new OpenFoamDictionary()
                .AddEntry("solver", "PBiCGStab")
                .AddEntry("preconditioner", new OpenFoamDictionary()
                    .AddEntry("preconditioner", "GAMG")
                    .AddEntry("smoother", "DILU")
                    .AddEntry("tolerance", new OpenFoamRawValue("1e-5"))
                    .AddEntry("relTol", 0.1))
                .AddEntry("tolerance", new OpenFoamRawValue("1e-16"))
                .AddEntry("relTol", 0.1)
                .AddEntry("minIter", 1)
                .AddEntry("maxIter", 5);
        }

        private static OpenFoamDictionary BuildOtherSolver()
        {
            return new OpenFoamDictionary()
                .AddEntry("solver", "PBiCGStab")
                .AddEntry("preconditioner", "DILU")
                .AddEntry("tolerance", new OpenFoamRawValue("1e-16"))
                .AddEntry("relTol", 0.1)
                .AddEntry("minIter", 1)
                .AddEntry("maxIter", 5);
        }

        private static OpenFoamDictionary BuildLuSgs(BaramFlowNumericalSettings settings, bool transient,
                                                      bool turbulence)
        {
            OpenFoamDictionary residual = new OpenFoamDictionary();
            if (transient)
            {
                residual.AddEntry("rho", Residual(settings.DensityAbsoluteTolerance, settings.DensityRelativeTolerance))
                        .AddEntry("rhoU", Residual(settings.MomentumAbsoluteTolerance, settings.MomentumRelativeTolerance))
                        .AddEntry("rhoE", Residual(settings.EnergyAbsoluteTolerance, settings.EnergyRelativeTolerance));
                if (turbulence)
                    residual.AddEntry(QuotePattern("(k|epsilon|omega|nuTilda|gammaInt|ReThetat)"),
                                      Residual(settings.TurbulenceAbsoluteTolerance, settings.TurbulenceRelativeTolerance));
                return new OpenFoamDictionary().AddEntry("residualControl", residual)
                    .AddEntry("nPseudoTimeIterations", settings.MaximumIterationsPerTimeStep);
            }
            residual.AddEntry("rho", settings.DensityAbsoluteTolerance)
                    .AddEntry("rhoU", settings.MomentumAbsoluteTolerance)
                    .AddEntry("rhoE", settings.EnergyAbsoluteTolerance);
            if (turbulence)
                residual.AddEntry(QuotePattern("(k|epsilon|omega|nuTilda|gammaInt|ReThetat)"),
                                  settings.TurbulenceAbsoluteTolerance);
            return new OpenFoamDictionary().AddEntry("residualControl", residual);
        }

        private static OpenFoamDictionary BuildRiemann(BaramFlowNumericalSettings settings)
        {
            string flux;
            switch (settings.DensityFlux)
            {
                case BaramFlowDensityFlux.Ausm: flux = "AUSMplusFlux"; break;
                case BaramFlowDensityFlux.AusmUp: flux = "AUSMplusUpFlux"; break;
                default: flux = "roeFlux"; break;
            }
            return new OpenFoamDictionary()
                .AddEntry("fluxScheme", flux)
                .AddEntry("secondOrder", settings.MomentumDiscretization == BaramFlowUpwindDiscretization.SecondOrderUpwind ? "yes" : "no")
                .AddEntry("reconGradScheme", "VKLimited Gauss linear 1")
                .AddEntry("roeFluxCoeffs", new OpenFoamDictionary().AddEntry("epsilon", settings.EntropyFixCoefficient))
                .AddEntry("AUSMplusUpFluxCoeffs", new OpenFoamDictionary().AddEntry("MInf", settings.CutOffMachNumber));
        }

        private static OpenFoamDictionary BuildLaplacian(BaramFlowNumericalSettings settings)
        {
            string scheme = settings.HighOrderTermRelaxationEnabled
                                ? "Gauss linear limited corrected " + Format(settings.HighOrderTermRelaxationFactor)
                                : "Gauss linear corrected";
            return new OpenFoamDictionary().AddEntry("default", scheme);
        }

        private static OpenFoamDictionary Residual(double absolute, double relative)
        {
            return new OpenFoamDictionary().AddEntry("tolerance", absolute).AddEntry("relTol", relative);
        }

        private static void AddUpwind(OpenFoamDictionary dictionary, string key,
                                      BaramFlowUpwindDiscretization scheme, string prefix, string gradient)
        {
            dictionary.AddEntry(key, scheme == BaramFlowUpwindDiscretization.FirstOrderUpwind
                                         ? prefix + "Gauss upwind"
                                         : prefix + "Gauss linearUpwind " + gradient);
        }

        private static string[] GetTurbulenceFields(BaramFlowModelsSettings models, BaramFlowGeneralSettings general)
        {
            List<string> fields = new List<string>();
            foreach (BaramFlowSolverField field in models.GetRequiredFields(general))
            {
                if (field == BaramFlowSolverField.TurbulentKineticEnergy ||
                    field == BaramFlowSolverField.TurbulentDissipationRate ||
                    field == BaramFlowSolverField.SpecificDissipationRate ||
                    field == BaramFlowSolverField.ModifiedTurbulentViscosity ||
                    field == BaramFlowSolverField.Intermittency ||
                    field == BaramFlowSolverField.TransitionOnsetReynoldsNumber)
                    fields.Add(BaramFlowModelsSettings.GetOpenFoamFieldName(field));
            }
            return fields.ToArray();
        }

        private static bool IsTurbulent(BaramFlowModelsSettings models)
        {
            return models.TurbulenceModel != BaramFlowTurbulenceModel.Inviscid &&
                   models.TurbulenceModel != BaramFlowTurbulenceModel.Laminar;
        }

        private static string GetPressureInterpolation(BaramFlowPressureInterpolation interpolation)
        {
            switch (interpolation)
            {
                case BaramFlowPressureInterpolation.MomentumWeightedReconstruct: return "momentumWeightedReconstruct";
                case BaramFlowPressureInterpolation.MomentumWeighted: return "momentumWeighted";
                default: return "linear";
            }
        }

        private static string QuotePattern(string pattern)
        {
            return "\"" + pattern + "\"";
        }

        private static string Format(double value)
        {
            return value.ToString("G15", CultureInfo.InvariantCulture);
        }

        private static void Validate(BaramFlowNumericalSettings settings, BaramFlowGeneralSettings general,
                                     BaramFlowModelsSettings models)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (general == null) throw new ArgumentNullException("general");
            if (models == null) throw new ArgumentNullException("models");
            general.Validate();
            models.Validate(general);
            settings.Validate(general, models);
        }
    }
}
