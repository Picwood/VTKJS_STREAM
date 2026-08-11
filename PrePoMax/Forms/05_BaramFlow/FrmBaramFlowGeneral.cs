// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeModel.CFD.BaramFlow;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramFlowGeneral : Form
    {
        private readonly ComboBox _cmbSolverType;
        private readonly ComboBox _cmbFlowType;
        private readonly RadioButton _rbSteady;
        private readonly RadioButton _rbTransient;
        private readonly NumericUpDown _numGravityX;
        private readonly NumericUpDown _numGravityY;
        private readonly NumericUpDown _numGravityZ;
        private readonly NumericUpDown _numOperatingPressure;
        private readonly GroupBox _grpGravity;
        private readonly GroupBox _grpOperatingConditions;
        private readonly Button _btnDefaults;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private bool _loading;

        public BaramFlowGeneralSettings Settings { get; private set; }

        public FrmBaramFlowGeneral(BaramFlowGeneralSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            Settings = settings.Copy();
            //
            _cmbSolverType = CreateDropDown();
            _cmbSolverType.Items.Add("Pressure based");
            _cmbSolverType.Items.Add("Density based");
            _cmbFlowType = CreateDropDown();
            _cmbFlowType.Items.Add("Incompressible");
            _cmbFlowType.Items.Add("Compressible");
            _rbSteady = new RadioButton { Text = "Steady", AutoSize = true };
            _rbTransient = new RadioButton { Text = "Transient", AutoSize = true };
            _numGravityX = CreateGravityInput();
            _numGravityY = CreateGravityInput();
            _numGravityZ = CreateGravityInput();
            _numOperatingPressure = CreatePressureInput();
            _grpGravity = CreateGroup("Gravity");
            _grpOperatingConditions = CreateGroup("Operating Conditions");
            _btnDefaults = new Button();
            _btnOk = new Button();
            _btnCancel = new Button();
            //
            Text = "BaramFlow General";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(470, 475);
            InitializeComponent();
            LoadSettings(Settings);
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);
            //
            GroupBox flow = CreateGroup("Flow");
            TableLayoutPanel flowFields = CreateFieldPanel(2);
            AddField(flowFields, 0, "Solver type", _cmbSolverType);
            AddField(flowFields, 1, "Flow type", _cmbFlowType);
            flow.Controls.Add(flowFields);
            root.Controls.Add(flow, 0, 0);
            //
            GroupBox time = CreateGroup("Time");
            FlowLayoutPanel timeOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 8, 0, 0)
            };
            _rbSteady.Margin = new Padding(3, 3, 28, 3);
            timeOptions.Controls.AddRange(new Control[] { _rbSteady, _rbTransient });
            time.Controls.Add(timeOptions);
            root.Controls.Add(time, 0, 1);
            //
            TableLayoutPanel gravityFields = CreateFieldPanel(3);
            AddField(gravityFields, 0, "X (m/s^2)", _numGravityX);
            AddField(gravityFields, 1, "Y (m/s^2)", _numGravityY);
            AddField(gravityFields, 2, "Z (m/s^2)", _numGravityZ);
            _grpGravity.Controls.Add(gravityFields);
            root.Controls.Add(_grpGravity, 0, 2);
            //
            TableLayoutPanel operatingFields = CreateFieldPanel(1);
            AddField(operatingFields, 0, "Operating pressure (Pa)", _numOperatingPressure);
            _grpOperatingConditions.Controls.Add(operatingFields);
            root.Controls.Add(_grpOperatingConditions, 0, 3);
            //
            FlowLayoutPanel tools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 8, 0, 0)
            };
            ConfigureButton(_btnDefaults, "Defaults", 85, btnDefaults_Click);
            tools.Controls.Add(_btnDefaults);
            root.Controls.Add(tools, 0, 4);
            //
            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            ConfigureButton(_btnCancel, "Cancel", 80, btnCancel_Click);
            ConfigureButton(_btnOk, "OK", 80, btnOk_Click);
            footer.Controls.AddRange(new Control[] { _btnCancel, _btnOk });
            root.Controls.Add(footer, 0, 5);
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
            //
            _cmbSolverType.SelectedIndexChanged += cmbSolverType_SelectedIndexChanged;
        }

        private void LoadSettings(BaramFlowGeneralSettings settings)
        {
            _loading = true;
            try
            {
                _cmbSolverType.SelectedIndex =
                    settings.SolverType == BaramFlowSolverType.DensityBased ? 1 : 0;
                _cmbFlowType.SelectedIndex =
                    settings.FlowType == BaramFlowFlowType.Compressible ? 1 : 0;
                _rbTransient.Checked = settings.TimeMode == BaramFlowTimeMode.Transient;
                _rbSteady.Checked = !_rbTransient.Checked;
                _numGravityX.Value = Clamp(settings.GravityX, _numGravityX.Minimum, _numGravityX.Maximum);
                _numGravityY.Value = Clamp(settings.GravityY, _numGravityY.Minimum, _numGravityY.Maximum);
                _numGravityZ.Value = Clamp(settings.GravityZ, _numGravityZ.Minimum, _numGravityZ.Maximum);
                _numOperatingPressure.Value =
                    Clamp(settings.OperatingPressure, _numOperatingPressure.Minimum, _numOperatingPressure.Maximum);
            }
            finally
            {
                _loading = false;
            }
            UpdateSolverDependentControls();
        }

        private void cmbSolverType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            UpdateSolverDependentControls();
        }

        private void UpdateSolverDependentControls()
        {
            bool densityBased = _cmbSolverType.SelectedIndex == 1;
            _cmbFlowType.Enabled = !densityBased;
            _grpGravity.Enabled = !densityBased;
            _grpOperatingConditions.Enabled = !densityBased;
            if (densityBased)
            {
                _cmbFlowType.SelectedIndex = 1;
                _numGravityX.Value = 0;
                _numGravityY.Value = 0;
                _numGravityZ.Value = 0;
                _numOperatingPressure.Value = 0;
            }
        }

        private void btnDefaults_Click(object sender, EventArgs e)
        {
            LoadSettings(new BaramFlowGeneralSettings());
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                BaramFlowGeneralSettings settings = new BaramFlowGeneralSettings
                {
                    SolverType = _cmbSolverType.SelectedIndex == 1
                                     ? BaramFlowSolverType.DensityBased
                                     : BaramFlowSolverType.PressureBased,
                    FlowType = _cmbFlowType.SelectedIndex == 1
                                   ? BaramFlowFlowType.Compressible
                                   : BaramFlowFlowType.Incompressible,
                    TimeMode = _rbTransient.Checked ? BaramFlowTimeMode.Transient : BaramFlowTimeMode.Steady,
                    GravityX = Decimal.ToDouble(_numGravityX.Value),
                    GravityY = Decimal.ToDouble(_numGravityY.Value),
                    GravityZ = Decimal.ToDouble(_numGravityZ.Value),
                    OperatingPressure = Decimal.ToDouble(_numOperatingPressure.Value)
                };
                settings.Normalize();
                settings.Validate();
                Settings = settings;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramFlow General", MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private static ComboBox CreateDropDown()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        }

        private static NumericUpDown CreateGravityInput()
        {
            return new NumericUpDown
            {
                DecimalPlaces = 6,
                Minimum = -200,
                Maximum = 200,
                Increment = 0.1M,
                Dock = DockStyle.Fill
            };
        }

        private static NumericUpDown CreatePressureInput()
        {
            return new NumericUpDown
            {
                DecimalPlaces = 3,
                Minimum = 0,
                Maximum = 1000000000000M,
                Increment = 100,
                Dock = DockStyle.Fill,
                ThousandsSeparator = true
            };
        }

        private static GroupBox CreateGroup(string text)
        {
            return new GroupBox { Text = text, Dock = DockStyle.Fill, Padding = new Padding(8) };
        }

        private static TableLayoutPanel CreateFieldPanel(int rows)
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = rows,
                Padding = new Padding(4)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            for (int i = 0; i < rows; i++) panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
            return panel;
        }

        private static void AddField(TableLayoutPanel panel, int row, string text, Control control)
        {
            Label label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            control.Margin = new Padding(3, 2, 3, 2);
            panel.Controls.Add(label, 0, row);
            panel.Controls.Add(control, 1, row);
        }

        private static void ConfigureButton(Button button, string text, int width, EventHandler click)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 24;
            button.Margin = new Padding(3, 0, 0, 0);
            button.Click += click;
        }

        private static decimal Clamp(double value, decimal minimum, decimal maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            decimal result = (decimal)value;
            if (result < minimum) return minimum;
            if (result > maximum) return maximum;
            return result;
        }
    }
}
