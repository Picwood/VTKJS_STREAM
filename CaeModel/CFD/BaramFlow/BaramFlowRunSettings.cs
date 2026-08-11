// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;

namespace CaeModel.CFD.BaramFlow
{
    [Serializable]
    public enum BaramFlowTimeSteppingMethod
    {
        Fixed,
        Adaptive
    }

    [Serializable]
    public enum BaramFlowDataWriteFormat
    {
        Binary,
        Ascii
    }

    [Serializable]
    public class BaramFlowRunSettings
    {
        public int NumberOfIterations { get; set; }
        public BaramFlowTimeSteppingMethod TimeSteppingMethod { get; set; }
        public double TimeStepSize { get; set; }
        public double MaximumCourantNumber { get; set; }
        public double MaximumDiffusionNumber { get; set; }
        public double EndTime { get; set; }
        public int ReportIntervalSteps { get; set; }
        public double ReportIntervalSeconds { get; set; }
        public bool RetainOnlyMostRecentFiles { get; set; }
        public int MaximumNumberOfDataFiles { get; set; }
        public BaramFlowDataWriteFormat DataWriteFormat { get; set; }
        public int DataWritePrecision { get; set; }
        public int TimePrecision { get; set; }

        public BaramFlowRunSettings()
        {
            NumberOfIterations = 1000;
            TimeSteppingMethod = BaramFlowTimeSteppingMethod.Fixed;
            TimeStepSize = 0.01;
            MaximumCourantNumber = 1;
            MaximumDiffusionNumber = 10;
            EndTime = 10;
            ReportIntervalSteps = 100;
            ReportIntervalSeconds = 0.1;
            RetainOnlyMostRecentFiles = false;
            MaximumNumberOfDataFiles = 1;
            DataWriteFormat = BaramFlowDataWriteFormat.Binary;
            DataWritePrecision = 12;
            TimePrecision = 6;
        }

        public BaramFlowRunSettings Copy()
        {
            return (BaramFlowRunSettings)MemberwiseClone();
        }

        public void Normalize(BaramFlowGeneralSettings general)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (general.SolverType == BaramFlowSolverType.DensityBased)
                TimeSteppingMethod = BaramFlowTimeSteppingMethod.Fixed;
        }

        public void Validate(BaramFlowGeneralSettings general)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (!Enum.IsDefined(typeof(BaramFlowTimeSteppingMethod), TimeSteppingMethod))
                throw new InvalidOperationException("The BaramFlow time-stepping method is not supported.");
            if (!Enum.IsDefined(typeof(BaramFlowDataWriteFormat), DataWriteFormat))
                throw new InvalidOperationException("The BaramFlow data write format is not supported.");
            if (general.SolverType == BaramFlowSolverType.DensityBased &&
                TimeSteppingMethod != BaramFlowTimeSteppingMethod.Fixed)
                throw new InvalidOperationException("The density-based solver requires fixed time stepping.");
            if (NumberOfIterations < 1 || NumberOfIterations > 1000000000)
                throw new InvalidOperationException("Number of iterations must be between 1 and 1000000000.");
            ValidatePositive(TimeStepSize, "Time-step size");
            ValidatePositive(MaximumCourantNumber, "Maximum Courant number");
            ValidatePositive(MaximumDiffusionNumber, "Maximum diffusion number");
            ValidatePositive(EndTime, "End time");
            if (ReportIntervalSteps < 1 || ReportIntervalSteps > 1000000000)
                throw new InvalidOperationException("Report interval must be between 1 and 1000000000 iterations.");
            ValidatePositive(ReportIntervalSeconds, "Report interval");
            if (MaximumNumberOfDataFiles < 1 || MaximumNumberOfDataFiles > 1000000)
                throw new InvalidOperationException("Maximum number of data files must be between 1 and 1000000.");
            if (DataWritePrecision < 1 || DataWritePrecision > 32)
                throw new InvalidOperationException("Data write precision must be between 1 and 32.");
            if (TimePrecision < 1 || TimePrecision > 32)
                throw new InvalidOperationException("Time precision must be between 1 and 32.");
        }

        private static void ValidatePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 1E12)
                throw new InvalidOperationException(name + " must be greater than zero and no greater than 1E12.");
        }
    }
}
