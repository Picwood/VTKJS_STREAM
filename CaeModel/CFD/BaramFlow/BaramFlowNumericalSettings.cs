// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace CaeModel.CFD.BaramFlow
{
    [Serializable]
    public enum BaramFlowPressureVelocityCoupling
    {
        Simple,
        SimpleC
    }

    [Serializable]
    public enum BaramFlowTimeDiscretization
    {
        FirstOrderImplicit,
        SecondOrderImplicit
    }

    [Serializable]
    public enum BaramFlowUpwindDiscretization
    {
        FirstOrderUpwind,
        SecondOrderUpwind
    }

    [Serializable]
    public enum BaramFlowPressureInterpolation
    {
        Linear,
        MomentumWeightedReconstruct,
        MomentumWeighted
    }

    [Serializable]
    public enum BaramFlowDensityFlux
    {
        RoeFds,
        Ausm,
        AusmUp
    }

    [Serializable]
    public class BaramFlowNumericalSettings
    {
        public BaramFlowPressureVelocityCoupling PressureVelocityCoupling { get; set; }
        public bool UseMomentumPredictor { get; set; }
        public BaramFlowDensityFlux DensityFlux { get; set; }
        public double EntropyFixCoefficient { get; set; }
        public double CutOffMachNumber { get; set; }
        public BaramFlowTimeDiscretization TimeDiscretization { get; set; }
        public BaramFlowPressureInterpolation PressureInterpolation { get; set; }
        public BaramFlowUpwindDiscretization MomentumDiscretization { get; set; }
        public BaramFlowUpwindDiscretization EnergyDiscretization { get; set; }
        public BaramFlowUpwindDiscretization TurbulenceDiscretization { get; set; }
        public bool HighOrderTermRelaxationEnabled { get; set; }
        public double HighOrderTermRelaxationFactor { get; set; }
        public int MaximumIterationsPerTimeStep { get; set; }
        public int NumberOfCorrectors { get; set; }
        public int NumberOfNonOrthogonalCorrectors { get; set; }
        public double PressureRelaxation { get; set; }
        public double PressureFinalRelaxation { get; set; }
        public double DensityRelaxation { get; set; }
        public double DensityFinalRelaxation { get; set; }
        public double MomentumRelaxation { get; set; }
        public double MomentumFinalRelaxation { get; set; }
        public double EnergyRelaxation { get; set; }
        public double EnergyFinalRelaxation { get; set; }
        public double TurbulenceRelaxation { get; set; }
        public double TurbulenceFinalRelaxation { get; set; }
        public double PressureAbsoluteTolerance { get; set; }
        public double PressureRelativeTolerance { get; set; }
        public double DensityAbsoluteTolerance { get; set; }
        public double DensityRelativeTolerance { get; set; }
        public double MomentumAbsoluteTolerance { get; set; }
        public double MomentumRelativeTolerance { get; set; }
        public double EnergyAbsoluteTolerance { get; set; }
        public double EnergyRelativeTolerance { get; set; }
        public double TurbulenceAbsoluteTolerance { get; set; }
        public double TurbulenceRelativeTolerance { get; set; }

        public BaramFlowNumericalSettings()
        {
            PressureVelocityCoupling = BaramFlowPressureVelocityCoupling.Simple;
            UseMomentumPredictor = true;
            DensityFlux = BaramFlowDensityFlux.RoeFds;
            EntropyFixCoefficient = 0.5;
            CutOffMachNumber = 0.729;
            TimeDiscretization = BaramFlowTimeDiscretization.FirstOrderImplicit;
            PressureInterpolation = BaramFlowPressureInterpolation.Linear;
            MomentumDiscretization = BaramFlowUpwindDiscretization.SecondOrderUpwind;
            EnergyDiscretization = BaramFlowUpwindDiscretization.SecondOrderUpwind;
            TurbulenceDiscretization = BaramFlowUpwindDiscretization.FirstOrderUpwind;
            HighOrderTermRelaxationEnabled = false;
            HighOrderTermRelaxationFactor = 0.5;
            MaximumIterationsPerTimeStep = 20;
            NumberOfCorrectors = 4;
            NumberOfNonOrthogonalCorrectors = 0;
            PressureRelaxation = 0.3;
            PressureFinalRelaxation = 1;
            DensityRelaxation = 0.9;
            DensityFinalRelaxation = 1;
            MomentumRelaxation = 0.7;
            MomentumFinalRelaxation = 1;
            EnergyRelaxation = 1;
            EnergyFinalRelaxation = 1;
            TurbulenceRelaxation = 0.7;
            TurbulenceFinalRelaxation = 1;
            PressureAbsoluteTolerance = 0.001;
            PressureRelativeTolerance = 0.05;
            DensityAbsoluteTolerance = 0.001;
            DensityRelativeTolerance = 0.05;
            MomentumAbsoluteTolerance = 0.001;
            MomentumRelativeTolerance = 0.05;
            EnergyAbsoluteTolerance = 0.001;
            EnergyRelativeTolerance = 0.05;
            TurbulenceAbsoluteTolerance = 0.001;
            TurbulenceRelativeTolerance = 0.05;
        }

        public BaramFlowNumericalSettings Copy()
        {
            return (BaramFlowNumericalSettings)MemberwiseClone();
        }

        public void Validate(BaramFlowGeneralSettings general, BaramFlowModelsSettings models)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (models == null) throw new ArgumentNullException("models");
            ValidateEnum(PressureVelocityCoupling, "pressure-velocity coupling scheme");
            ValidateEnum(DensityFlux, "density-based flux type");
            ValidateEnum(TimeDiscretization, "time discretization scheme");
            ValidateEnum(PressureInterpolation, "pressure interpolation scheme");
            ValidateEnum(MomentumDiscretization, "momentum discretization scheme");
            ValidateEnum(EnergyDiscretization, "energy discretization scheme");
            ValidateEnum(TurbulenceDiscretization, "turbulence discretization scheme");
            ValidateRange(EntropyFixCoefficient, 0, 1000, "Entropy-fix coefficient");
            ValidateRange(CutOffMachNumber, 0, 1000, "Cut-off Mach number");
            ValidateRange(HighOrderTermRelaxationFactor, 0, 1, "High-order term relaxation factor");
            if (MaximumIterationsPerTimeStep < 1 || MaximumIterationsPerTimeStep > 1000000)
                throw new InvalidOperationException("Maximum iterations per time step must be between 1 and 1000000.");
            if (NumberOfCorrectors < 1 || NumberOfCorrectors > 1000)
                throw new InvalidOperationException("Number of correctors must be between 1 and 1000.");
            if (NumberOfNonOrthogonalCorrectors < 0 || NumberOfNonOrthogonalCorrectors > 1000)
                throw new InvalidOperationException("Number of non-orthogonal correctors must be between 0 and 1000.");
            ValidateUnit(PressureRelaxation, "Pressure relaxation");
            ValidateUnit(PressureFinalRelaxation, "Final pressure relaxation");
            ValidateUnit(DensityRelaxation, "Density relaxation");
            ValidateUnit(DensityFinalRelaxation, "Final density relaxation");
            ValidateUnit(MomentumRelaxation, "Momentum relaxation");
            ValidateUnit(MomentumFinalRelaxation, "Final momentum relaxation");
            ValidateUnit(EnergyRelaxation, "Energy relaxation");
            ValidateUnit(EnergyFinalRelaxation, "Final energy relaxation");
            ValidateUnit(TurbulenceRelaxation, "Turbulence relaxation");
            ValidateUnit(TurbulenceFinalRelaxation, "Final turbulence relaxation");
            ValidateTolerance(PressureAbsoluteTolerance, "Absolute pressure tolerance");
            ValidateTolerance(PressureRelativeTolerance, "Relative pressure tolerance");
            ValidateTolerance(DensityAbsoluteTolerance, "Absolute density tolerance");
            ValidateTolerance(DensityRelativeTolerance, "Relative density tolerance");
            ValidateTolerance(MomentumAbsoluteTolerance, "Absolute momentum tolerance");
            ValidateTolerance(MomentumRelativeTolerance, "Relative momentum tolerance");
            ValidateTolerance(EnergyAbsoluteTolerance, "Absolute energy tolerance");
            ValidateTolerance(EnergyRelativeTolerance, "Relative energy tolerance");
            ValidateTolerance(TurbulenceAbsoluteTolerance, "Absolute turbulence tolerance");
            ValidateTolerance(TurbulenceRelativeTolerance, "Relative turbulence tolerance");
        }

        private static void ValidateEnum<T>(T value, string name)
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new InvalidOperationException("The BaramFlow " + name + " is not supported.");
        }

        private static void ValidateUnit(double value, string name)
        {
            ValidateRange(value, 0, 1, name);
        }

        private static void ValidateTolerance(double value, string name)
        {
            ValidateRange(value, 0, 1E12, name);
        }

        private static void ValidateRange(double value, double minimum, double maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
                throw new InvalidOperationException(name + " must be between " + minimum + " and " + maximum + ".");
        }
    }
}
