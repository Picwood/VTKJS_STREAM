// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeModel.CFD.BaramFlow;
using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramFlowRunConditions : Form
    {
        private readonly BaramFlowGeneralSettings _general;
        private readonly NumericUpDown _numberOfIterations;
        private readonly ComboBox _timeSteppingMethod;
        private readonly NumericUpDown _timeStepSize;
        private readonly NumericUpDown _maximumCourantNumber;
        private readonly NumericUpDown _maximumDiffusionNumber;
        private readonly NumericUpDown _endTime;
        private readonly NumericUpDown _reportIntervalSteps;
        private readonly NumericUpDown _reportIntervalSeconds;
        private readonly CheckBox _retainOnlyMostRecentFiles;
        private readonly NumericUpDown _maximumNumberOfDataFiles;
        private readonly ComboBox _dataWriteFormat;
        private readonly NumericUpDown _dataWritePrecision;
        private readonly NumericUpDown _timePrecision;

        public BaramFlowRunSettings Settings { get; private set; }

        public FrmBaramFlowRunConditions(BaramFlowGeneralSettings general, BaramFlowRunSettings settings)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (settings == null) throw new ArgumentNullException("settings");
            _general = general.Copy();
            _general.Normalize();
            Settings = settings.Copy();
            Settings.Normalize(_general);
            Settings.Validate(_general);

            Text = "BaramFlow Run Conditions";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            MinimumSize = new Size(570, 480);
            ClientSize = new Size(650, 560);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 2,
                ColumnCount = 1
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage iterationTab = new TabPage("Iteration");
            TabPage outputTab = new TabPage("Output");
            tabs.TabPages.Add(iterationTab);
            tabs.TabPages.Add(outputTab);
            root.Controls.Add(tabs, 0, 0);

            TableLayoutPanel iteration = CreateTable();
            _numberOfIterations = CreateInteger(1, 1000000000);
            _timeSteppingMethod = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowTimeSteppingMethod)));
            _timeStepSize = CreateNumber(9, 0.000000001M, 1000000000000M);
            _maximumCourantNumber = CreateNumber(6, 0.000001M, 1000000000000M);
            _maximumDiffusionNumber = CreateNumber(6, 0.000001M, 1000000000000M);
            _endTime = CreateNumber(9, 0.000000001M, 1000000000000M);
            _reportIntervalSteps = CreateInteger(1, 1000000000);
            _reportIntervalSeconds = CreateNumber(9, 0.000000001M, 1000000000000M);
            AddRow(iteration, "Number of iterations", _numberOfIterations);
            AddRow(iteration, "Time-stepping method", _timeSteppingMethod);
            AddRow(iteration, "Time-step size", _timeStepSize);
            AddRow(iteration, "Maximum Courant number", _maximumCourantNumber);
            AddRow(iteration, "Maximum diffusion number", _maximumDiffusionNumber);
            AddRow(iteration, "End time", _endTime);
            AddRow(iteration, "Report interval (iterations)", _reportIntervalSteps);
            AddRow(iteration, "Report interval (s)", _reportIntervalSeconds);
            iterationTab.Controls.Add(iteration);

            TableLayoutPanel output = CreateTable();
            _retainOnlyMostRecentFiles = new CheckBox { Text = "Enabled", AutoSize = true };
            _maximumNumberOfDataFiles = CreateInteger(1, 1000000);
            _dataWriteFormat = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowDataWriteFormat)));
            _dataWritePrecision = CreateInteger(1, 32);
            _timePrecision = CreateInteger(1, 32);
            AddRow(output, "Retain only most recent files", _retainOnlyMostRecentFiles);
            AddRow(output, "Maximum number of data files", _maximumNumberOfDataFiles);
            AddRow(output, "Data write format", _dataWriteFormat);
            AddRow(output, "Data write precision", _dataWritePrecision);
            AddRow(output, "Time precision", _timePrecision);
            outputTab.Controls.Add(output);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88 };
            Button ok = new Button { Text = "OK", Width = 88 };
            ok.Click += Ok_Click;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            root.Controls.Add(buttons, 0, 1);
            AcceptButton = ok;
            CancelButton = cancel;

            LoadSettings();
            _timeSteppingMethod.SelectedIndexChanged += delegate { UpdateControlState(); };
            _retainOnlyMostRecentFiles.CheckedChanged += delegate { UpdateControlState(); };
            UpdateControlState();
        }

        private void LoadSettings()
        {
            SetNumber(_numberOfIterations, Settings.NumberOfIterations);
            SelectEnum(_timeSteppingMethod, Settings.TimeSteppingMethod);
            SetNumber(_timeStepSize, Settings.TimeStepSize);
            SetNumber(_maximumCourantNumber, Settings.MaximumCourantNumber);
            SetNumber(_maximumDiffusionNumber, Settings.MaximumDiffusionNumber);
            SetNumber(_endTime, Settings.EndTime);
            SetNumber(_reportIntervalSteps, Settings.ReportIntervalSteps);
            SetNumber(_reportIntervalSeconds, Settings.ReportIntervalSeconds);
            _retainOnlyMostRecentFiles.Checked = Settings.RetainOnlyMostRecentFiles;
            SetNumber(_maximumNumberOfDataFiles, Settings.MaximumNumberOfDataFiles);
            SelectEnum(_dataWriteFormat, Settings.DataWriteFormat);
            SetNumber(_dataWritePrecision, Settings.DataWritePrecision);
            SetNumber(_timePrecision, Settings.TimePrecision);
        }

        private void UpdateControlState()
        {
            bool transient = _general.TimeMode == BaramFlowTimeMode.Transient;
            bool densityBased = _general.SolverType == BaramFlowSolverType.DensityBased;
            bool adaptive = transient && SelectedEnum<BaramFlowTimeSteppingMethod>(_timeSteppingMethod) ==
                                      BaramFlowTimeSteppingMethod.Adaptive;
            _numberOfIterations.Enabled = !transient;
            _timeSteppingMethod.Enabled = transient && !densityBased;
            _timeStepSize.Enabled = transient && !adaptive;
            _maximumCourantNumber.Enabled = transient && (adaptive || densityBased);
            _maximumDiffusionNumber.Enabled = adaptive;
            _endTime.Enabled = transient;
            _reportIntervalSteps.Enabled = !transient;
            _reportIntervalSeconds.Enabled = transient;
            _maximumNumberOfDataFiles.Enabled = _retainOnlyMostRecentFiles.Checked;
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            try
            {
                BaramFlowRunSettings settings = Settings.Copy();
                settings.NumberOfIterations = Decimal.ToInt32(_numberOfIterations.Value);
                settings.TimeSteppingMethod = SelectedEnum<BaramFlowTimeSteppingMethod>(_timeSteppingMethod);
                settings.TimeStepSize = Value(_timeStepSize);
                settings.MaximumCourantNumber = Value(_maximumCourantNumber);
                settings.MaximumDiffusionNumber = Value(_maximumDiffusionNumber);
                settings.EndTime = Value(_endTime);
                settings.ReportIntervalSteps = Decimal.ToInt32(_reportIntervalSteps.Value);
                settings.ReportIntervalSeconds = Value(_reportIntervalSeconds);
                settings.RetainOnlyMostRecentFiles = _retainOnlyMostRecentFiles.Checked;
                settings.MaximumNumberOfDataFiles = Decimal.ToInt32(_maximumNumberOfDataFiles.Value);
                settings.DataWriteFormat = SelectedEnum<BaramFlowDataWriteFormat>(_dataWriteFormat);
                settings.DataWritePrecision = Decimal.ToInt32(_dataWritePrecision.Value);
                settings.TimePrecision = Decimal.ToInt32(_timePrecision.Value);
                settings.Normalize(_general);
                settings.Validate(_general);
                Settings = settings;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static TableLayoutPanel CreateTable()
        {
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left,
                                           Margin = new Padding(3, 8, 3, 3) }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(3, 4, 3, 4);
            table.Controls.Add(control, 1, row);
        }

        private static ComboBox CreateEnumCombo(Array values)
        {
            ComboBox combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            foreach (object value in values) combo.Items.Add(new EnumOption(value, FormatEnum(value.ToString())));
            return combo;
        }

        private static NumericUpDown CreateInteger(decimal minimum, decimal maximum)
        {
            return new NumericUpDown { Minimum = minimum, Maximum = maximum, Width = 160 };
        }

        private static NumericUpDown CreateNumber(int decimals, decimal minimum, decimal maximum)
        {
            return new NumericUpDown
            {
                DecimalPlaces = decimals,
                Minimum = minimum,
                Maximum = maximum,
                Increment = decimals > 6 ? 0.001M : 0.01M,
                Width = 160
            };
        }

        private static void SetNumber(NumericUpDown control, double value)
        {
            decimal number = (decimal)value;
            control.Value = Math.Min(control.Maximum, Math.Max(control.Minimum, number));
        }

        private static double Value(NumericUpDown control)
        {
            return Decimal.ToDouble(control.Value);
        }

        private static void SelectEnum(ComboBox combo, object value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
                if (Equals(((EnumOption)combo.Items[i]).Value, value))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            throw new InvalidOperationException("The selected run option is not available.");
        }

        private static T SelectedEnum<T>(ComboBox combo)
        {
            return (T)((EnumOption)combo.SelectedItem).Value;
        }

        private static string FormatEnum(string value)
        {
            return Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
        }

        private class EnumOption
        {
            public object Value { get; private set; }
            private readonly string _text;
            public EnumOption(object value, string text) { Value = value; _text = text; }
            public override string ToString() { return _text; }
        }
    }
}
