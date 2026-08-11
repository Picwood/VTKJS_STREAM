// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Collections.Generic;

namespace CaeModel.CFD.BaramFlow
{
    [Serializable]
    public enum BaramFlowTurbulenceModel
    {
        Inviscid,
        Laminar,
        SpalartAllmaras,
        KEpsilon,
        KOmegaSst,
        TransitionSst,
        Des,
        Les
    }

    [Serializable]
    public enum BaramFlowKEpsilonModel
    {
        Standard,
        Rng,
        Realizable
    }

    [Serializable]
    public enum BaramFlowNearWallTreatment
    {
        StandardWallFunctions,
        EnhancedWallTreatment
    }

    [Serializable]
    public enum BaramFlowDesRansModel
    {
        SpalartAllmaras,
        KOmegaSst
    }

    [Serializable]
    public enum BaramFlowShieldingFunction
    {
        Ddes,
        Iddes
    }

    [Serializable]
    public enum BaramFlowLengthScaleModel
    {
        CubeRootVolume,
        VanDriest,
        Smooth
    }

    [Serializable]
    public enum BaramFlowLesSubgridScaleModel
    {
        Smagorinsky,
        Wale,
        DynamicKEqn,
        KEqn
    }

    [Serializable]
    public enum BaramFlowSolverField
    {
        Pressure,
        Velocity,
        Temperature,
        TurbulentViscosity,
        TurbulentThermalDiffusivity,
        TurbulentKineticEnergy,
        TurbulentDissipationRate,
        SpecificDissipationRate,
        ModifiedTurbulentViscosity,
        Intermittency,
        TransitionOnsetReynoldsNumber
    }

    [Serializable]
    public class BaramFlowModelsSettings
    {
        public bool EnergyEnabled { get; set; }
        public BaramFlowTurbulenceModel TurbulenceModel { get; set; }
        public BaramFlowKEpsilonModel KEpsilonModel { get; set; }
        public BaramFlowNearWallTreatment NearWallTreatment { get; set; }
        public double EnhancedWallThreshold { get; set; }
        public double EnhancedWallBlendingWidth { get; set; }
        public double EnergyPrandtlNumber { get; set; }
        public double WallPrandtlNumber { get; set; }
        public double TurbulentSchmidtNumber { get; set; }
        public BaramFlowDesRansModel DesRansModel { get; set; }
        public bool DesLowReDamping { get; set; }
        public bool DelayedDes { get; set; }
        public BaramFlowShieldingFunction ShieldingFunction { get; set; }
        public BaramFlowLengthScaleModel DesLengthScale { get; set; }
        public double DesCoefficient { get; set; }
        public double DesKOmegaCoefficient { get; set; }
        public double DesKEpsilonCoefficient { get; set; }
        public BaramFlowLesSubgridScaleModel LesSubgridScaleModel { get; set; }
        public BaramFlowLengthScaleModel LesLengthScale { get; set; }
        public double LesCk { get; set; }
        public double LesCe { get; set; }
        public double LesCw { get; set; }

        public BaramFlowModelsSettings()
        {
            EnergyEnabled = false;
            TurbulenceModel = BaramFlowTurbulenceModel.KEpsilon;
            KEpsilonModel = BaramFlowKEpsilonModel.Standard;
            NearWallTreatment = BaramFlowNearWallTreatment.StandardWallFunctions;
            EnhancedWallThreshold = 60;
            EnhancedWallBlendingWidth = 10;
            EnergyPrandtlNumber = 0.85;
            WallPrandtlNumber = 0.85;
            TurbulentSchmidtNumber = 0.7;
            DesRansModel = BaramFlowDesRansModel.SpalartAllmaras;
            DesLowReDamping = true;
            DelayedDes = false;
            ShieldingFunction = BaramFlowShieldingFunction.Ddes;
            DesLengthScale = BaramFlowLengthScaleModel.CubeRootVolume;
            DesCoefficient = 0.65;
            DesKOmegaCoefficient = 0.82;
            DesKEpsilonCoefficient = 0.6;
            LesSubgridScaleModel = BaramFlowLesSubgridScaleModel.Smagorinsky;
            LesLengthScale = BaramFlowLengthScaleModel.CubeRootVolume;
            LesCk = 0.094;
            LesCe = 1.048;
            LesCw = 0.325;
        }

        public BaramFlowModelsSettings Copy()
        {
            return (BaramFlowModelsSettings)MemberwiseClone();
        }

        public void Normalize(BaramFlowGeneralSettings general)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (general.FlowType == BaramFlowFlowType.Compressible) EnergyEnabled = true;
        }

