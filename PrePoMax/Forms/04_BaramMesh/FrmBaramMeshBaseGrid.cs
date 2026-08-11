// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    class FrmBaramMeshBaseGrid : Form
    {
        private readonly NumericUpDown _numXMin;
        private readonly NumericUpDown _numXMax;
        private readonly NumericUpDown _numYMin;
        private readonly NumericUpDown _numYMax;
        private readonly NumericUpDown _numZMin;
        private readonly NumericUpDown _numZMax;
        private readonly NumericUpDown _numCellsX;
        private readonly NumericUpDown _numCellsY;
        private readonly NumericUpDown _numCellsZ;
        private readonly Button _btnUseModelBounds;
        private readonly Button _btnRun;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private readonly BaramBounds _defaultBounds;
        private BaramMeshConfiguration _configuration;
        private bool _runOpenFoam;

        public BaramMeshConfiguration Configuration { get { return _configuration; } }
        public bool RunOpenFoam { get { return _runOpenFoam; } }

        public FrmBaramMeshBaseGrid(BaramMeshConfiguration configuration, BaramBounds defaultBounds)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            _configuration = configuration;
            _defaultBounds = defaultBounds;

            _numXMin = CreateCoordinateInput();
            _numXMax = CreateCoordinateInput();
            _numYMin = CreateCoordinateInput();
            _numYMax = CreateCoordinateInput();
            _numZMin = CreateCoordinateInput();
            _numZMax = CreateCoordinateInput();
            _numCellsX = CreateCellInput();
            _numCellsY = CreateCellInput();
            _numCellsZ = CreateCellInput();
            _btnUseModelBounds = new Button();
            _btnRun = new Button();
            _btnOk = new Button();
            _btnCancel = new Button();

            Text = "BaramMesh Base Grid";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(360, 292);

            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.Padding = new Padding(10);
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            TableLayoutPanel fields = new TableLayoutPanel();
            fields.Dock = DockStyle.Fill;
            fields.ColumnCount = 3;
            fields.RowCount = 8;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.Controls.Add(fields, 0, 0);

            AddHeader(fields, "Min", 1);
            AddHeader(fields, "Max", 2);
            AddBoundsRow(fields, 1, "X", _numXMin, _numXMax);
            AddBoundsRow(fields, 2, "Y", _numYMin, _numYMax);
            AddBoundsRow(fields, 3, "Z", _numZMin, _numZMax);
            AddLabel(fields, 4, "Cells X");
            fields.Controls.Add(_numCellsX, 1, 4);
            fields.SetColumnSpan(_numCellsX, 2);
            AddLabel(fields, 5, "Cells Y");
            fields.Controls.Add(_numCellsY, 1, 5);
            fields.SetColumnSpan(_numCellsY, 2);
            AddLabel(fields, 6, "Cells Z");
            fields.Controls.Add(_numCellsZ, 1, 6);
            fields.SetColumnSpan(_numCellsZ, 2);

            _btnUseModelBounds.Text = "Use Geometry Bounds";
            _btnUseModelBounds.Width = 140;
            _btnUseModelBounds.Height = 24;
            _btnUseModelBounds.Margin = new Padding(0, 5, 0, 0);
            _btnUseModelBounds.Click += btnUseModelBounds_Click;
            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Fill;
            tools.FlowDirection = FlowDirection.LeftToRight;
            tools.Controls.Add(_btnUseModelBounds);
            root.Controls.Add(tools, 0, 1);

            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.Padding = new Padding(0, 8, 0, 0);
            root.Controls.Add(footer, 0, 2);

            ConfigureButton(_btnCancel, "Cancel", btnCancel_Click);
            ConfigureButton(_btnOk, "OK", btnOk_Click);
            ConfigureButton(_btnRun, "Run", btnRun_Click);
            footer.Controls.AddRange(new Control[] { _btnCancel, _btnRun, _btnOk });

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private static NumericUpDown CreateCoordinateInput()
        {
            NumericUpDown input = new NumericUpDown();
            input.DecimalPlaces = 6;
            input.Minimum = -1000000000000M;
            input.Maximum = 1000000000000M;
            input.Increment = 1;
            input.Dock = DockStyle.Fill;
            return input;
        }

        private static NumericUpDown CreateCellInput()
        {
            NumericUpDown input = new NumericUpDown();
            input.DecimalPlaces = 0;
            input.Minimum = 1;
            input.Maximum = 100000000;
            input.Increment = 1;
            input.Dock = DockStyle.Fill;
            return input;
        }

        private static void ConfigureButton(Button button, string text, EventHandler click)
        {
            button.Text = text;
            button.Width = 75;
            button.Height = 24;
            button.Margin = new Padding(3, 0, 0, 0);
            button.Click += click;
        }

        private static void AddHeader(TableLayoutPanel panel, string text, int column)
        {
            Label label = new Label();
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            panel.Controls.Add(label, column, 0);
        }

        private static void AddBoundsRow(TableLayoutPanel panel, int row, string labelText,
                                         NumericUpDown min, NumericUpDown max)
        {
            AddLabel(panel, row, labelText);
            panel.Controls.Add(min, 1, row);
            panel.Controls.Add(max, 2, row);
        }

        private static void AddLabel(TableLayoutPanel panel, int row, string text)
        {
            Label label = new Label();
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            panel.Controls.Add(label, 0, row);
        }

        private void LoadValues()
        {
            BaramBounds bounds = GetCurrentBounds();
            SetBounds(bounds);
            _numCellsX.Value = ClampDecimal(_configuration.BaseGrid.NumCellsX, _numCellsX.Minimum, _numCellsX.Maximum);
            _numCellsY.Value = ClampDecimal(_configuration.BaseGrid.NumCellsY, _numCellsY.Minimum, _numCellsY.Maximum);
            _numCellsZ.Value = ClampDecimal(_configuration.BaseGrid.NumCellsZ, _numCellsZ.Minimum, _numCellsZ.Maximum);
        }

        private BaramBounds GetCurrentBounds()
        {
            BaramGeometry geometry;
            if (_configuration.BaseGrid.BoundingHex6.HasValue &&
                _configuration.Geometry.TryGetValue(_configuration.BaseGrid.BoundingHex6.Value, out geometry) &&
                geometry.GeometryType == BaramGeometryType.Volume &&
                geometry.Shape == BaramShape.Hex6)
            {
                return new BaramBounds(Math.Min(geometry.Point1.X, geometry.Point2.X),
                                       Math.Max(geometry.Point1.X, geometry.Point2.X),
                                       Math.Min(geometry.Point1.Y, geometry.Point2.Y),
                                       Math.Max(geometry.Point1.Y, geometry.Point2.Y),
                                       Math.Min(geometry.Point1.Z, geometry.Point2.Z),
                                       Math.Max(geometry.Point1.Z, geometry.Point2.Z));
            }
            return _defaultBounds;
        }

        private void SetBounds(BaramBounds bounds)
        {
            _numXMin.Value = ClampDecimal(bounds.XMin, _numXMin.Minimum, _numXMin.Maximum);
            _numXMax.Value = ClampDecimal(bounds.XMax, _numXMax.Minimum, _numXMax.Maximum);
            _numYMin.Value = ClampDecimal(bounds.YMin, _numYMin.Minimum, _numYMin.Maximum);
            _numYMax.Value = ClampDecimal(bounds.YMax, _numYMax.Minimum, _numYMax.Maximum);
            _numZMin.Value = ClampDecimal(bounds.ZMin, _numZMin.Minimum, _numZMin.Maximum);
            _numZMax.Value = ClampDecimal(bounds.ZMax, _numZMax.Minimum, _numZMax.Maximum);
        }

        private static decimal ClampDecimal(double value, decimal minimum, decimal maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            decimal decimalValue = (decimal)value;
            if (decimalValue < minimum) return minimum;
            if (decimalValue > maximum) return maximum;
            return decimalValue;
        }

        private static decimal ClampDecimal(int value, decimal minimum, decimal maximum)
        {
            decimal decimalValue = value;
            if (decimalValue < minimum) return minimum;
            if (decimalValue > maximum) return maximum;
            return decimalValue;
        }

        private void btnUseModelBounds_Click(object sender, EventArgs e)
        {
            SetBounds(_defaultBounds);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            AcceptChanges(false);
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            AcceptChanges(true);
        }

        private void AcceptChanges(bool runOpenFoam)
        {
            try
            {
                BaramBounds bounds = ReadBounds();
                _configuration.BaseGrid.NumCellsX = Decimal.ToInt32(_numCellsX.Value);
                _configuration.BaseGrid.NumCellsY = Decimal.ToInt32(_numCellsY.Value);
                _configuration.BaseGrid.NumCellsZ = Decimal.ToInt32(_numCellsZ.Value);
                BaramGeometryFactory.SetBaseGridBounds(_configuration, bounds, "BaseGrid");
                _configuration.Step = BaramStep.BaseGrid;
                _configuration.Validate();
                _runOpenFoam = runOpenFoam;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramMesh base grid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        private BaramBounds ReadBounds()
        {
            double xMin = Decimal.ToDouble(_numXMin.Value);
            double xMax = Decimal.ToDouble(_numXMax.Value);
            double yMin = Decimal.ToDouble(_numYMin.Value);
            double yMax = Decimal.ToDouble(_numYMax.Value);
            double zMin = Decimal.ToDouble(_numZMin.Value);
            double zMax = Decimal.ToDouble(_numZMax.Value);
            if (xMin >= xMax) throw new InvalidOperationException("X min must be smaller than X max.");
            if (yMin >= yMax) throw new InvalidOperationException("Y min must be smaller than Y max.");
            if (zMin >= zMax) throw new InvalidOperationException("Z min must be smaller than Z max.");
            return new BaramBounds(xMin, xMax, yMin, yMax, zMin, zMax);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
