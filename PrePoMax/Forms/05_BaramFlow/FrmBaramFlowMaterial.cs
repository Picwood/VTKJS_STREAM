// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeModel.CFD.BaramFlow;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramFlowMaterial : Form
    {
        private readonly BaramFlowGeneralSettings _general;
        private readonly TextBox _tbName;
        private readonly ComboBox _cmbPhase;
        private readonly TextBox _tbDensityModel;
        private readonly NumericUpDown _numDensity;
        private readonly NumericUpDown _numMolecularWeight;
        private readonly NumericUpDown _numViscosity;
        private readonly NumericUpDown _numSpecificHeat;
        private readonly NumericUpDown _numConductivity;

        public BaramFlowMaterialSettings Settings { get; private set; }

        public FrmBaramFlowMaterial(BaramFlowGeneralSettings general, BaramFlowMaterialSettings settings)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (settings == null) throw new ArgumentNullException("settings");
            _general = general.Copy();
            Settings = settings.Copy();
            Settings.Normalize(_general);
            Settings.Validate(_general);
            Text = "BaramFlow Material";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(520, 560);
            _tbName = new TextBox { Dock = DockStyle.Fill };
            _cmbPhase = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _cmbPhase.Items.AddRange(new object[] { "Gas", "Liquid" });
            _tbDensityModel = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, TabStop = false };
            _numDensity = CreateNumber(8, 0.00000001M, 1000000000000M, 0.1M);
            _numMolecularWeight = CreateNumber(6, 0.000001M, 1000000000M, 0.1M);
            _numViscosity = CreateNumber(10, 0.0000000001M, 1000000000M, 0.00001M);
            _numSpecificHeat = CreateNumber(6, 0.000001M, 1000000000M, 1M);
            _numConductivity = CreateNumber(8, 0.00000001M, 1000000000M, 0.01M);
            InitializeComponent();
            LoadSettings(Settings);
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            GroupBox identity = CreateGroup("Material");
            TableLayoutPanel identityFields = CreateFieldPanel(2);
            AddField(identityFields, 0, "Name", _tbName);
            AddField(identityFields, 1, "Phase", _cmbPhase);
            identity.Controls.Add(identityFields);
            root.Controls.Add(identity, 0, 0);

            GroupBox thermo = CreateGroup("Thermodynamics");
            TableLayoutPanel thermoFields = CreateFieldPanel(3);
            AddField(thermoFields, 0, "Density model", _tbDensityModel);
            AddField(thermoFields, 1, "Density (kg/m^3)", _numDensity);
            AddField(thermoFields, 2, "Molecular weight (kg/kmol)", _numMolecularWeight);
            thermo.Controls.Add(thermoFields);
            root.Controls.Add(thermo, 0, 1);

            GroupBox transport = CreateGroup("Transport");
            TableLayoutPanel transportFields = CreateFieldPanel(1);
            AddField(transportFields, 0, "Dynamic viscosity (Pa s)", _numViscosity);
            transport.Controls.Add(transportFields);
            root.Controls.Add(transport, 0, 2);

            GroupBox thermal = CreateGroup("Thermal");
            TableLayoutPanel thermalFields = CreateFieldPanel(2);
            AddField(thermalFields, 0, "Specific heat (J/(kg K))", _numSpecificHeat);
            AddField(thermalFields, 1, "Thermal conductivity (W/(m K))", _numConductivity);
            thermal.Controls.Add(thermalFields);
            root.Controls.Add(thermal, 0, 3);

            FlowLayoutPanel tools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button defaults = new Button { Text = "Phase defaults", Width = 110 };
            defaults.Click += Defaults_Click;
            tools.Controls.Add(defaults);
            root.Controls.Add(tools, 0, 4);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88 };
            Button ok = new Button { Text = "OK", Width = 88 };
            ok.Click += Ok_Click;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            root.Controls.Add(buttons, 0, 5);
            AcceptButton = ok;
            CancelButton = cancel;
            _cmbPhase.SelectedIndexChanged += delegate { UpdateDensityModel(); };
        }

        private void LoadSettings(BaramFlowMaterialSettings settings)
        {
            _tbName.Text = settings.Name;
            _cmbPhase.SelectedIndex = settings.Phase == BaramFlowMaterialPhase.Liquid ? 1 : 0;
            SetNumber(_numDensity, settings.Density);
            SetNumber(_numMolecularWeight, settings.MolecularWeight);
            SetNumber(_numViscosity, settings.DynamicViscosity);
            SetNumber(_numSpecificHeat, settings.SpecificHeat);
            SetNumber(_numConductivity, settings.ThermalConductivity);
            UpdateDensityModel();
        }

        private void UpdateDensityModel()
        {
            bool idealGas = _general.FlowType == BaramFlowFlowType.Compressible && _cmbPhase.SelectedIndex == 0;
            _tbDensityModel.Text = idealGas ? "Ideal gas" : "Constant";
            _numDensity.Enabled = !idealGas;
        }

        private void Defaults_Click(object sender, EventArgs e)
        {
            BaramFlowMaterialSettings settings = new BaramFlowMaterialSettings();
            if (_cmbPhase.SelectedIndex == 1) settings.SetWaterDefaults();
            else settings.SetAirDefaults();
            settings.Normalize(_general);
            LoadSettings(settings);
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            try
            {
                BaramFlowMaterialSettings settings = new BaramFlowMaterialSettings
                {
                    Name = _tbName.Text.Trim(),
                    Phase = _cmbPhase.SelectedIndex == 1 ? BaramFlowMaterialPhase.Liquid : BaramFlowMaterialPhase.Gas,
                    Density = Decimal.ToDouble(_numDensity.Value),
                    MolecularWeight = Decimal.ToDouble(_numMolecularWeight.Value),
                    DynamicViscosity = Decimal.ToDouble(_numViscosity.Value),
                    SpecificHeat = Decimal.ToDouble(_numSpecificHeat.Value),
                    ThermalConductivity = Decimal.ToDouble(_numConductivity.Value)
                };
                settings.Normalize(_general);
                settings.Validate(_general);
                Settings = settings;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramFlow Material", MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        private static GroupBox CreateGroup(string text)
        {
            return new GroupBox { Text = text, Dock = DockStyle.Fill, Padding = new Padding(8) };
        }

        private static TableLayoutPanel CreateFieldPanel(int rows)
        {
            TableLayoutPanel panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = rows };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows; i++) panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
            return panel;
        }

        private static void AddField(TableLayoutPanel panel, int row, string label, Control control)
        {
            panel.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 3, 3)
            }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(3, 4, 3, 4);
            panel.Controls.Add(control, 1, row);
        }

        private static NumericUpDown CreateNumber(int decimals, decimal minimum, decimal maximum, decimal increment)
        {
            return new NumericUpDown
            {
                DecimalPlaces = decimals,
                Minimum = minimum,
                Maximum = maximum,
                Increment = increment,
                Dock = DockStyle.Fill
            };
        }

        private static void SetNumber(NumericUpDown control, double value)
        {
            decimal number = (decimal)value;
            control.Value = Math.Min(control.Maximum, Math.Max(control.Minimum, number));
        }
    }
}
