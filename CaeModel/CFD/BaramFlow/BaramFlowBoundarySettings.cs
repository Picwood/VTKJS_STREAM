// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Collections.Generic;

namespace CaeModel.CFD.BaramFlow
{
    [Serializable]
    public enum BaramFlowBoundaryType
    {
        VelocityInlet,
        PressureOutlet,
        Wall,
        Symmetry,
        FsiInterface
    }

    [Serializable]
    public class BaramFlowBoundaryCondition
    {
        public string Name { get; set; }
        public string MeshType { get; set; }
        public int FaceCount { get; set; }
        public int StartFace { get; set; }
        public BaramFlowBoundaryType BoundaryType { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double VelocityZ { get; set; }
        public double GaugePressure { get; set; }
        public double Temperature { get; set; }
        public double TurbulenceIntensity { get; set; }
        public double TurbulentViscosityRatio { get; set; }

        public BaramFlowBoundaryCondition()
        {
            Name = "patch";
            MeshType = "patch";
            FaceCount = 0;
            StartFace = 0;
            BoundaryType = BaramFlowBoundaryType.Wall;
            VelocityX = 1;
            VelocityY = 0;
            VelocityZ = 0;
            GaugePressure = 0;
            Temperature = 300;
            TurbulenceIntensity = 0.05;
            TurbulentViscosityRatio = 10;
        }

        public BaramFlowBoundaryCondition Copy()
        {
            return (BaramFlowBoundaryCondition)MemberwiseClone();
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException("A boundary patch name is required.");
            if (string.IsNullOrWhiteSpace(MeshType)) throw new InvalidOperationException("Boundary patch " + Name + " has no mesh type.");
            if (FaceCount < 0 || StartFace < 0) throw new InvalidOperationException("Boundary patch " + Name + " has invalid face addressing.");
            if (!Enum.IsDefined(typeof(BaramFlowBoundaryType), BoundaryType))
                throw new InvalidOperationException("Boundary patch " + Name + " has an unsupported boundary type.");
            ValidateFinite(VelocityX, "Velocity X");
            ValidateFinite(VelocityY, "Velocity Y");
            ValidateFinite(VelocityZ, "Velocity Z");
            ValidateFinite(GaugePressure, "Gauge pressure");
            if (!IsFinite(Temperature) || Temperature <= 0 || Temperature > 100000)
                throw new InvalidOperationException("Boundary temperature must be greater than zero and no greater than 100000 K.");
            if (!IsFinite(TurbulenceIntensity) || TurbulenceIntensity <= 0 || TurbulenceIntensity > 1)
                throw new InvalidOperationException("Turbulence intensity must be greater than zero and no greater than 1.");
            if (!IsFinite(TurbulentViscosityRatio) || TurbulentViscosityRatio <= 0 || TurbulentViscosityRatio > 1E9)
                throw new InvalidOperationException("Turbulent viscosity ratio must be greater than zero and no greater than 1E9.");
        }

        private static void ValidateFinite(double value, string name)
        {
            if (!IsFinite(value) || Math.Abs(value) > 1E12)
                throw new InvalidOperationException(name + " must be finite and between -1E12 and 1E12.");
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    [Serializable]
    public class BaramFlowBoundarySettings
    {
        public double InitialGaugePressure { get; set; }
        public double InitialVelocityX { get; set; }
        public double InitialVelocityY { get; set; }
        public double InitialVelocityZ { get; set; }
        public double InitialTemperature { get; set; }
        public double InitialTurbulenceIntensity { get; set; }
        public double InitialTurbulentViscosityRatio { get; set; }
        public BaramFlowBoundaryCondition[] Boundaries { get; set; }

        public BaramFlowBoundarySettings()
        {
            InitialGaugePressure = 0;
            InitialVelocityX = 0;
            InitialVelocityY = 0;
            InitialVelocityZ = 0;
            InitialTemperature = 300;
            InitialTurbulenceIntensity = 0.05;
            InitialTurbulentViscosityRatio = 10;
            Boundaries = new BaramFlowBoundaryCondition[0];
        }

        public BaramFlowBoundarySettings Copy()
        {
            BaramFlowBoundarySettings copy = (BaramFlowBoundarySettings)MemberwiseClone();
            if (Boundaries == null) copy.Boundaries = new BaramFlowBoundaryCondition[0];
            else
            {
                copy.Boundaries = new BaramFlowBoundaryCondition[Boundaries.Length];
                for (int i = 0; i < Boundaries.Length; i++)
                    copy.Boundaries[i] = Boundaries[i] == null ? null : Boundaries[i].Copy();
            }
            return copy;
        }

        public void Validate()
        {
            ValidateFinite(InitialGaugePressure, "Initial gauge pressure");
            ValidateFinite(InitialVelocityX, "Initial velocity X");
            ValidateFinite(InitialVelocityY, "Initial velocity Y");
            ValidateFinite(InitialVelocityZ, "Initial velocity Z");
            if (!IsFinite(InitialTemperature) || InitialTemperature <= 0 || InitialTemperature > 100000)
                throw new InvalidOperationException("Initial temperature must be greater than zero and no greater than 100000 K.");
            if (!IsFinite(InitialTurbulenceIntensity) || InitialTurbulenceIntensity <= 0 || InitialTurbulenceIntensity > 1)
                throw new InvalidOperationException("Initial turbulence intensity must be greater than zero and no greater than 1.");
            if (!IsFinite(InitialTurbulentViscosityRatio) || InitialTurbulentViscosityRatio <= 0 || InitialTurbulentViscosityRatio > 1E9)
                throw new InvalidOperationException("Initial turbulent viscosity ratio must be greater than zero and no greater than 1E9.");
            if (Boundaries == null || Boundaries.Length == 0)
                throw new InvalidOperationException("The BaramFlow case does not contain boundary patches.");
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BaramFlowBoundaryCondition boundary in Boundaries)
            {
                if (boundary == null) throw new InvalidOperationException("A BaramFlow boundary record is missing.");
                boundary.Validate();
                if (!names.Add(boundary.Name)) throw new InvalidOperationException("Duplicate boundary patch: " + boundary.Name);
            }
        }

        private static void ValidateFinite(double value, string name)
        {
            if (!IsFinite(value) || Math.Abs(value) > 1E12)
                throw new InvalidOperationException(name + " must be finite and between -1E12 and 1E12.");
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
