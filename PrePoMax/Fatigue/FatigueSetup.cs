// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeJob;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PrePoMax.Fatigue
{
    [Serializable]
    public class FatigueSetup
    {
        public FatigueModelSetup Model { get; set; }
        public FatigueLoadsSetup Loads { get; set; }
        public FatigueSnCurveSetup SnCurve { get; set; }

        public FatigueSetup()
        {
            Model = new FatigueModelSetup();
            Loads = new FatigueLoadsSetup();
            SnCurve = FatigueSnCurveSetup.CreateDefault();
        }

        public static FatigueSetup CreateDefault()
        {
            FatigueSetup setup = new FatigueSetup();
            setup.Loads.LoadCases.Add(new FatigueLoadCaseSetup());
            setup.Loads.Components.Add(new FatigueLoadComponentSetup());
            setup.Model.UnitLoadCases.Add(new FatigueUnitLoadCaseSetup());
            return setup;
        }

        public void ApplyProjectContext(string currentResultFileName, AnalysisJob[] jobs,
                                        Func<AnalysisJob, string> getInputFileName,
                                        string[] visiblePartNames = null,
                                        string currentResultName = null)
        {
            if (string.IsNullOrWhiteSpace(currentResultFileName) && string.IsNullOrWhiteSpace(currentResultName)) return;
            //
            string resultName = !string.IsNullOrWhiteSpace(currentResultName)
                                ? currentResultName
                                : Path.GetFileNameWithoutExtension(currentResultFileName);
            AnalysisJob matchingJob = jobs != null
                                      ? jobs.FirstOrDefault(j => string.Equals(j.Name, resultName,
                                                                               StringComparison.OrdinalIgnoreCase))
                                      : null;
            //
            Model.AnalysisName = matchingJob != null ? matchingJob.Name : resultName;
            Model.FrdFileName = currentResultFileName;
            Model.InpFileName = matchingJob != null && getInputFileName != null ? getInputFileName(matchingJob) : Model.InpFileName;
            Model.RequireMatchingAnalysisAndResultNames = matchingJob != null;
            Model.VisiblePartNames.Clear();
            if (visiblePartNames != null) Model.VisiblePartNames.AddRange(visiblePartNames);
            //
            if (Model.UnitLoadCases.Count == 0) Model.UnitLoadCases.Add(new FatigueUnitLoadCaseSetup());
            if (Model.UnitLoadCases.Count == 1)
            {
                Model.UnitLoadCases[0].LoadName = Loads.Components.Count > 0 ? Loads.Components[0].Name : "Load 1";
                Model.UnitLoadCases[0].FrdFileName = currentResultFileName;
                Model.UnitLoadCases[0].ResultStep = Model.ResultStep;
            }
        }

        public string[] Validate()
        {
            List<string> errors = new List<string>();
            //
            if (string.IsNullOrWhiteSpace(Model.AnalysisName)) errors.Add("The fatigue model context is not defined.");
            if (Model.VisiblePartNames == null || Model.VisiblePartNames.Count == 0)
                errors.Add("At least one visible result part is required for the fatigue model.");
            if (string.IsNullOrWhiteSpace(Loads.LoadsFileName) &&
                (Loads.GeneratedLoads == null || Loads.GeneratedLoads.Count == 0))
                errors.Add("Fatigue loads are not defined.");
            if (Loads.LoadCases.Count == 0) errors.Add("At least one fatigue load case is required.");
            if (Loads.Components.Count == 0) errors.Add("At least one fatigue load component is required.");
            if (SnCurve.Ds1 <= 0 || SnCurve.N1 <= 0 || SnCurve.N2 <= 0 || SnCurve.M1 <= 0 || SnCurve.M2 <= 0)
                errors.Add("SN curve stress ranges, cycles and slopes must be positive.");
            //
            return errors.ToArray();
        }

        public void WriteHandoff(string fileName)
        {
            if (Loads.GeneratedLoads == null) Loads.GeneratedLoads = new List<FatigueGeneratedLoadSetup>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            WriteObjectStart(sb, "model", 1);
            WriteValue(sb, "analysisName", Model.AnalysisName, 2, true);
            WriteValue(sb, "inpFileName", Model.InpFileName, 2, true);
            WriteValue(sb, "frdFileName", Model.FrdFileName, 2, true);
            WriteValue(sb, "resultStep", Model.ResultStep, 2, true);
            WriteValue(sb, "materials", Model.Materials, 2, true);
            WriteValue(sb, "edgeDetectionAngle", Model.EdgeDetectionAngle, 2, true);
            WriteValue(sb, "keepMaterialInterfaces", Model.KeepMaterialInterfaces, 2, true);
            WriteValue(sb, "requireMatchingAnalysisAndResultNames", Model.RequireMatchingAnalysisAndResultNames, 2, true);
            WriteStringArray(sb, "visiblePartNames", Model.VisiblePartNames, 2, true);
            WriteArrayStart(sb, "unitLoadCases", 2);
            for (int i = 0; i < Model.UnitLoadCases.Count; i++)
            {
                FatigueUnitLoadCaseSetup ulc = Model.UnitLoadCases[i];
                Indent(sb, 3); sb.AppendLine("{");
                WriteValue(sb, "loadName", ulc.LoadName, 4, true);
                WriteValue(sb, "loadType", ulc.LoadType, 4, true);
                WriteValue(sb, "stressFileKind", ulc.StressFileKind, 4, true);
                WriteValue(sb, "loadValue", ulc.LoadValue, 4, true);
                WriteValue(sb, "variableValue", ulc.VariableValue, 4, true);
                WriteValue(sb, "frdFileName", ulc.FrdFileName, 4, true);
                WriteValue(sb, "resultStep", ulc.ResultStep, 4, false);
                Indent(sb, 3); sb.Append(i == Model.UnitLoadCases.Count - 1 ? "}" : "},"); sb.AppendLine();
            }
            WriteArrayEnd(sb, 2, false);
            WriteObjectEnd(sb, 1, true);
            //
            WriteObjectStart(sb, "loads", 1);
            WriteValue(sb, "format", Loads.Format, 2, true);
            WriteValue(sb, "loadsFileName", Loads.LoadsFileName, 2, true);
            WriteValue(sb, "reduction", Loads.Reduction, 2, true);
            WriteArrayStart(sb, "loadCases", 2);
            for (int i = 0; i < Loads.LoadCases.Count; i++)
            {
                FatigueLoadCaseSetup lc = Loads.LoadCases[i];
                Indent(sb, 3); sb.AppendLine("{");
                WriteValue(sb, "name", lc.Name, 4, true);
                WriteValue(sb, "repetitions", lc.Repetitions, 4, true);
                WriteValue(sb, "resultStep", lc.ResultStep, 4, false);
                Indent(sb, 3); sb.Append(i == Loads.LoadCases.Count - 1 ? "}" : "},"); sb.AppendLine();
            }
            WriteArrayEnd(sb, 2, true);
            WriteArrayStart(sb, "components", 2);
            for (int i = 0; i < Loads.Components.Count; i++)
            {
                FatigueLoadComponentSetup comp = Loads.Components[i];
                Indent(sb, 3); sb.AppendLine("{");
                WriteValue(sb, "name", comp.Name, 4, true);
                WriteValue(sb, "unit", comp.Unit, 4, true);
                WriteValue(sb, "scale", comp.Scale, 4, true);
                WriteValue(sb, "offset", comp.Offset, 4, true);
                WriteValue(sb, "plot", comp.Plot, 4, false);
                Indent(sb, 3); sb.Append(i == Loads.Components.Count - 1 ? "}" : "},"); sb.AppendLine();
            }
            WriteArrayEnd(sb, 2, true);
            WriteArrayStart(sb, "generatedLoads", 2);
            for (int i = 0; i < Loads.GeneratedLoads.Count; i++)
            {
                FatigueGeneratedLoadSetup load = Loads.GeneratedLoads[i];
                Indent(sb, 3); sb.AppendLine("{");
                WriteValue(sb, "name", load.Name, 4, true);
                WriteValue(sb, "unit", load.Unit, 4, true);
                WriteValue(sb, "waveform", load.Waveform, 4, true);
                WriteValue(sb, "mean", load.Mean, 4, true);
                WriteValue(sb, "amplitude", load.Amplitude, 4, true);
                WriteValue(sb, "frequency", load.Frequency, 4, true);
                WriteValue(sb, "phase", load.Phase, 4, true);
                WriteValue(sb, "cycles", load.Cycles, 4, true);
                WriteValue(sb, "samplesPerSecond", load.SamplesPerSecond, 4, true);
                WriteValue(sb, "repetitions", load.Repetitions, 4, true);
                WriteValue(sb, "resultStep", load.ResultStep, 4, false);
                Indent(sb, 3); sb.Append(i == Loads.GeneratedLoads.Count - 1 ? "}" : "},"); sb.AppendLine();
            }
            WriteArrayEnd(sb, 2, false);
            WriteObjectEnd(sb, 1, true);
            //
            WriteObjectStart(sb, "snCurve", 1);
            WriteValue(sb, "name", SnCurve.Name, 2, true);
            WriteValue(sb, "comments", SnCurve.Comments, 2, true);
            WriteValue(sb, "ds1", SnCurve.Ds1, 2, true);
            WriteValue(sb, "n1", SnCurve.N1, 2, true);
            WriteValue(sb, "n2", SnCurve.N2, 2, true);
            WriteValue(sb, "m1", SnCurve.M1, 2, true);
            WriteValue(sb, "m2", SnCurve.M2, 2, true);
            WriteValue(sb, "gf", SnCurve.Gf, 2, true);
            WriteValue(sb, "allowableDamage", SnCurve.AllowableDamage, 2, true);
            WriteValue(sb, "minS", SnCurve.MinS, 2, true);
            WriteValue(sb, "maxS", SnCurve.MaxS, 2, true);
            WriteValue(sb, "meanMode", SnCurve.MeanMode, 2, true);
            WriteValue(sb, "m", SnCurve.M, 2, true);
            WriteValue(sb, "re", SnCurve.Re, 2, true);
            WriteValue(sb, "rm", SnCurve.Rm, 2, true);
            WriteValue(sb, "observeYieldLines", SnCurve.ObserveYieldLines, 2, true);
            WriteValue(sb, "extrapolateInCompression", SnCurve.ExtrapolateInCompression, 2, true);
            WriteValue(sb, "dal", SnCurve.Dal, 2, true);
            WriteValue(sb, "ps", SnCurve.Ps, 2, true);
            WriteValue(sb, "std", SnCurve.Std, 2, true);
            WriteValue(sb, "calculationBackend", SnCurve.CalculationBackend, 2, true);
            WriteValue(sb, "fatigueStress", SnCurve.FatigueStress, 2, true);
            WriteValue(sb, "cycleCounter", SnCurve.CycleCounter, 2, true);
            WriteValue(sb, "multiaxialCriterion", SnCurve.MultiaxialCriterion, 2, true);
            WriteValue(sb, "normalSearchPlanes", SnCurve.NormalSearchPlanes, 2, true);
            WriteValue(sb, "inclinedSearchPlanes", SnCurve.InclinedSearchPlanes, 2, true);
            WriteValue(sb, "multiaxialK1", SnCurve.MultiaxialK1, 2, true);
            WriteValue(sb, "multiaxialK2", SnCurve.MultiaxialK2, 2, false);
            WriteObjectEnd(sb, 1, false);
            sb.AppendLine("}");
            //
            File.WriteAllText(fileName, sb.ToString());
        }

        private static void WriteObjectStart(StringBuilder sb, string name, int indent)
        {
            Indent(sb, indent); sb.Append('"').Append(JsonEscape(name)).AppendLine("\": {");
        }
        private static void WriteObjectEnd(StringBuilder sb, int indent, bool comma)
        {
            Indent(sb, indent); sb.Append(comma ? "}," : "}"); sb.AppendLine();
        }
        private static void WriteArrayStart(StringBuilder sb, string name, int indent)
        {
            Indent(sb, indent); sb.Append('"').Append(JsonEscape(name)).AppendLine("\": [");
        }
        private static void WriteArrayEnd(StringBuilder sb, int indent, bool comma)
        {
            Indent(sb, indent); sb.Append(comma ? "]," : "]"); sb.AppendLine();
        }
        private static void WriteValue(StringBuilder sb, string name, string value, int indent, bool comma)
        {
            Indent(sb, indent);
            sb.Append('"').Append(JsonEscape(name)).Append("\": \"").Append(JsonEscape(value ?? "")).Append('"');
            if (comma) sb.Append(',');
            sb.AppendLine();
        }
        private static void WriteValue(StringBuilder sb, string name, double value, int indent, bool comma)
        {
            Indent(sb, indent);
            sb.Append('"').Append(JsonEscape(name)).Append("\": ").Append(value.ToString("R", CultureInfo.InvariantCulture));
            if (comma) sb.Append(',');
            sb.AppendLine();
        }
        private static void WriteValue(StringBuilder sb, string name, int value, int indent, bool comma)
        {
            Indent(sb, indent);
            sb.Append('"').Append(JsonEscape(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma) sb.Append(',');
            sb.AppendLine();
        }
        private static void WriteValue(StringBuilder sb, string name, bool value, int indent, bool comma)
        {
            Indent(sb, indent);
            sb.Append('"').Append(JsonEscape(name)).Append("\": ").Append(value ? "true" : "false");
            if (comma) sb.Append(',');
            sb.AppendLine();
        }
        private static void WriteStringArray(StringBuilder sb, string name, IEnumerable<string> values, int indent, bool comma)
        {
            Indent(sb, indent);
            sb.Append('"').Append(JsonEscape(name)).Append("\": [");
            if (values != null)
            {
                bool first = true;
                foreach (string value in values)
                {
                    if (!first) sb.Append(", ");
                    sb.Append('"').Append(JsonEscape(value ?? "")).Append('"');
                    first = false;
                }
            }
            sb.Append(']');
            if (comma) sb.Append(',');
            sb.AppendLine();
        }
        private static void Indent(StringBuilder sb, int indent)
        {
            sb.Append(' ', indent * 2);
        }
        private static string JsonEscape(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }

    [Serializable]
    public class FatigueModelSetup
    {
        public string AnalysisName { get; set; }
        public string InpFileName { get; set; }
        public string FrdFileName { get; set; }
        public int ResultStep { get; set; }
        public string Materials { get; set; }
        public double EdgeDetectionAngle { get; set; }
        public bool KeepMaterialInterfaces { get; set; }
        public bool RequireMatchingAnalysisAndResultNames { get; set; }
        public List<string> VisiblePartNames { get; set; }
        public List<FatigueUnitLoadCaseSetup> UnitLoadCases { get; set; }

        public FatigueModelSetup()
        {
            ResultStep = 1;
            Materials = "";
            EdgeDetectionAngle = 40;
            RequireMatchingAnalysisAndResultNames = true;
            VisiblePartNames = new List<string>();
            UnitLoadCases = new List<FatigueUnitLoadCaseSetup>();
        }
    }

    [Serializable]
    public class FatigueUnitLoadCaseSetup
    {
        public string LoadName { get; set; }
        public string LoadType { get; set; }
        public string StressFileKind { get; set; }
        public double LoadValue { get; set; }
        public double VariableValue { get; set; }
        public string FrdFileName { get; set; }
        public int ResultStep { get; set; }

        public FatigueUnitLoadCaseSetup()
        {
            LoadName = "Load 1";
            LoadType = "Linear";
            StressFileKind = "standard";
            LoadValue = 1;
            VariableValue = 0;
            ResultStep = 1;
        }
    }

    [Serializable]
    public class FatigueLoadsSetup
    {
        public string Format { get; set; }
        public string LoadsFileName { get; set; }
        public string Reduction { get; set; }
        public List<FatigueLoadCaseSetup> LoadCases { get; set; }
        public List<FatigueLoadComponentSetup> Components { get; set; }
        public List<FatigueGeneratedLoadSetup> GeneratedLoads { get; set; }

        public FatigueLoadsSetup()
        {
            Format = "XLS";
            Reduction = "Full time series";
            LoadCases = new List<FatigueLoadCaseSetup>();
            Components = new List<FatigueLoadComponentSetup>();
            GeneratedLoads = new List<FatigueGeneratedLoadSetup>();
        }
    }

    [Serializable]
    public class FatigueLoadCaseSetup
    {
        public string Name { get; set; }
        public double Repetitions { get; set; }
        public int ResultStep { get; set; }

        public FatigueLoadCaseSetup()
        {
            Name = "Load case 1";
            Repetitions = 1;
            ResultStep = 1;
        }
    }

    [Serializable]
    public class FatigueGeneratedLoadSetup
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Waveform { get; set; }
        public double Mean { get; set; }
        public double Amplitude { get; set; }
        public double Frequency { get; set; }
        public double Phase { get; set; }
        public double Cycles { get; set; }
        public double SamplesPerSecond { get; set; }
        public double Repetitions { get; set; }
        public int ResultStep { get; set; }

        public FatigueGeneratedLoadSetup()
        {
            Name = "Fx";
            Unit = "kN";
            Waveform = "Sinusoidal";
            Mean = 0;
            Amplitude = 1;
            Frequency = 1;
            Phase = 0;
            Cycles = 10;
            SamplesPerSecond = 1000;
            Repetitions = 1;
            ResultStep = 1;
        }
    }

    [Serializable]
    public class FatigueLoadComponentSetup
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public double Scale { get; set; }
        public double Offset { get; set; }
        public bool Plot { get; set; }

        public FatigueLoadComponentSetup()
        {
            Name = "Load 1";
            Unit = "";
            Scale = 1;
            Offset = 0;
            Plot = true;
        }
    }

    [Serializable]
    public class FatigueSnCurveSetup
    {
        public string Name { get; set; }
        public string Comments { get; set; }
        public double Ds1 { get; set; }
        public double N1 { get; set; }
        public double N2 { get; set; }
        public double M1 { get; set; }
        public double M2 { get; set; }
        public double Gf { get; set; }
        public double AllowableDamage { get; set; }
        public double MinS { get; set; }
        public double MaxS { get; set; }
        public string MeanMode { get; set; }
        public double M { get; set; }
        public double Re { get; set; }
        public double Rm { get; set; }
        public bool ObserveYieldLines { get; set; }
        public bool ExtrapolateInCompression { get; set; }
        public double Dal { get; set; }
        public double Ps { get; set; }
        public double Std { get; set; }
        public string CalculationBackend { get; set; }
        public string FatigueStress { get; set; }
        public string CycleCounter { get; set; }
        public string MultiaxialCriterion { get; set; }
        public int NormalSearchPlanes { get; set; }
        public int InclinedSearchPlanes { get; set; }
        public double MultiaxialK1 { get; set; }
        public double MultiaxialK2 { get; set; }

        public static FatigueSnCurveSetup CreateDefault()
        {
            FatigueSnCurveSetup sn = new FatigueSnCurveSetup();
            sn.Name = "Default";
            sn.Comments = "";
            sn.Ds1 = 100;
            sn.N1 = 2e6;
            sn.N2 = 1e7;
            sn.M1 = 3;
            sn.M2 = 5;
            sn.Gf = 1.35;
            sn.AllowableDamage = 1;
            sn.MinS = 0;
            sn.MaxS = 10000;
            sn.MeanMode = "None";
            sn.M = 0.20;
            sn.Re = 235;
            sn.Rm = 370;
            sn.ObserveYieldLines = false;
            sn.ExtrapolateInCompression = false;
            sn.Dal = 1.0;
            sn.Ps = 97.7;
            sn.Std = 0.2;
            sn.CalculationBackend = "MATLAB";
            sn.FatigueStress = "Pnmax";
            sn.CycleCounter = "Reservoir";
            sn.MultiaxialCriterion = "Normal stress";
            sn.NormalSearchPlanes = 36;
            sn.InclinedSearchPlanes = 1;
            sn.MultiaxialK1 = 0.3;
            sn.MultiaxialK2 = 1;
            return sn;
        }
    }
}
