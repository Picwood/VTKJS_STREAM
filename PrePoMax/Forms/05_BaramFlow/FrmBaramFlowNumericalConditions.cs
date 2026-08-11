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
    internal class FrmBaramFlowNumericalConditions : Form
    {
        private readonly BaramFlowGeneralSettings _general;
        private readonly BaramFlowModelsSettings _models;
        private readonly ComboBox _coupling;
        private readonly CheckBox _momentumPredictor;
        private readonly ComboBox _densityFlux;
        private readonly NumericUpDown _entropyFix;
        private readonly NumericUpDown _cutOffMach;
        private readonly NumericUpDown _maxIterations;
        private readonly NumericUpDown _correctors;
        private readonly NumericUpDown _nonOrthogonalCorrectors;
        private readonly ComboBox _timeScheme;
        private readonly ComboBox _pressureScheme;
        private readonly ComboBox _momentumScheme;
        private readonly ComboBox _energyScheme;
        private readonly ComboBox _turbulenceScheme;
        private readonly CheckBox _highOrderRelaxation;
        private readonly NumericUpDown _highOrderFactor;
        private readonly NumericUpDown[,] _relaxation;
        private readonly NumericUpDown[,] _convergence;

        public BaramFlowNumericalSettings Settings { get; private set; }

        public FrmBaramFlowNumericalConditions(BaramFlowGeneralSettings general, BaramFlowModelsSettings models,
                                                BaramFlowNumericalSettings settings)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (models == null) throw new ArgumentNullException("models");
            if (settings == null) throw new ArgumentNullException("settings");
            _general = general.Copy();
            _models = models.Copy();
            _models.Normalize(_general);
            Settings = settings.Copy();
            Settings.Validate(_general, _models);
            Text = "BaramFlow Numerical Conditions";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            MinimumSize = new Size(650, 520);
            ClientSize = new Size(760, 610);

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
            TabPage algorithmsTab = new TabPage("Algorithms");
            TabPage discretizationTab = new TabPage("Discretization");
            TabPage relaxationTab = new TabPage("Relaxation");
            TabPage convergenceTab = new TabPage("Convergence");
            tabs.TabPages.AddRange(new TabPage[] { algorithmsTab, discretizationTab, relaxationTab, convergenceTab });
            root.Controls.Add(tabs, 0, 0);

            TableLayoutPanel algorithms = CreateTable(220);
            algorithms.Dock = DockStyle.Top;
            algorithms.Padding = new Padding(10);
            _coupling = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowPressureVelocityCoupling)));
            _momentumPredictor = new CheckBox { Text = "Enabled", AutoSize = true };
            _densityFlux = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowDensityFlux)));
            _entropyFix = CreateNumber(6, 0, 1000);
            _cutOffMach = CreateNumber(6, 0, 1000);
            _maxIterations = CreateInteger(1, 1000000);
            _correctors = CreateInteger(1, 1000);
            _nonOrthogonalCorrectors = CreateInteger(0, 1000);
            AddRow(algorithms, "Pressure-velocity coupling", _coupling);
            AddRow(algorithms, "Momentum predictor", _momentumPredictor);
            AddRow(algorithms, "Density flux", _densityFlux);
            AddRow(algorithms, "Entropy-fix coefficient", _entropyFix);
            AddRow(algorithms, "Cut-off Mach number", _cutOffMach);
            AddRow(algorithms, "Max iterations per time step", _maxIterations);
            AddRow(algorithms, "Pressure correctors", _correctors);
            AddRow(algorithms, "Non-orthogonal correctors", _nonOrthogonalCorrectors);
            algorithmsTab.Controls.Add(algorithms);

            TableLayoutPanel discretization = CreateTable(220);
            discretization.Dock = DockStyle.Top;
            discretization.Padding = new Padding(10);
            _timeScheme = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowTimeDiscretization)));
            _pressureScheme = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowPressureInterpolation)));
            _momentumScheme = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowUpwindDiscretization)));
            _energyScheme = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowUpwindDiscretization)));
            _turbulenceScheme = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowUpwindDiscretization)));
            _highOrderRelaxation = new CheckBox { Text = "Enabled", AutoSize = true };
            _highOrderFactor = CreateNumber(6, 0, 1);
            AddRow(discretization, "Time", _timeScheme);
            AddRow(discretization, "Pressure interpolation", _pressureScheme);
            AddRow(discretization, "Momentum / flow", _momentumScheme);
            AddRow(discretization, "Energy", _energyScheme);
            AddRow(discretization, "Turbulence", _turbulenceScheme);
            AddRow(discretization, "High-order term relaxation", _highOrderRelaxation);
            AddRow(discretization, "Limiting factor", _highOrderFactor);
            discretizationTab.Controls.Add(discretization);

            string[] quantities = { "Pressure", "Density", "Momentum", "Energy", "Turbulence" };
            _relaxation = new NumericUpDown[quantities.Length, 2];
            relaxationTab.Controls.Add(CreateValueGrid(quantities, new string[] { "Current", "Final" },
                                                       _relaxation, 6, 0, 1));
            _convergence = new NumericUpDown[quantities.Length, 2];
            convergenceTab.Controls.Add(CreateValueGrid(quantities, new string[] { "Absolute", "Relative" },
                                                        _convergence, 12, 0, 1000000000000M));

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
            _densityFlux.SelectedIndexChanged += delegate { UpdateControlState(); };
            _highOrderRelaxation.CheckedChanged += delegate { UpdateControlState(); };
            UpdateControlState();
        }

        private void LoadSettings()
        {
            SelectEnum(_coupling, Settings.PressureVelocityCoupling);
            _momentumPredictor.Checked = Settings.UseMomentumPredictor;
            SelectEnum(_densityFlux, Settings.DensityFlux);
            SetNumber(_entropyFix, Settings.EntropyFixCoefficient);
            SetNumber(_cutOffMach, Settings.CutOffMachNumber);
            SetNumber(_maxIterations, Settings.MaximumIterationsPerTimeStep);
            SetNumber(_correctors, Settings.NumberOfCorrectors);
            SetNumber(_nonOrthogonalCorrectors, Settings.NumberOfNonOrthogonalCorrectors);
            SelectEnum(_timeScheme, Settings.TimeDiscretization);
            SelectEnum(_pressureScheme, Settings.PressureInterpolation);
            SelectEnum(_momentumScheme, Settings.MomentumDiscretization);
            SelectEnum(_energyScheme, Settings.EnergyDiscretization);
            SelectEnum(_turbulenceScheme, Settings.TurbulenceDiscretization);
            _highOrderRelaxation.Checked = Settings.HighOrderTermRelaxationEnabled;
            SetNumber(_highOrderFactor, Settings.HighOrderTermRelaxationFactor);
            double[,] relaxation =
            {
                { Settings.PressureRelaxation, Settings.PressureFinalRelaxation },
                { Settings.DensityRelaxation, Settings.DensityFinalRelaxation },
                { Settings.MomentumRelaxation, Settings.MomentumFinalRelaxation },
                { Settings.EnergyRelaxation, Settings.EnergyFinalRelaxation },
                { Settings.TurbulenceRelaxation, Settings.TurbulenceFinalRelaxation }
            };
            double[,] convergence =
            {
                { Settings.PressureAbsoluteTolerance, Settings.PressureRelativeTolerance },
                { Settings.DensityAbsoluteTolerance, Settings.DensityRelativeTolerance },
                { Settings.MomentumAbsoluteTolerance, Settings.MomentumRelativeTolerance },
                { Settings.EnergyAbsoluteTolerance, Settings.EnergyRelativeTolerance },
                { Settings.TurbulenceAbsoluteTolerance, Settings.TurbulenceRelativeTolerance }
            };
            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 2; column++)
                {
                    SetNumber(_relaxation[row, column], relaxation[row, column]);
                    SetNumber(_convergence[row, column], convergence[row, column]);
                }
        }

        private void UpdateControlState()
        {
            bool densityBased = _general.SolverType == BaramFlowSolverType.DensityBased;
            bool transient = _general.TimeMode == BaramFlowTimeMode.Transient;
            bool energy = _models.EnergyEnabled;
            bool turbulence = _models.TurbulenceModel != BaramFlowTurbulenceModel.Inviscid &&
                              _models.TurbulenceModel != BaramFlowTurbulenceModel.Laminar;
            _coupling.Enabled = !densityBased;
            _momentumPredictor.Enabled = !densityBased && transient;
            _densityFlux.Enabled = densityBased;
            _entropyFix.Enabled = densityBased && SelectedEnum<BaramFlowDensityFlux>(_densityFlux) == BaramFlowDensityFlux.RoeFds;
            _cutOffMach.Enabled = densityBased && SelectedEnum<BaramFlowDensityFlux>(_densityFlux) == BaramFlowDensityFlux.AusmUp;
            _maxIterations.Enabled = transient || densityBased;
            _correctors.Enabled = !densityBased && transient;
            _nonOrthogonalCorrectors.Enabled = !densityBased;
            _timeScheme.Enabled = !densityBased && transient;
            _pressureScheme.Enabled = !densityBased;
            _momentumScheme.Enabled = true;
            _energyScheme.Enabled = !densityBased && energy;
            _turbulenceScheme.Enabled = turbulence;
            _highOrderFactor.Enabled = _highOrderRelaxation.Checked;
            for (int column = 0; column < 2; column++)
            {
                _relaxation[0, column].Enabled = !densityBased && (column == 0 || transient);
                _relaxation[1, column].Enabled = !densityBased && _general.FlowType == BaramFlowFlowType.Compressible &&
                                                  (column == 0 || transient);
                _relaxation[2, column].Enabled = !densityBased && (column == 0 || transient);
                _relaxation[3, column].Enabled = !densityBased && energy && (column == 0 || transient);
                _relaxation[4, column].Enabled = !densityBased && turbulence && (column == 0 || transient);
                _convergence[0, column].Enabled = !densityBased && (column == 0 || transient);
                _convergence[1, column].Enabled = densityBased && (column == 0 || transient);
                _convergence[2, column].Enabled = column == 0 || transient;
                _convergence[3, column].Enabled = energy && (column == 0 || transient);
                _convergence[4, column].Enabled = turbulence && (column == 0 || transient);
            }
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            try
            {
                BaramFlowNumericalSettings settings = Settings.Copy();
                settings.PressureVelocityCoupling = SelectedEnum<BaramFlowPressureVelocityCoupling>(_coupling);
                settings.UseMomentumPredictor = _momentumPredictor.Checked;
                settings.DensityFlux = SelectedEnum<BaramFlowDensityFlux>(_densityFlux);
                settings.EntropyFixCoefficient = Value(_entropyFix);
                settings.CutOffMachNumber = Value(_cutOffMach);
                settings.MaximumIterationsPerTimeStep = (int)_maxIterations.Value;
                settings.NumberOfCorrectors = (int)_correctors.Value;
                settings.NumberOfNonOrthogonalCorrectors = (int)_nonOrthogonalCorrectors.Value;
                settings.TimeDiscretization = SelectedEnum<BaramFlowTimeDiscretization>(_timeScheme);
                settings.PressureInterpolation = SelectedEnum<BaramFlowPressureInterpolation>(_pressureScheme);
                settings.MomentumDiscretization = SelectedEnum<BaramFlowUpwindDiscretization>(_momentumScheme);
                settings.EnergyDiscretization = SelectedEnum<BaramFlowUpwindDiscretization>(_energyScheme);
                settings.TurbulenceDiscretization = SelectedEnum<BaramFlowUpwindDiscretization>(_turbulenceScheme);
                settings.HighOrderTermRelaxationEnabled = _highOrderRelaxation.Checked;
                settings.HighOrderTermRelaxationFactor = Value(_highOrderFactor);
                settings.PressureRelaxation = Value(_relaxation[0, 0]);
                settings.PressureFinalRelaxation = Value(_relaxation[0, 1]);
                settings.DensityRelaxation = Value(_relaxation[1, 0]);
                settings.DensityFinalRelaxation = Value(_relaxation[1, 1]);
                settings.MomentumRelaxation = Value(_relaxation[2, 0]);
                settings.MomentumFinalRelaxation = Value(_relaxation[2, 1]);
                settings.EnergyRelaxation = Value(_relaxation[3, 0]);
                settings.EnergyFinalRelaxation = Value(_relaxation[3, 1]);
                settings.TurbulenceRelaxation = Value(_relaxation[4, 0]);
                settings.TurbulenceFinalRelaxation = Value(_relaxation[4, 1]);
                settings.PressureAbsoluteTolerance = Value(_convergence[0, 0]);
                settings.PressureRelativeTolerance = Value(_convergence[0, 1]);
                settings.DensityAbsoluteTolerance = Value(_convergence[1, 0]);
                settings.DensityRelativeTolerance = Value(_convergence[1, 1]);
                settings.MomentumAbsoluteTolerance = Value(_convergence[2, 0]);
                settings.MomentumRelativeTolerance = Value(_convergence[2, 1]);
                settings.EnergyAbsoluteTolerance = Value(_convergence[3, 0]);
                settings.EnergyRelativeTolerance = Value(_convergence[3, 1]);
                settings.TurbulenceAbsoluteTolerance = Value(_convergence[4, 0]);
                settings.TurbulenceRelativeTolerance = Value(_convergence[4, 1]);
                settings.Validate(_general, _models);
                Settings = settings;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static TableLayoutPanel CreateValueGrid(string[] rows, string[] columns, NumericUpDown[,] controls,
                                                        int decimals, decimal minimum, decimal maximum)
        {
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                AutoSize = true,
                ColumnCount = 3,
                RowCount = rows.Length + 1
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.Controls.Add(new Label { Text = "Quantity", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            for (int column = 0; column < columns.Length; column++)
                table.Controls.Add(new Label { Text = columns[column], AutoSize = true, Anchor = AnchorStyles.Left }, column + 1, 0);
            for (int row = 0; row < rows.Length; row++)
            {
                table.Controls.Add(new Label { Text = rows[row], AutoSize = true, Anchor = AnchorStyles.Left }, 0, row + 1);
                for (int column = 0; column < columns.Length; column++)
                {
                    controls[row, column] = CreateNumber(decimals, minimum, maximum);
                    controls[row, column].Dock = DockStyle.Fill;
                    table.Controls.Add(controls[row, column], column + 1, row + 1);
                }
            }
            return table;
        }

        private static TableLayoutPanel CreateTable(int labelWidth)
        {
            TableLayoutPanel table = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
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
            ComboBox combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
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
                Increment = decimals > 0 ? 0.01M : 1,
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
            throw new InvalidOperationException("The selected numerical option is not available.");
        }

        private static T SelectedEnum<T>(ComboBox combo)
        {
            return (T)((EnumOption)combo.SelectedItem).Value;
        }

        private static string FormatEnum(string value)
        {
            value = value.Replace("SimpleC", "SIMPLEC").Replace("Simple", "SIMPLE")
                         .Replace("RoeFds", "Roe-FDS").Replace("AusmUp", "AUSM-up").Replace("Ausm", "AUSM");
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
