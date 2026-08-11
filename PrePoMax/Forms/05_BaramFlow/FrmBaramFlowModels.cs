// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeModel.CFD.BaramFlow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramFlowModels : Form
    {
        private readonly BaramFlowGeneralSettings _general;
        private readonly CheckBox _chkEnergy;
        private readonly ComboBox _cmbTurbulence;
        private readonly TabControl _tabs;
        private readonly TabPage _tpRans;
        private readonly TabPage _tpDes;
        private readonly TabPage _tpLes;
        private readonly ComboBox _cmbKEpsilon;
        private readonly ComboBox _cmbNearWall;
        private readonly NumericUpDown _numThreshold;
        private readonly NumericUpDown _numBlendingWidth;
        private readonly NumericUpDown _numEnergyPrandtl;
        private readonly NumericUpDown _numWallPrandtl;
        private readonly NumericUpDown _numSchmidt;
        private readonly ComboBox _cmbDesRans;
        private readonly CheckBox _chkLowRe;
        private readonly CheckBox _chkDelayedDes;
        private readonly ComboBox _cmbShielding;
        private readonly ComboBox _cmbDesLength;
        private readonly NumericUpDown _numDes;
        private readonly NumericUpDown _numDesKOmega;
        private readonly NumericUpDown _numDesKEpsilon;
        private readonly ComboBox _cmbLesSubgrid;
        private readonly ComboBox _cmbLesLength;
        private readonly NumericUpDown _numLesCk;
        private readonly NumericUpDown _numLesCe;
        private readonly NumericUpDown _numLesCw;

        public BaramFlowModelsSettings Settings { get; private set; }

        public FrmBaramFlowModels(BaramFlowGeneralSettings general, BaramFlowModelsSettings settings)
        {
            if (general == null) throw new ArgumentNullException("general");
            if (settings == null) throw new ArgumentNullException("settings");
            _general = general.Copy();
            Settings = settings.Copy();
            Settings.Normalize(_general);
            Settings.Validate(_general);
            //
            Text = "BaramFlow Models";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            MinimumSize = new Size(570, 560);
            ClientSize = new Size(610, 610);
            //
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(10);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);
            //
            TableLayoutPanel summary = CreateTable();
            _chkEnergy = new CheckBox { Text = "Include energy equation", AutoSize = true };
            _cmbTurbulence = CreateEnumCombo(GetTurbulenceChoices(_general));
            AddRow(summary, "Energy", _chkEnergy);
            AddRow(summary, "Turbulence", _cmbTurbulence);
            root.Controls.Add(summary, 0, 0);
            //
            _tabs = new TabControl { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 10) };
            _tpRans = new TabPage("RANS / Transport");
            _tpDes = new TabPage("DES");
            _tpLes = new TabPage("LES");
            _tabs.TabPages.AddRange(new TabPage[] { _tpRans, _tpDes, _tpLes });
            root.Controls.Add(_tabs, 0, 1);
            //
            TableLayoutPanel rans = CreateTable();
            rans.Dock = DockStyle.Top;
            rans.Padding = new Padding(8);
            _cmbKEpsilon = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowKEpsilonModel)));
            _cmbNearWall = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowNearWallTreatment)));
            _numThreshold = CreateNumber(0.000001M, 1000000000M);
            _numBlendingWidth = CreateNumber(0.000001M, 1000000000M);
            _numEnergyPrandtl = CreateNumber(0.000001M, 1000000M);
            _numWallPrandtl = CreateNumber(0.000001M, 1000000M);
            _numSchmidt = CreateNumber(0.000001M, 1000000M);
            AddRow(rans, "k-epsilon model", _cmbKEpsilon);
            AddRow(rans, "Near-wall treatment", _cmbNearWall);
            AddRow(rans, "Reynolds threshold", _numThreshold);
            AddRow(rans, "Blending width", _numBlendingWidth);
            AddRow(rans, "Energy Prandtl number", _numEnergyPrandtl);
            AddRow(rans, "Wall Prandtl number", _numWallPrandtl);
            AddRow(rans, "Turbulent Schmidt number", _numSchmidt);
            _tpRans.Controls.Add(rans);
            //
            TableLayoutPanel des = CreateTable();
            des.Dock = DockStyle.Top;
            des.Padding = new Padding(8);
            _cmbDesRans = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowDesRansModel)));
            _chkLowRe = new CheckBox { Text = "Enabled", AutoSize = true };
            _chkDelayedDes = new CheckBox { Text = "Enabled", AutoSize = true };
            _cmbShielding = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowShieldingFunction)));
            _cmbDesLength = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowLengthScaleModel)));
            _numDes = CreateNumber(0.000001M, 1000000M);
            _numDesKOmega = CreateNumber(0.000001M, 1000000M);
            _numDesKEpsilon = CreateNumber(0.000001M, 1000000M);
            AddRow(des, "RANS model", _cmbDesRans);
            AddRow(des, "Low-Re damping", _chkLowRe);
            AddRow(des, "Delayed DES", _chkDelayedDes);
            AddRow(des, "Shielding function", _cmbShielding);
            AddRow(des, "Length-scale model", _cmbDesLength);
            AddRow(des, "C DES", _numDes);
            AddRow(des, "C DES (k-omega)", _numDesKOmega);
            AddRow(des, "C DES (k-epsilon)", _numDesKEpsilon);
            _tpDes.Controls.Add(des);
            //
            TableLayoutPanel les = CreateTable();
            les.Dock = DockStyle.Top;
            les.Padding = new Padding(8);
            _cmbLesSubgrid = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowLesSubgridScaleModel)));
            _cmbLesLength = CreateEnumCombo(Enum.GetValues(typeof(BaramFlowLengthScaleModel)));
            _numLesCk = CreateNumber(0.000001M, 1000000M);
            _numLesCe = CreateNumber(0.000001M, 1000000M);
            _numLesCw = CreateNumber(0.000001M, 1000000M);
            AddRow(les, "Subgrid-scale model", _cmbLesSubgrid);
            AddRow(les, "Length-scale model", _cmbLesLength);
            AddRow(les, "Ck", _numLesCk);
            AddRow(les, "Ce", _numLesCe);
            AddRow(les, "Cw", _numLesCw);
            _tpLes.Controls.Add(les);
            //
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88 };
            Button ok = new Button { Text = "OK", Width = 88 };
            ok.Click += Ok_Click;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            root.Controls.Add(buttons, 0, 2);
            AcceptButton = ok;
            CancelButton = cancel;
            //
            LoadSettings();
            _cmbTurbulence.SelectedIndexChanged += delegate { UpdateControlState(); };
            _cmbKEpsilon.SelectedIndexChanged += delegate { UpdateControlState(); };
            _cmbNearWall.SelectedIndexChanged += delegate { UpdateControlState(); };
            _cmbDesRans.SelectedIndexChanged += delegate { UpdateControlState(); };
            _chkDelayedDes.CheckedChanged += delegate { UpdateControlState(); };
            _cmbShielding.SelectedIndexChanged += delegate { UpdateControlState(); };
            _cmbLesSubgrid.SelectedIndexChanged += delegate { UpdateControlState(); };
            UpdateControlState();
        }

        private void LoadSettings()
        {
            _chkEnergy.Checked = Settings.EnergyEnabled;
            _chkEnergy.Enabled = _general.FlowType != BaramFlowFlowType.Compressible;
            SelectEnum(_cmbTurbulence, Settings.TurbulenceModel);
            SelectEnum(_cmbKEpsilon, Settings.KEpsilonModel);
            SelectEnum(_cmbNearWall, Settings.NearWallTreatment);
            SetNumber(_numThreshold, Settings.EnhancedWallThreshold);
            SetNumber(_numBlendingWidth, Settings.EnhancedWallBlendingWidth);
            SetNumber(_numEnergyPrandtl, Settings.EnergyPrandtlNumber);
            SetNumber(_numWallPrandtl, Settings.WallPrandtlNumber);
            SetNumber(_numSchmidt, Settings.TurbulentSchmidtNumber);
            SelectEnum(_cmbDesRans, Settings.DesRansModel);
            _chkLowRe.Checked = Settings.DesLowReDamping;
            _chkDelayedDes.Checked = Settings.DelayedDes;
            SelectEnum(_cmbShielding, Settings.ShieldingFunction);
            SelectEnum(_cmbDesLength, Settings.DesLengthScale);
            SetNumber(_numDes, Settings.DesCoefficient);
            SetNumber(_numDesKOmega, Settings.DesKOmegaCoefficient);
            SetNumber(_numDesKEpsilon, Settings.DesKEpsilonCoefficient);
            SelectEnum(_cmbLesSubgrid, Settings.LesSubgridScaleModel);
            SelectEnum(_cmbLesLength, Settings.LesLengthScale);
            SetNumber(_numLesCk, Settings.LesCk);
            SetNumber(_numLesCe, Settings.LesCe);
            SetNumber(_numLesCw, Settings.LesCw);
        }

        private void UpdateControlState()
        {
            BaramFlowTurbulenceModel model = SelectedEnum<BaramFlowTurbulenceModel>(_cmbTurbulence);
            if (model == BaramFlowTurbulenceModel.Des) _tabs.SelectedTab = _tpDes;
            else if (model == BaramFlowTurbulenceModel.Les) _tabs.SelectedTab = _tpLes;
            else _tabs.SelectedTab = _tpRans;
            _cmbKEpsilon.Enabled = model == BaramFlowTurbulenceModel.KEpsilon;
            bool realizable = _cmbKEpsilon.Enabled &&
                              SelectedEnum<BaramFlowKEpsilonModel>(_cmbKEpsilon) == BaramFlowKEpsilonModel.Realizable;
            _cmbNearWall.Enabled = realizable;
            bool enhanced = realizable &&
                            SelectedEnum<BaramFlowNearWallTreatment>(_cmbNearWall) ==
                            BaramFlowNearWallTreatment.EnhancedWallTreatment;
            _numThreshold.Enabled = enhanced;
            _numBlendingWidth.Enabled = enhanced;
            bool spalart = SelectedEnum<BaramFlowDesRansModel>(_cmbDesRans) ==
                           BaramFlowDesRansModel.SpalartAllmaras;
            _chkLowRe.Enabled = spalart;
            _numDes.Enabled = spalart;
            _numDesKOmega.Enabled = !spalart;
            _numDesKEpsilon.Enabled = !spalart;
            _cmbShielding.Enabled = _chkDelayedDes.Checked;
            bool iddes = _chkDelayedDes.Checked &&
                         SelectedEnum<BaramFlowShieldingFunction>(_cmbShielding) ==
                         BaramFlowShieldingFunction.Iddes;
            _cmbDesLength.Enabled = !iddes;
            BaramFlowLesSubgridScaleModel subgrid = SelectedEnum<BaramFlowLesSubgridScaleModel>(_cmbLesSubgrid);
            _numLesCk.Enabled = subgrid != BaramFlowLesSubgridScaleModel.DynamicKEqn;
            _numLesCe.Enabled = subgrid != BaramFlowLesSubgridScaleModel.DynamicKEqn;
            _numLesCw.Enabled = subgrid == BaramFlowLesSubgridScaleModel.Wale;
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            try
            {
                BaramFlowModelsSettings settings = new BaramFlowModelsSettings
                {
                    EnergyEnabled = _chkEnergy.Checked,
                    TurbulenceModel = SelectedEnum<BaramFlowTurbulenceModel>(_cmbTurbulence),
                    KEpsilonModel = SelectedEnum<BaramFlowKEpsilonModel>(_cmbKEpsilon),
                    NearWallTreatment = SelectedEnum<BaramFlowNearWallTreatment>(_cmbNearWall),
                    EnhancedWallThreshold = (double)_numThreshold.Value,
                    EnhancedWallBlendingWidth = (double)_numBlendingWidth.Value,
                    EnergyPrandtlNumber = (double)_numEnergyPrandtl.Value,
                    WallPrandtlNumber = (double)_numWallPrandtl.Value,
                    TurbulentSchmidtNumber = (double)_numSchmidt.Value,
                    DesRansModel = SelectedEnum<BaramFlowDesRansModel>(_cmbDesRans),
                    DesLowReDamping = _chkLowRe.Checked,
                    DelayedDes = _chkDelayedDes.Checked,
                    ShieldingFunction = SelectedEnum<BaramFlowShieldingFunction>(_cmbShielding),
                    DesLengthScale = SelectedEnum<BaramFlowLengthScaleModel>(_cmbDesLength),
                    DesCoefficient = (double)_numDes.Value,
                    DesKOmegaCoefficient = (double)_numDesKOmega.Value,
                    DesKEpsilonCoefficient = (double)_numDesKEpsilon.Value,
                    LesSubgridScaleModel = SelectedEnum<BaramFlowLesSubgridScaleModel>(_cmbLesSubgrid),
                    LesLengthScale = SelectedEnum<BaramFlowLengthScaleModel>(_cmbLesLength),
                    LesCk = (double)_numLesCk.Value,
                    LesCe = (double)_numLesCe.Value,
                    LesCw = (double)_numLesCw.Value
                };
                settings.Normalize(_general);
                settings.Validate(_general);
                Settings = settings;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramFlow Models", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static TableLayoutPanel CreateTable()
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label text = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) };
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(3, 4, 3, 4);
            table.Controls.Add(text, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private static ComboBox CreateEnumCombo(Array values)
        {
            ComboBox combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FormattingEnabled = true,
                Width = 260
            };
            foreach (object value in values) combo.Items.Add(new EnumOption(value, FormatEnum(value.ToString())));
            return combo;
        }

        private static Array GetTurbulenceChoices(BaramFlowGeneralSettings general)
        {
            Array all = Enum.GetValues(typeof(BaramFlowTurbulenceModel));
            if (general.TimeMode == BaramFlowTimeMode.Transient) return all;
            List<BaramFlowTurbulenceModel> values = new List<BaramFlowTurbulenceModel>();
            foreach (BaramFlowTurbulenceModel value in all)
                if (value != BaramFlowTurbulenceModel.Des && value != BaramFlowTurbulenceModel.Les) values.Add(value);
            return values.ToArray();
        }

        private static NumericUpDown CreateNumber(decimal minimum, decimal maximum)
        {
            return new NumericUpDown
            {
                DecimalPlaces = 6,
                Minimum = minimum,
                Maximum = maximum,
                Increment = 0.01M,
                Width = 160
            };
        }

        private static void SetNumber(NumericUpDown control, double value)
        {
            decimal number = (decimal)value;
            control.Value = Math.Min(control.Maximum, Math.Max(control.Minimum, number));
        }

        private static void SelectEnum(ComboBox combo, object value)
        {
            int index = -1;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                EnumOption option = (EnumOption)combo.Items[i];
                if (Equals(option.Value, value))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0) throw new InvalidOperationException("The selected model is not available for the current flow settings.");
            combo.SelectedIndex = index;
        }

        private static T SelectedEnum<T>(ComboBox combo)
        {
            if (combo.SelectedItem == null) return default(T);
            return (T)((EnumOption)combo.SelectedItem).Value;
        }

        private static string FormatEnum(string value)
        {
            value = value.Replace("KEpsilon", "k-epsilon").Replace("KOmegaSst", "k-omega SST")
                         .Replace("SpalartAllmaras", "Spalart-Allmaras")
                         .Replace("CubeRootVolume", "Cube-root Volume")
                         .Replace("DynamicKEqn", "Dynamic k-equation").Replace("KEqn", "k-equation")
                         .Replace("Ddes", "DDES").Replace("Iddes", "IDDES").Replace("Wale", "WALE")
                         .Replace("Des", "DES").Replace("Les", "LES").Replace("Rng", "RNG");
            return Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
        }

        private sealed class EnumOption
        {
            public object Value { get; private set; }
            private readonly string _text;

            public EnumOption(object value, string text)
            {
                Value = value;
                _text = text;
            }

            public override string ToString()
            {
                return _text;
            }
        }
    }
}
