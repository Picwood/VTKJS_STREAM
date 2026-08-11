// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramMeshRefinementVolume : Form
    {
        private readonly BaramMeshConfiguration _configuration;
        private readonly int? _geometryId;
        private readonly TextBox _txtName;
        private readonly ComboBox _cmbShape;
        private readonly NumericUpDown[] _point1;
        private readonly NumericUpDown[] _point2;
        private readonly NumericUpDown _radius;

        public BaramGeometry Geometry { get; private set; }

        public FrmBaramMeshRefinementVolume(BaramMeshConfiguration configuration, int? geometryId,
                                            BaramGeometry geometry)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (geometry == null) throw new ArgumentNullException("geometry");
            _configuration = configuration;
            _geometryId = geometryId;
            _txtName = new TextBox { Dock = DockStyle.Fill };
            _cmbShape = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbShape.Items.AddRange(new object[] { BaramShape.Hex, BaramShape.Sphere, BaramShape.Cylinder });
            _point1 = CreateVectorInputs();
            _point2 = CreateVectorInputs();
            _radius = CreateCoordinateInput();
            //
            Text = geometryId.HasValue ? "Edit Refinement Volume" : "Add Refinement Volume";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 290);
            InitializeComponent();
            LoadValues(geometry);
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                                                           Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);
            TableLayoutPanel fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddField(fields, 0, "Name", _txtName);
            AddField(fields, 1, "Shape", _cmbShape);
            AddField(fields, 2, "Point 1 / center", CreateVectorPanel(_point1));
            AddField(fields, 3, "Point 2", CreateVectorPanel(_point2));
            AddField(fields, 4, "Radius", _radius);
            root.Controls.Add(fields, 0, 0);
            FlowLayoutPanel footer = FrmBaramMeshSurfaceRefinement.CreateFooter(out Button ok, out Button cancel);
            ok.Click += btnOk_Click;
            root.Controls.Add(footer, 0, 1);
            AcceptButton = ok;
            CancelButton = cancel;
            _cmbShape.SelectedIndexChanged += cmbShape_SelectedIndexChanged;
        }

        private void LoadValues(BaramGeometry geometry)
        {
            _txtName.Text = geometry.Name;
            _cmbShape.SelectedItem = geometry.Shape;
            SetVector(_point1, geometry.Point1);
            SetVector(_point2, geometry.Point2);
            _radius.Value = FrmBaramMeshSurfaceRefinement.ClampDecimal(geometry.Radius > 0 ? geometry.Radius : 1, _radius);
            UpdateEnabledState();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                string name = BaramGeometryFactory.SanitizeGeometryName(_txtName.Text);
                if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Volume name is required.");
                if (_configuration.Geometry.Any(entry => entry.Key != _geometryId &&
                    string.Equals(entry.Value.Name, name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Geometry name already exists: " + name + ".");
                BaramShape shape = (BaramShape)_cmbShape.SelectedItem;
                BaramVector point1 = ReadVector(_point1);
                BaramVector point2 = ReadVector(_point2);
                double radius = Decimal.ToDouble(_radius.Value);
                if (shape == BaramShape.Hex &&
                    (point1.X == point2.X || point1.Y == point2.Y || point1.Z == point2.Z))
                    throw new InvalidOperationException("A refinement box must have a non-zero size in every direction.");
                if (shape == BaramShape.Cylinder && point1.X == point2.X && point1.Y == point2.Y && point1.Z == point2.Z)
                    throw new InvalidOperationException("Cylinder points must define a non-zero axis.");
                if ((shape == BaramShape.Sphere || shape == BaramShape.Cylinder) && radius <= 0)
                    throw new InvalidOperationException("Radius must be greater than zero.");
                //
                BaramGeometry previous = null;
                if (_geometryId.HasValue) _configuration.Geometry.TryGetValue(_geometryId.Value, out previous);
                Geometry = new BaramGeometry
                {
                    Name = name,
                    GeometryType = BaramGeometryType.Volume,
                    Shape = shape,
                    CfdType = BaramCfdType.None,
                    Point1 = point1,
                    Point2 = point2,
                    Radius = radius,
                    CastellationGroup = previous != null ? previous.CastellationGroup : null,
                    LayerGroup = previous != null ? previous.LayerGroup : null,
                    SlaveLayerGroup = previous != null ? previous.SlaveLayerGroup : null
                };
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void cmbShape_SelectedIndexChanged(object sender, EventArgs e) { UpdateEnabledState(); }
        private void UpdateEnabledState()
        {
            if (_cmbShape.SelectedItem == null) return;
            BaramShape shape = (BaramShape)_cmbShape.SelectedItem;
            foreach (NumericUpDown input in _point2) input.Enabled = shape != BaramShape.Sphere;
            _radius.Enabled = shape == BaramShape.Sphere || shape == BaramShape.Cylinder;
        }

        private static NumericUpDown[] CreateVectorInputs()
        {
            return new NumericUpDown[] { CreateCoordinateInput(), CreateCoordinateInput(), CreateCoordinateInput() };
        }
        private static NumericUpDown CreateCoordinateInput()
        {
            return new NumericUpDown { DecimalPlaces = 6, Minimum = -1000000000, Maximum = 1000000000,
                                       Increment = 0.1M, Width = 105 };
        }
        private static FlowLayoutPanel CreateVectorPanel(NumericUpDown[] inputs)
        {
            FlowLayoutPanel panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                                                          WrapContents = false };
            panel.Controls.AddRange(inputs);
            return panel;
        }
        private static void AddField(TableLayoutPanel fields, int row, string text, Control control)
        {
            fields.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            fields.Controls.Add(control, 1, row);
        }
        private static void SetVector(NumericUpDown[] inputs, BaramVector vector)
        {
            inputs[0].Value = FrmBaramMeshSurfaceRefinement.ClampDecimal(vector.X, inputs[0]);
            inputs[1].Value = FrmBaramMeshSurfaceRefinement.ClampDecimal(vector.Y, inputs[1]);
            inputs[2].Value = FrmBaramMeshSurfaceRefinement.ClampDecimal(vector.Z, inputs[2]);
        }
        private static BaramVector ReadVector(NumericUpDown[] inputs)
        {
            return new BaramVector(Decimal.ToDouble(inputs[0].Value), Decimal.ToDouble(inputs[1].Value),
                                   Decimal.ToDouble(inputs[2].Value));
        }
    }
}
