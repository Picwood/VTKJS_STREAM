// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeModel.CFD.BaramFlow;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramFlowBoundaryConditions : Form
    {
        private readonly DataGridView _grid;
        private readonly NumericUpDown[] _initial;
        private readonly BaramFlowBoundarySettings _original;
        public BaramFlowBoundarySettings Settings { get; private set; }

        public FrmBaramFlowBoundaryConditions(BaramFlowBoundarySettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _original = settings.Copy();
            _original.Validate();
            Settings = _original.Copy();
            Text = "BaramFlow Boundary Conditions";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 520);
            ClientSize = new Size(1120, 650);
            _grid = new DataGridView();
            _initial = new NumericUpDown[7];
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            GroupBox initialGroup = new GroupBox { Text = "Initial Values", Dock = DockStyle.Fill, Padding = new Padding(8) };
            TableLayoutPanel initial = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2 };
            for (int i = 0; i < 8; i++) initial.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            string[] labels = { "Gauge pressure (Pa)", "Velocity X (m/s)", "Velocity Y (m/s)", "Velocity Z (m/s)",
                                "Temperature (K)", "Turbulence (%)", "Viscosity ratio" };
            for (int i = 0; i < labels.Length; i++)
            {
                initial.Controls.Add(new Label { Text = labels[i], AutoSize = true, Anchor = AnchorStyles.Left }, i, 0);
                _initial[i] = CreateNumber(i == 5 ? 4 : 6, i == 4 ? 0.000001M : -1000000000000M,
                                           i == 5 ? 100 : 1000000000000M);
                if (i == 6) _initial[i].Minimum = 0.000001M;
                initial.Controls.Add(_initial[i], i, 1);
            }
            initialGroup.Controls.Add(initial);
            root.Controls.Add(initialGroup, 0, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoGenerateColumns = false;
            AddTextColumn("Name", "Patch", 150, true);
            AddTextColumn("MeshType", "Mesh type", 80, true);
            AddTextColumn("FaceCount", "Faces", 60, true);
            DataGridViewComboBoxColumn type = new DataGridViewComboBoxColumn
            {
                Name = "BoundaryType",
                HeaderText = "Boundary type",
                DataSource = Enum.GetValues(typeof(BaramFlowBoundaryType)),
                Width = 125,
                FlatStyle = FlatStyle.Flat
            };
            _grid.Columns.Add(type);
            AddTextColumn("VelocityX", "U X", 75, false);
            AddTextColumn("VelocityY", "U Y", 75, false);
            AddTextColumn("VelocityZ", "U Z", 75, false);
            AddTextColumn("GaugePressure", "Gauge p", 85, false);
            AddTextColumn("Temperature", "T (K)", 75, false);
            AddTextColumn("Turbulence", "Turb. (%)", 80, false);
            AddTextColumn("ViscosityRatio", "Visc. ratio", 85, false);
            _grid.CellBeginEdit += Grid_CellBeginEdit;
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            root.Controls.Add(_grid, 0, 1);

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
            root.Controls.Add(buttons, 0, 2);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void LoadSettings()
        {
            SetNumber(_initial[0], Settings.InitialGaugePressure);
            SetNumber(_initial[1], Settings.InitialVelocityX);
            SetNumber(_initial[2], Settings.InitialVelocityY);
            SetNumber(_initial[3], Settings.InitialVelocityZ);
            SetNumber(_initial[4], Settings.InitialTemperature);
            SetNumber(_initial[5], Settings.InitialTurbulenceIntensity * 100);
            SetNumber(_initial[6], Settings.InitialTurbulentViscosityRatio);
            _grid.Rows.Clear();
            foreach (BaramFlowBoundaryCondition boundary in Settings.Boundaries)
                _grid.Rows.Add(boundary.Name, boundary.MeshType, boundary.FaceCount, boundary.BoundaryType,
                               boundary.VelocityX, boundary.VelocityY, boundary.VelocityZ,
                               boundary.GaugePressure, boundary.Temperature,
                               boundary.TurbulenceIntensity * 100, boundary.TurbulentViscosityRatio);
        }

        private void Grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name == "BoundaryType" &&
                _original.Boundaries[e.RowIndex].BoundaryType == BaramFlowBoundaryType.FsiInterface) e.Cancel = true;
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            try
            {
                _grid.EndEdit();
                BaramFlowBoundarySettings settings = _original.Copy();
                settings.InitialGaugePressure = Value(_initial[0]);
                settings.InitialVelocityX = Value(_initial[1]);
                settings.InitialVelocityY = Value(_initial[2]);
                settings.InitialVelocityZ = Value(_initial[3]);
                settings.InitialTemperature = Value(_initial[4]);
                settings.InitialTurbulenceIntensity = Value(_initial[5]) / 100;
                settings.InitialTurbulentViscosityRatio = Value(_initial[6]);
                for (int i = 0; i < settings.Boundaries.Length; i++)
                {
                    DataGridViewRow row = _grid.Rows[i];
                    BaramFlowBoundaryCondition boundary = settings.Boundaries[i];
                    BaramFlowBoundaryType type = (BaramFlowBoundaryType)row.Cells["BoundaryType"].Value;
                    if (_original.Boundaries[i].BoundaryType != BaramFlowBoundaryType.FsiInterface &&
                        type == BaramFlowBoundaryType.FsiInterface)
                        throw new InvalidOperationException("FSI interface roles must be assigned in BaramMesh.");
                    boundary.BoundaryType = type;
                    boundary.VelocityX = CellDouble(row, "VelocityX");
                    boundary.VelocityY = CellDouble(row, "VelocityY");
                    boundary.VelocityZ = CellDouble(row, "VelocityZ");
                    boundary.GaugePressure = CellDouble(row, "GaugePressure");
                    boundary.Temperature = CellDouble(row, "Temperature");
                    boundary.TurbulenceIntensity = CellDouble(row, "Turbulence") / 100;
                    boundary.TurbulentViscosityRatio = CellDouble(row, "ViscosityRatio");
                }
                settings.Validate();
                Settings = settings;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramFlow Boundary Conditions", MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        private void AddTextColumn(string name, string header, int width, bool readOnly)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private static NumericUpDown CreateNumber(int decimals, decimal minimum, decimal maximum)
        {
            return new NumericUpDown
            {
                DecimalPlaces = decimals,
                Minimum = minimum,
                Maximum = maximum,
                Dock = DockStyle.Fill
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

        private static double CellDouble(DataGridViewRow row, string name)
        {
            object value = row.Cells[name].Value;
            if (value == null) throw new InvalidOperationException("A boundary value is missing in column " + name + ".");
            return Convert.ToDouble(value);
        }
    }
}