        public void Validate(BaramFlowGeneralSettings general)
        {
            if (general == null) throw new ArgumentNullException("general");
            ValidateEnum(TurbulenceModel, "turbulence model");
            ValidateEnum(KEpsilonModel, "k-epsilon model");
            ValidateEnum(NearWallTreatment, "near-wall treatment");
            ValidateEnum(DesRansModel, "DES RANS model");
            ValidateEnum(ShieldingFunction, "DES shielding function");
            ValidateEnum(DesLengthScale, "DES length-scale model");
            ValidateEnum(LesSubgridScaleModel, "LES subgrid-scale model");
            ValidateEnum(LesLengthScale, "LES length-scale model");
            if (general.FlowType == BaramFlowFlowType.Compressible && !EnergyEnabled)
                throw new InvalidOperationException("The energy model is required for compressible flow.");
            if (general.TimeMode != BaramFlowTimeMode.Transient &&
                (TurbulenceModel == BaramFlowTurbulenceModel.Des ||
                 TurbulenceModel == BaramFlowTurbulenceModel.Les))
                throw new InvalidOperationException("DES and LES turbulence models require transient flow.");
            ValidatePositive(EnhancedWallThreshold, "Enhanced-wall Reynolds threshold");
            ValidatePositive(EnhancedWallBlendingWidth, "Enhanced-wall blending width");
            ValidatePositive(EnergyPrandtlNumber, "Energy Prandtl number");
            ValidatePositive(WallPrandtlNumber, "Wall Prandtl number");
            ValidatePositive(TurbulentSchmidtNumber, "Turbulent Schmidt number");
            ValidatePositive(DesCoefficient, "DES coefficient");
            ValidatePositive(DesKOmegaCoefficient, "DES k-omega coefficient");
            ValidatePositive(DesKEpsilonCoefficient, "DES k-epsilon coefficient");
            ValidatePositive(LesCk, "LES Ck");
            ValidatePositive(LesCe, "LES Ce");
            ValidatePositive(LesCw, "LES Cw");
        }

        public BaramFlowSolverField[] GetRequiredFields(BaramFlowGeneralSettings general)
        {
            BaramFlowModelsSettings settings = Copy();
            settings.Normalize(general);
            settings.Validate(general);
            List<BaramFlowSolverField> fields = new List<BaramFlowSolverField>
            {
                BaramFlowSolverField.Pressure,
                BaramFlowSolverField.Velocity
            };
            if (settings.EnergyEnabled) fields.Add(BaramFlowSolverField.Temperature);
            bool turbulent = settings.TurbulenceModel != BaramFlowTurbulenceModel.Inviscid &&
                             settings.TurbulenceModel != BaramFlowTurbulenceModel.Laminar;
            if (!turbulent) return fields.ToArray();
            fields.Add(BaramFlowSolverField.TurbulentViscosity);
            if (settings.EnergyEnabled) fields.Add(BaramFlowSolverField.TurbulentThermalDiffusivity);
            //
            if (settings.TurbulenceModel == BaramFlowTurbulenceModel.SpalartAllmaras ||
                (settings.TurbulenceModel == BaramFlowTurbulenceModel.Des &&
                 settings.DesRansModel == BaramFlowDesRansModel.SpalartAllmaras))
            {
                fields.Add(BaramFlowSolverField.ModifiedTurbulentViscosity);
            }
            else if (settings.TurbulenceModel == BaramFlowTurbulenceModel.KEpsilon)
            {
                fields.Add(BaramFlowSolverField.TurbulentKineticEnergy);
                fields.Add(BaramFlowSolverField.TurbulentDissipationRate);
            }
            else if (settings.TurbulenceModel == BaramFlowTurbulenceModel.KOmegaSst ||
                     (settings.TurbulenceModel == BaramFlowTurbulenceModel.Des &&
                      settings.DesRansModel == BaramFlowDesRansModel.KOmegaSst))
            {
                fields.Add(BaramFlowSolverField.TurbulentKineticEnergy);
                fields.Add(BaramFlowSolverField.SpecificDissipationRate);
            }
            else if (settings.TurbulenceModel == BaramFlowTurbulenceModel.TransitionSst)
            {
                fields.Add(BaramFlowSolverField.TurbulentKineticEnergy);
                fields.Add(BaramFlowSolverField.SpecificDissipationRate);
                fields.Add(BaramFlowSolverField.Intermittency);
                fields.Add(BaramFlowSolverField.TransitionOnsetReynoldsNumber);
            }
            else if (settings.TurbulenceModel == BaramFlowTurbulenceModel.Les &&
                     (settings.LesSubgridScaleModel == BaramFlowLesSubgridScaleModel.KEqn ||
                      settings.LesSubgridScaleModel == BaramFlowLesSubgridScaleModel.DynamicKEqn))
            {
                fields.Add(BaramFlowSolverField.TurbulentKineticEnergy);
            }
            return fields.ToArray();
        }

        public static string GetOpenFoamFieldName(BaramFlowSolverField field)
        {
            switch (field)
            {
                case BaramFlowSolverField.Pressure: return "p";
                case BaramFlowSolverField.Velocity: return "U";
                case BaramFlowSolverField.Temperature: return "T";
                case BaramFlowSolverField.TurbulentViscosity: return "nut";
                case BaramFlowSolverField.TurbulentThermalDiffusivity: return "alphat";
                case BaramFlowSolverField.TurbulentKineticEnergy: return "k";
                case BaramFlowSolverField.TurbulentDissipationRate: return "epsilon";
                case BaramFlowSolverField.SpecificDissipationRate: return "omega";
                case BaramFlowSolverField.ModifiedTurbulentViscosity: return "nuTilda";
                case BaramFlowSolverField.Intermittency: return "gammaInt";
                case BaramFlowSolverField.TransitionOnsetReynoldsNumber: return "ReThetat";
                default: throw new ArgumentOutOfRangeException("field");
            }
        }

        private static void ValidateEnum<T>(T value, string name)
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new InvalidOperationException("The BaramFlow " + name + " is not supported.");
        }

        private static void ValidatePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 1E9)
                throw new InvalidOperationException(name + " must be greater than zero and no greater than 1E9.");
        }
    }
}
