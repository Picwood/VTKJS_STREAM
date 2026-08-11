// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace CaeModel.CFD.BaramFlow
{
    [Serializable]
    public enum BaramFlowSolverType
    {
        PressureBased,
        DensityBased
    }

    [Serializable]
    public enum BaramFlowFlowType
    {
        Incompressible,
        Compressible
    }

    [Serializable]
    public enum BaramFlowTimeMode
    {
        Steady,
        Transient
    }

    [Serializable]
    public class BaramFlowGeneralSettings
    {
        public BaramFlowSolverType SolverType { get; set; }
        public BaramFlowFlowType FlowType { get; set; }
        public BaramFlowTimeMode TimeMode { get; set; }
        public double GravityX { get; set; }
        public double GravityY { get; set; }
        public double GravityZ { get; set; }
        public double OperatingPressure { get; set; }

        public BaramFlowGeneralSettings()
        {
            SolverType = BaramFlowSolverType.PressureBased;
            FlowType = BaramFlowFlowType.Incompressible;
            TimeMode = BaramFlowTimeMode.Steady;
            GravityX = 0;
            GravityY = 0;
            GravityZ = 0;
            OperatingPressure = 101325;
        }

        public BaramFlowGeneralSettings Copy()
        {
            return new BaramFlowGeneralSettings
            {
                SolverType = SolverType,
                FlowType = FlowType,
                TimeMode = TimeMode,
                GravityX = GravityX,
                GravityY = GravityY,
                GravityZ = GravityZ,
                OperatingPressure = OperatingPressure
            };
        }

        public void Normalize()
        {
            if (SolverType == BaramFlowSolverType.DensityBased)
            {
                FlowType = BaramFlowFlowType.Compressible;
                GravityX = 0;
                GravityY = 0;
                GravityZ = 0;
                OperatingPressure = 0;
            }
        }

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(BaramFlowSolverType), SolverType))
                throw new InvalidOperationException("The BaramFlow solver type is not supported.");
            if (!Enum.IsDefined(typeof(BaramFlowFlowType), FlowType))
                throw new InvalidOperationException("The BaramFlow flow type is not supported.");
            if (!Enum.IsDefined(typeof(BaramFlowTimeMode), TimeMode))
                throw new InvalidOperationException("The BaramFlow time mode is not supported.");
            if (SolverType == BaramFlowSolverType.DensityBased &&
                FlowType != BaramFlowFlowType.Compressible)
                throw new InvalidOperationException("The density-based solver requires compressible flow.");
            ValidateGravity(GravityX, "X");
            ValidateGravity(GravityY, "Y");
            ValidateGravity(GravityZ, "Z");
            if (!IsFinite(OperatingPressure) || OperatingPressure < 0 || OperatingPressure > 1E12)
                throw new InvalidOperationException("Operating pressure must be between 0 and 1E12 Pa.");
        }

        private static void ValidateGravity(double value, string component)
        {
            if (!IsFinite(value) || value < -200 || value > 200)
                throw new InvalidOperationException("Gravity " + component + " must be between -200 and 200 m/s^2.");
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
