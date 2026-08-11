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
    public class BaramFlowBoundaryCaseWriter
    {
        private readonly OpenFoamDictionaryWriter _writer;

        public BaramFlowBoundaryCaseWriter()
        {
            _writer = new OpenFoamDictionaryWriter();
        }

        public string[] Write(BaramFlowProject project, BaramFlowBoundarySettings settings)
        {
            if (project == null) throw new ArgumentNullException("project");
            return Write(project, settings, project.General, project.Models, project.Material);
        }

        public string[] Write(BaramFlowProject project, BaramFlowBoundarySettings settings,
                              BaramFlowGeneralSettings general, BaramFlowModelsSettings models,
                              BaramFlowMaterialSettings material)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (settings == null) throw new ArgumentNullException("settings");
            project.Validate();
            settings = settings.Copy();
            settings.Validate();
            general = general.Copy();
            general.Normalize();
            general.Validate();
            models = models.Copy();
            models.Normalize(general);
            models.Validate(general);
            material = material.Copy();
            material.Normalize(general);
            material.Validate(general);
            string zeroDirectory = Path.Combine(project.CaseDirectory, "0");
            Directory.CreateDirectory(zeroDirectory);
            List<string> files = new List<string>();
            foreach (BaramFlowSolverField field in models.GetRequiredFields(general))
            {
                string name = BaramFlowModelsSettings.GetOpenFoamFieldName(field);
                string fileName = Path.Combine(zeroDirectory, name);
                string className = field == BaramFlowSolverField.Velocity ? "volVectorField" : "volScalarField";
                _writer.WriteDictionary(fileName, name, "0", className,
                                        BuildField(field, settings, general, models, material));
                files.Add(fileName);
            }
            RemoveInactiveFields(zeroDirectory, files);
            return files.ToArray();
        }

        public OpenFoamDictionary BuildField(BaramFlowSolverField field, BaramFlowBoundarySettings settings,
                                             BaramFlowGeneralSettings general, BaramFlowModelsSettings models,
                                             BaramFlowMaterialSettings material)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (general == null) throw new ArgumentNullException("general");
            if (models == null) throw new ArgumentNullException("models");
            if (material == null) throw new ArgumentNullException("material");
            settings = settings.Copy();
            settings.Validate();
            general = general.Copy();
            general.Normalize();
            general.Validate();
            models = models.Copy();
            models.Normalize(general);
            models.Validate(general);
            material = material.Copy();
            material.Normalize(general);
            material.Validate(general);
            if (Array.IndexOf(models.GetRequiredFields(general), field) < 0)
                throw new InvalidOperationException("Field " + field + " is not active for the selected Models configuration.");
            FieldValues initial = GetInitialValues(settings, models, material);
            OpenFoamDictionary boundaryField = new OpenFoamDictionary();
            foreach (BaramFlowBoundaryCondition boundary in settings.Boundaries)
                boundaryField.AddEntry(boundary.Name, BuildPatch(field, boundary, initial, general, models, material));
            return new OpenFoamDictionary()
                .AddEntry("dimensions", new OpenFoamRawValue(GetDimensions(field)))
                .AddEntry("internalField", new OpenFoamRawValue("uniform " + GetInternalValue(field, settings, initial, general)))
                .AddEntry("boundaryField", boundaryField);
        }

        private static OpenFoamDictionary BuildPatch(BaramFlowSolverField field, BaramFlowBoundaryCondition boundary,
                                                      FieldValues initial, BaramFlowGeneralSettings general,
                                                      BaramFlowModelsSettings models, BaramFlowMaterialSettings material)
        {
            if (boundary.BoundaryType == BaramFlowBoundaryType.Symmetry)
                return TypeOnly("symmetry");
            bool wall = boundary.BoundaryType == BaramFlowBoundaryType.Wall ||
                        boundary.BoundaryType == BaramFlowBoundaryType.FsiInterface;
            if (field == BaramFlowSolverField.Pressure)
            {
                if (boundary.BoundaryType == BaramFlowBoundaryType.PressureOutlet)
                    return new OpenFoamDictionary().AddEntry("type", "totalPressure")
                        .AddEntry("p0", new OpenFoamRawValue("uniform " + Format(general.OperatingPressure + boundary.GaugePressure)));
                if (wall)
                    return TypeOnly(general.SolverType == BaramFlowSolverType.DensityBased ? "zeroGradient" : "fixedFluxPressure");
                return TypeOnly("zeroGradient");
            }
            if (field == BaramFlowSolverField.Velocity)
            {
                if (boundary.BoundaryType == BaramFlowBoundaryType.VelocityInlet)
                    return Fixed(Vector(boundary.VelocityX, boundary.VelocityY, boundary.VelocityZ));
                if (boundary.BoundaryType == BaramFlowBoundaryType.PressureOutlet)
                    return new OpenFoamDictionary().AddEntry("type", "pressureInletOutletVelocity")
                        .AddEntry("value", new OpenFoamRawValue("uniform " + Vector(initial.Ux, initial.Uy, initial.Uz)));
                return wall ? Fixed("(0 0 0)") : TypeOnly("zeroGradient");
            }
            if (field == BaramFlowSolverField.Temperature)
            {
                if (boundary.BoundaryType == BaramFlowBoundaryType.VelocityInlet)
                    return Fixed(Format(boundary.Temperature));
                return TypeOnly("zeroGradient");
            }
            if (wall) return BuildWallTurbulence(field, initial, models);
            if (boundary.BoundaryType == BaramFlowBoundaryType.PressureOutlet) return TypeOnly("zeroGradient");
            return BuildInletTurbulence(field, boundary, initial, models, material);
        }

        private static OpenFoamDictionary BuildWallTurbulence(BaramFlowSolverField field, FieldValues values,
                                                               BaramFlowModelsSettings models)
        {
            switch (field)
            {
                case BaramFlowSolverField.TurbulentViscosity:
                    string nutType = models.TurbulenceModel == BaramFlowTurbulenceModel.KEpsilon &&
                                     !(models.KEpsilonModel == BaramFlowKEpsilonModel.Realizable &&
                                       models.NearWallTreatment == BaramFlowNearWallTreatment.EnhancedWallTreatment)
                                         ? "nutkWallFunction" : "nutSpaldingWallFunction";
                    return ValueType(nutType, values.Nut);
                case BaramFlowSolverField.TurbulentThermalDiffusivity:
                    return new OpenFoamDictionary().AddEntry("type", "compressible::alphatWallFunction")
                        .AddEntry("Prt", models.WallPrandtlNumber)
                        .AddEntry("value", new OpenFoamRawValue("uniform " + Format(values.Alphat)));
                case BaramFlowSolverField.TurbulentKineticEnergy: return ValueType("kqRWallFunction", values.K);
                case BaramFlowSolverField.TurbulentDissipationRate:
                    return ValueType(models.KEpsilonModel == BaramFlowKEpsilonModel.Realizable &&
                                     models.NearWallTreatment == BaramFlowNearWallTreatment.EnhancedWallTreatment
                                         ? "epsilonBlendedWallFunction" : "epsilonWallFunction", values.Epsilon);
                case BaramFlowSolverField.SpecificDissipationRate: return ValueType("omegaBlendedWallFunction", values.Omega);
                default: return TypeOnly("zeroGradient");
            }
        }

        private static OpenFoamDictionary BuildInletTurbulence(BaramFlowSolverField field,
                                                                BaramFlowBoundaryCondition boundary,
                                                                FieldValues values,
                                                                BaramFlowModelsSettings models,
                                                                BaramFlowMaterialSettings material)
        {
            FieldValues inlet = GetTurbulenceValues(boundary.VelocityX, boundary.VelocityY, boundary.VelocityZ,
                                                     boundary.TurbulenceIntensity,
                                                     boundary.TurbulentViscosityRatio, material);
            inlet.Alphat = material.Density * inlet.Nut / models.EnergyPrandtlNumber;
            switch (field)
            {
                case BaramFlowSolverField.TurbulentViscosity: return ValueType("calculated", inlet.Nut);
                case BaramFlowSolverField.TurbulentThermalDiffusivity: return ValueType("calculated", inlet.Alphat);
                case BaramFlowSolverField.TurbulentKineticEnergy:
                    return new OpenFoamDictionary().AddEntry("type", "turbulentIntensityInletOutletTKE")
                        .AddEntry("turbIntensity", new OpenFoamRawValue("uniform " + Format(boundary.TurbulenceIntensity)))
                        .AddEntry("value", new OpenFoamRawValue("uniform " + Format(inlet.K)));
                case BaramFlowSolverField.TurbulentDissipationRate:
                    return new OpenFoamDictionary().AddEntry("type", "viscosityRatioInletOutletTDR")
                        .AddEntry("viscosityRatio", new OpenFoamRawValue("uniform " + Format(boundary.TurbulentViscosityRatio)))
                        .AddEntry("value", new OpenFoamRawValue("uniform " + Format(inlet.Epsilon)));
                case BaramFlowSolverField.SpecificDissipationRate:
                    return new OpenFoamDictionary().AddEntry("type", "viscosityRatioInletOutletTDR")
                        .AddEntry("viscosityRatio", new OpenFoamRawValue("uniform " + Format(boundary.TurbulentViscosityRatio)))
                        .AddEntry("value", new OpenFoamRawValue("uniform " + Format(inlet.Omega)));
                case BaramFlowSolverField.ModifiedTurbulentViscosity: return Fixed(Format(inlet.NuTilda));
                case BaramFlowSolverField.Intermittency: return Fixed("1");
                case BaramFlowSolverField.TransitionOnsetReynoldsNumber: return Fixed("200");
                default: return ValueType("calculated", values.Nut);
            }
        }

        private static FieldValues GetInitialValues(BaramFlowBoundarySettings settings, BaramFlowModelsSettings models,
                                                     BaramFlowMaterialSettings material)
        {
            FieldValues values = GetTurbulenceValues(settings.InitialVelocityX, settings.InitialVelocityY,
                                                      settings.InitialVelocityZ, settings.InitialTurbulenceIntensity,
                                                      settings.InitialTurbulentViscosityRatio, material);
            values.Ux = settings.InitialVelocityX;
            values.Uy = settings.InitialVelocityY;
            values.Uz = settings.InitialVelocityZ;
            values.Alphat = material.Density * values.Nut / models.EnergyPrandtlNumber;
            return values;
        }

        private static FieldValues GetTurbulenceValues(double ux, double uy, double uz, double intensity,
                                                        double viscosityRatio, BaramFlowMaterialSettings material)
        {
            double speed = Math.Sqrt(ux * ux + uy * uy + uz * uz);
            speed = Math.Max(speed, 1E-3);
            double nu = material.DynamicViscosity / material.Density;
            double k = Math.Max(1.5 * Math.Pow(speed * intensity, 2), 1E-12);
            double nut = Math.Max(viscosityRatio * nu, 1E-15);
            return new FieldValues
            {
                K = k,
                Nut = nut,
                Alphat = material.Density * nut / 0.85,
                Epsilon = Math.Max(0.09 * k * k / nut, 1E-12),
                Omega = Math.Max(k / nut, 1E-12),
                NuTilda = nut
            };
        }

        private static string GetInternalValue(BaramFlowSolverField field, BaramFlowBoundarySettings settings,
                                               FieldValues values, BaramFlowGeneralSettings general)
        {
            switch (field)
            {
                case BaramFlowSolverField.Pressure: return Format(general.OperatingPressure + settings.InitialGaugePressure);
                case BaramFlowSolverField.Velocity: return Vector(values.Ux, values.Uy, values.Uz);
                case BaramFlowSolverField.Temperature: return Format(settings.InitialTemperature);
                case BaramFlowSolverField.TurbulentViscosity: return Format(values.Nut);
                case BaramFlowSolverField.TurbulentThermalDiffusivity: return Format(values.Alphat);
                case BaramFlowSolverField.TurbulentKineticEnergy: return Format(values.K);
                case BaramFlowSolverField.TurbulentDissipationRate: return Format(values.Epsilon);
                case BaramFlowSolverField.SpecificDissipationRate: return Format(values.Omega);
                case BaramFlowSolverField.ModifiedTurbulentViscosity: return Format(values.NuTilda);
                case BaramFlowSolverField.Intermittency: return "1";
                case BaramFlowSolverField.TransitionOnsetReynoldsNumber: return "200";
                default: throw new ArgumentOutOfRangeException("field");
            }
        }

        private static string GetDimensions(BaramFlowSolverField field)
        {
            switch (field)
            {
                case BaramFlowSolverField.Pressure: return "[1 -1 -2 0 0 0 0]";
                case BaramFlowSolverField.Velocity: return "[0 1 -1 0 0 0 0]";
                case BaramFlowSolverField.Temperature: return "[0 0 0 1 0 0 0]";
                case BaramFlowSolverField.TurbulentViscosity:
                case BaramFlowSolverField.ModifiedTurbulentViscosity: return "[0 2 -1 0 0 0 0]";
                case BaramFlowSolverField.TurbulentThermalDiffusivity: return "[1 -1 -1 0 0 0 0]";
                case BaramFlowSolverField.TurbulentKineticEnergy: return "[0 2 -2 0 0 0 0]";
                case BaramFlowSolverField.TurbulentDissipationRate: return "[0 2 -3 0 0 0 0]";
                case BaramFlowSolverField.SpecificDissipationRate: return "[0 0 -1 0 0 0 0]";
                default: return "[0 0 0 0 0 0 0]";
            }
        }

        private static OpenFoamDictionary Fixed(string value)
        {
            return new OpenFoamDictionary().AddEntry("type", "fixedValue")
                .AddEntry("value", new OpenFoamRawValue("uniform " + value));
        }

        private static OpenFoamDictionary ValueType(string type, double value)
        {
            return new OpenFoamDictionary().AddEntry("type", type)
                .AddEntry("value", new OpenFoamRawValue("uniform " + Format(value)));
        }

        private static OpenFoamDictionary TypeOnly(string type)
        {
            return new OpenFoamDictionary().AddEntry("type", type);
        }

        private static string Vector(double x, double y, double z)
        {
            return "(" + Format(x) + " " + Format(y) + " " + Format(z) + ")";
        }

        private static string Format(double value)
        {
            return value.ToString("G15", CultureInfo.InvariantCulture);
        }

        private static void RemoveInactiveFields(string zeroDirectory, List<string> activeFiles)
        {
            HashSet<string> active = new HashSet<string>(activeFiles, StringComparer.OrdinalIgnoreCase);
            foreach (BaramFlowSolverField field in Enum.GetValues(typeof(BaramFlowSolverField)))
            {
                string fileName = Path.Combine(zeroDirectory, BaramFlowModelsSettings.GetOpenFoamFieldName(field));
                if (!active.Contains(fileName) && File.Exists(fileName)) File.Delete(fileName);
            }
        }

        private class FieldValues
        {
            public double Ux;
            public double Uy;
            public double Uz;
            public double Nut;
            public double Alphat;
            public double K;
            public double Epsilon;
            public double Omega;
            public double NuTilda;
        }
    }
}
