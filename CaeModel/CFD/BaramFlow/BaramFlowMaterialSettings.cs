// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace CaeModel.CFD.BaramFlow
{
    [Serializable]
    public enum BaramFlowMaterialPhase
    {
        Gas,
        Liquid
    }

    [Serializable]
    public enum BaramFlowDensityModel
    {
        Constant,
        IdealGas
    }

    [Serializable]
    public class BaramFlowMaterialSettings
    {
        public string Name { get; set; }
        public BaramFlowMaterialPhase Phase { get; set; }
        public BaramFlowDensityModel DensityModel { get; set; }
        public double Density { get; set; }
        public double DynamicViscosity { get; set; }
        public double SpecificHeat { get; set; }
        public double ThermalConductivity { get; set; }
        public double MolecularWeight { get; set; }

        public BaramFlowMaterialSettings()
        {
            SetAirDefaults();
        }

        public BaramFlowMaterialSettings Copy()
        {
            return (BaramFlowMaterialSettings)MemberwiseClone();
        }

        public void SetAirDefaults()
        {
            Name = "air";
            Phase = BaramFlowMaterialPhase.Gas;
            DensityModel = BaramFlowDensityModel.Constant;
            Density = 1.225;
            DynamicViscosity = 1.79E-5;
            SpecificHeat = 1006;
            ThermalConductivity = 0.0245;
            MolecularWeight = 28.966;
        }

        public void SetWaterDefaults()
        {
            Name = "water";
            Phase = BaramFlowMaterialPhase.Liquid;
            DensityModel = BaramFlowDensityModel.Constant;
            Density = 999.1;
            DynamicViscosity = 0.00114;
            SpecificHeat = 4188.5;
            ThermalConductivity = 0.5888;
            MolecularWeight = 18.0153;
        }

        public void Normalize(BaramFlowGeneralSettings general)
        {
            if (general == null) throw new ArgumentNullException("general");
            DensityModel = general.FlowType == BaramFlowFlowType.Compressible &&
                           Phase == BaramFlowMaterialPhase.Gas
                               ? BaramFlowDensityModel.IdealGas
                               : BaramFlowDensityModel.Constant;
        }

        public void Validate(BaramFlowGeneralSettings general)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("The BaramFlow material name is required.");
            if (Name.Length > 128)
                throw new InvalidOperationException("The BaramFlow material name cannot exceed 128 characters.");
            ValidateEnum(Phase, "material phase");
            ValidateEnum(DensityModel, "density model");
            BaramFlowDensityModel expected = general.FlowType == BaramFlowFlowType.Compressible &&
                                             Phase == BaramFlowMaterialPhase.Gas
                                                 ? BaramFlowDensityModel.IdealGas
                                                 : BaramFlowDensityModel.Constant;
            if (DensityModel != expected)
                throw new InvalidOperationException("The selected material density model is incompatible with the flow type and phase.");
            ValidatePositive(Density, "Density");
            ValidatePositive(DynamicViscosity, "Dynamic viscosity");
            ValidatePositive(SpecificHeat, "Specific heat");
            ValidatePositive(ThermalConductivity, "Thermal conductivity");
            ValidatePositive(MolecularWeight, "Molecular weight");
        }

        public double GetPrandtlNumber(BaramFlowModelsSettings models)
        {
            if (models == null) throw new ArgumentNullException("models");
            if (models.TurbulenceModel == BaramFlowTurbulenceModel.Inviscid) return 0.7;
            return SpecificHeat * DynamicViscosity / ThermalConductivity;
        }

        private static void ValidateEnum<T>(T value, string name)
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new InvalidOperationException("The BaramFlow " + name + " is not supported.");
        }

        private static void ValidatePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 1E12)
                throw new InvalidOperationException(name + " must be greater than zero and no greater than 1E12.");
        }
    }
}
