// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramMeshSurfaceRefinement : Form
    {
        private readonly BaramMeshConfiguration _configuration;
        private readonly int? _groupId;
        private readonly TextBox _txtName;
        private readonly NumericUpDown _numMinimumLevel;
        private readonly NumericUpDown _numMaximumLevel;
        private readonly NumericUpDown _numFeatureLevel;
        private readonly Label _labMinimumCellSize;
        private readonly Label _labMaximumCellSize;
        private readonly Label _labFeatureCellSize;
        private readonly CheckBox _chkCurvature;
        private readonly NumericUpDown _numCurvatureCells;
        private readonly NumericUpDown _numCurvatureMaximumLevel;
        private readonly Label _labCurvatureCellSize;
        private readonly CheckBox _chkExcludeSharp;
        private readonly NumericUpDown _numMinimumRadius;
        private readonly CheckedListBox _surfaces;
        private readonly double[] _baseCellSize;

        public BaramSurfaceRefinement Refinement { get; private set; }
        public int[] GeometryIds { get; private set; }

        public FrmBaramMeshSurfaceRefinement(BaramMeshConfiguration configuration, int? groupId)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            _configuration = configuration;
            _groupId = groupId;
            _baseCellSize = BaramMeshRefinementDialogTools.GetBaseCellSize(configuration);
            //
            _txtName = new TextBox();
            _numMinimumLevel = CreateIntegerInput(0, 10);
            _numMaximumLevel = CreateIntegerInput(1, 10);
            _numFeatureLevel = CreateIntegerInput(1, 10);
            _labMinimumCellSize = CreateCellSizeLabel();
            _labMaximumCellSize = CreateCellSizeLabel();
            _labFeatureCellSize = CreateCellSizeLabel();
            _chkCurvature = new CheckBox { Text = "Enable curvature refinement", AutoSize = true };
            _numCurvatureCells = CreateIntegerInput(1, 1000000);
            _numCurvatureMaximumLevel = CreateIntegerInput(1, 10);
            _labCurvatureCellSize = CreateCellSizeLabel();
            _chkExcludeSharp = new CheckBox { Text = "Do not refine sharp surfaces", AutoSize = true };
            _numMinimumRadius = new NumericUpDown
            {
                DecimalPlaces = 6,
                Minimum = 0.000001M,
                Maximum = 1000000000,
                Increment = 0.1M,
                Dock = DockStyle.Fill
            };
            _surfaces = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
            //
            Text = groupId.HasValue ? "Edit Surface Refinement" : "Add Surface Refinement";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(620, 580);
            Size = new Size(700, 650);
            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 245));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);
            //
            TableLayoutPanel namePanel = CreateFieldPanel(2);
            AddField(namePanel, 0, "Group name", _txtName);
            root.Controls.Add(namePanel, 0, 0);
            //
            GroupBox parameters = new GroupBox { Text = "Refinement", Dock = DockStyle.Fill };
            TableLayoutPanel fields = CreateFieldPanel(7);
            AddField(fields, 0, "Minimum level", _numMinimumLevel);
            fields.Controls.Add(_labMinimumCellSize, 2, 0);
            AddField(fields, 1, "Maximum level", _numMaximumLevel);
            fields.Controls.Add(_labMaximumCellSize, 2, 1);
            AddField(fields, 2, "Feature edge level", _numFeatureLevel);
            fields.Controls.Add(_labFeatureCellSize, 2, 2);
            fields.Controls.Add(_chkCurvature, 0, 3);
            fields.SetColumnSpan(_chkCurvature, 3);
            AddField(fields, 4, "Cells per radius", _numCurvatureCells);
            AddField(fields, 5, "Curvature max level", _numCurvatureMaximumLevel);
            fields.Controls.Add(_labCurvatureCellSize, 2, 5);
            FlowLayoutPanel sharp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            sharp.Controls.Add(_chkExcludeSharp);
            sharp.Controls.Add(new Label { Text = "Minimum radius", AutoSize = true, Margin = new Padding(18, 6, 3, 0) });
            sharp.Controls.Add(_numMinimumRadius);
            fields.Controls.Add(sharp, 0, 6);
            fields.SetColumnSpan(sharp, 3);
            parameters.Controls.Add(fields);
            root.Controls.Add(parameters, 0, 1);
            //
            GroupBox selected = new GroupBox { Text = "Surfaces", Dock = DockStyle.Fill };
            selected.Controls.Add(_surfaces);
            root.Controls.Add(selected, 0, 2);
            //
            FlowLayoutPanel footer = CreateFooter(out Button ok, out Button cancel);
            ok.Click += btnOk_Click;
            root.Controls.Add(footer, 0, 3);
            AcceptButton = ok;
            CancelButton = cancel;
            //
            _numMinimumLevel.ValueChanged += level_ValueChanged;
            _numMaximumLevel.ValueChanged += level_ValueChanged;
            _numFeatureLevel.ValueChanged += level_ValueChanged;
            _numCurvatureMaximumLevel.ValueChanged += level_ValueChanged;
            _chkCurvature.CheckedChanged += option_CheckedChanged;
            _chkExcludeSharp.CheckedChanged += option_CheckedChanged;
        }

        private void LoadValues()
        {
            BaramSurfaceRefinement refinement = null;
            if (_groupId.HasValue)
                _configuration.Castellation.RefinementSurfaces.TryGetValue(_groupId.Value, out refinement);
            if (refinement == null) refinement = new BaramSurfaceRefinement();
            //
            _txtName.Text = _groupId.HasValue ? refinement.GroupName : GetUniqueGroupName("Group_");
            _numMinimumLevel.Value = refinement.MinimumLevel;
            _numMaximumLevel.Value = refinement.MaximumLevel;
            _numFeatureLevel.Value = refinement.FeatureEdgeRefinementLevel;
            _chkCurvature.Checked = !refinement.CurvatureRefinementDisabled;
            _numCurvatureCells.Value = Math.Max(1, refinement.CurvatureNumberOfCells);
            _numCurvatureMaximumLevel.Value = Math.Max(1, refinement.CurvatureMaxLevel);
            _chkExcludeSharp.Checked = refinement.CurvatureExcludeSharpSurface;
            double radius = refinement.CurvatureMinRadius;
            if (radius <= 0 && _baseCellSize != null) radius = _baseCellSize.Min();
            _numMinimumRadius.Value = ClampDecimal(radius > 0 ? radius : 0.000001, _numMinimumRadius);
            //
            foreach (KeyValuePair<int, BaramGeometry> entry in _configuration.Geometry.OrderBy(e => e.Key))
            {
                BaramGeometry geometry = entry.Value;
                if (geometry == null || geometry.GeometryType != BaramGeometryType.Surface) continue;
                if (BaramMeshRefinementDialogTools.IsBaseGridGeometry(entry.Key, geometry, _configuration)) continue;
                if (geometry.CastellationGroup.HasValue && geometry.CastellationGroup != _groupId) continue;
                int index = _surfaces.Items.Add(new GeometryItem(entry.Key, geometry.Name));
                if (geometry.CastellationGroup == _groupId) _surfaces.SetItemChecked(index, true);
            }
            UpdateCellSizes();
            UpdateEnabledState();
        }

        private string GetUniqueGroupName(string prefix)
        {
            int index = 1;
            HashSet<string> names = new HashSet<string>(
                _configuration.Castellation.RefinementSurfaces.Values.Select(r => r.GroupName),
                StringComparer.OrdinalIgnoreCase);
            while (names.Contains(prefix + index.ToString(CultureInfo.InvariantCulture))) index++;
            return prefix + index.ToString(CultureInfo.InvariantCulture);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                string name = BaramGeometryFactory.SanitizeGeometryName(_txtName.Text);
                if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Group name is required.");
                if (_configuration.Castellation.RefinementSurfaces.Any(entry => entry.Key != _groupId &&
                    string.Equals(entry.Value.GroupName, name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("A surface refinement group named " + name + " already exists.");
                int[] ids = GetCheckedGeometryIds();
                if (ids.Length == 0) throw new InvalidOperationException("Select at least one surface.");
                if (_numMinimumLevel.Value > _numMaximumLevel.Value)
                    throw new InvalidOperationException("Minimum level cannot exceed maximum level.");
                //
                Refinement = new BaramSurfaceRefinement
                {
                    GroupName = name,
                    MinimumLevel = Decimal.ToInt32(_numMinimumLevel.Value),
                    MaximumLevel = Decimal.ToInt32(_numMaximumLevel.Value),
                    FeatureEdgeRefinementLevel = Decimal.ToInt32(_numFeatureLevel.Value),
                    CurvatureRefinementDisabled = !_chkCurvature.Checked,
                    CurvatureNumberOfCells = Decimal.ToInt32(_numCurvatureCells.Value),
                    CurvatureMaxLevel = Decimal.ToInt32(_numCurvatureMaximumLevel.Value),
                    CurvatureExcludeSharpSurface = _chkExcludeSharp.Checked,
                    CurvatureMinRadius = Decimal.ToDouble(_numMinimumRadius.Value)
                };
                Refinement.Validate();
                GeometryIds = ids;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private int[] GetCheckedGeometryIds()
        {
            List<int> ids = new List<int>();
            foreach (object item in _surfaces.CheckedItems) ids.Add(((GeometryItem)item).Id);
            return ids.ToArray();
        }

        private void level_ValueChanged(object sender, EventArgs e) { UpdateCellSizes(); }
        private void option_CheckedChanged(object sender, EventArgs e) { UpdateEnabledState(); }

        private void UpdateCellSizes()
        {
            _labMinimumCellSize.Text = BaramMeshRefinementDialogTools.FormatCellSize(_baseCellSize, (int)_numMinimumLevel.Value);
            _labMaximumCellSize.Text = BaramMeshRefinementDialogTools.FormatCellSize(_baseCellSize, (int)_numMaximumLevel.Value);
            _labFeatureCellSize.Text = BaramMeshRefinementDialogTools.FormatCellSize(_baseCellSize, (int)_numFeatureLevel.Value);
            _labCurvatureCellSize.Text = BaramMeshRefinementDialogTools.FormatCellSize(_baseCellSize,
                                                                                       (int)_numCurvatureMaximumLevel.Value);
        }

        private void UpdateEnabledState()
        {
            _numCurvatureCells.Enabled = _chkCurvature.Checked;
            _numCurvatureMaximumLevel.Enabled = _chkCurvature.Checked;
            _chkExcludeSharp.Enabled = _chkCurvature.Checked;
            _numMinimumRadius.Enabled = _chkCurvature.Checked && _chkExcludeSharp.Checked;
        }

        private static NumericUpDown CreateIntegerInput(int min, int max)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Dock = DockStyle.Fill };
        }

        private static Label CreateCellSizeLabel()
        {
            return new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        }

        internal static TableLayoutPanel CreateFieldPanel(int rows)
        {
            TableLayoutPanel panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = rows,
                                                            Padding = new Padding(8) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return panel;
        }

        internal static void AddField(TableLayoutPanel panel, int row, string text, Control control)
        {
            panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            panel.Controls.Add(control, 1, row);
        }

        internal static FlowLayoutPanel CreateFooter(out Button ok, out Button cancel)
        {
            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                                                           Padding = new Padding(0, 8, 0, 0) };
            cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            ok = new Button { Text = "OK", Width = 80 };
            footer.Controls.Add(cancel);
            footer.Controls.Add(ok);
            return footer;
        }

        internal static decimal ClampDecimal(double value, NumericUpDown input)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return input.Minimum;
            decimal result = (decimal)value;
            return Math.Min(input.Maximum, Math.Max(input.Minimum, result));
        }

        private class GeometryItem
        {
            public int Id { get; private set; }
            private readonly string _name;
            public GeometryItem(int id, string name) { Id = id; _name = name; }
            public override string ToString() { return _name; }
        }
    }

    internal class FrmBaramMeshVolumeRefinement : Form
    {
        private readonly BaramMeshConfiguration _configuration;
        private readonly int? _groupId;
        private readonly TextBox _txtName;
        private readonly NumericUpDown _numLevel;
        private readonly Label _labCellSize;
        private readonly CheckBox _chkGap;
        private readonly NumericUpDown _numGapMinLayers;
        private readonly NumericUpDown _numGapStart;
        private readonly NumericUpDown _numGapMax;
        private readonly ComboBox _cmbGapDirection;
        private readonly CheckBox _chkGapSelf;
        private readonly CheckBox _chkIncrement;
        private readonly NumericUpDown _numIncrementMin;
        private readonly NumericUpDown _numIncrementMax;
        private readonly NumericUpDown _numSplitX;
        private readonly NumericUpDown _numSplitY;
        private readonly NumericUpDown _numSplitZ;
        private readonly CheckedListBox _volumes;
        private readonly double[] _baseCellSize;

        public BaramVolumeRefinement Refinement { get; private set; }
        public int[] GeometryIds { get; private set; }

        public FrmBaramMeshVolumeRefinement(BaramMeshConfiguration configuration, int? groupId)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            _configuration = configuration;
            _groupId = groupId;
            _baseCellSize = BaramMeshRefinementDialogTools.GetBaseCellSize(configuration);
            //
            _txtName = new TextBox();
            _numLevel = CreateIntegerInput(0, 10);
            _labCellSize = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _chkGap = new CheckBox { Text = "Enable gap refinement", AutoSize = true };
            _numGapMinLayers = CreateIntegerInput(4, 1000000);
            _numGapStart = CreateIntegerInput(0, 10);
            _numGapMax = CreateIntegerInput(1, 10);
            _cmbGapDirection = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _cmbGapDirection.Items.AddRange(new object[] { BaramGapRefinementMode.Mixed, BaramGapRefinementMode.Inside,
                                                          BaramGapRefinementMode.Outside });
            _chkGapSelf = new CheckBox { Text = "Detect gaps within the same surface", AutoSize = true };
            _chkIncrement = new CheckBox { Text = "Enable directional level increment", AutoSize = true };
            _numIncrementMin = CreateIntegerInput(0, 10);
            _numIncrementMax = CreateIntegerInput(0, 10);
            _numSplitX = CreateIntegerInput(0, 10);
            _numSplitY = CreateIntegerInput(0, 10);
            _numSplitZ = CreateIntegerInput(0, 10);
            _volumes = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
            //
            Text = groupId.HasValue ? "Edit Volume Refinement" : "Add Volume Refinement";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(620, 640);
            Size = new Size(700, 720);
            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                                                           Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 225));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);
            //
            TableLayoutPanel name = FrmBaramMeshSurfaceRefinement.CreateFieldPanel(1);
            FrmBaramMeshSurfaceRefinement.AddField(name, 0, "Group name", _txtName);
            root.Controls.Add(name, 0, 0);
            GroupBox level = new GroupBox { Text = "Volume refinement", Dock = DockStyle.Fill };
            TableLayoutPanel levelFields = FrmBaramMeshSurfaceRefinement.CreateFieldPanel(1);
            FrmBaramMeshSurfaceRefinement.AddField(levelFields, 0, "Refinement level", _numLevel);
            levelFields.Controls.Add(_labCellSize, 2, 0);
            level.Controls.Add(levelFields);
            root.Controls.Add(level, 0, 1);
            //
            GroupBox options = new GroupBox { Text = "Advanced refinement", Dock = DockStyle.Fill };
            TableLayoutPanel fields = FrmBaramMeshSurfaceRefinement.CreateFieldPanel(8);
            fields.Controls.Add(_chkGap, 0, 0); fields.SetColumnSpan(_chkGap, 3);
            FrmBaramMeshSurfaceRefinement.AddField(fields, 1, "Minimum cell layers", _numGapMinLayers);
            FrmBaramMeshSurfaceRefinement.AddField(fields, 2, "Detection start level", _numGapStart);
            FrmBaramMeshSurfaceRefinement.AddField(fields, 3, "Maximum gap level", _numGapMax);
            FrmBaramMeshSurfaceRefinement.AddField(fields, 4, "Gap direction", _cmbGapDirection);
            fields.Controls.Add(_chkGapSelf, 2, 4);
            fields.Controls.Add(_chkIncrement, 0, 5); fields.SetColumnSpan(_chkIncrement, 3);
            FlowLayoutPanel increment = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            increment.Controls.Add(new Label { Text = "Levels", AutoSize = true, Margin = new Padding(0, 6, 3, 0) });
            increment.Controls.Add(_numIncrementMin);
            increment.Controls.Add(new Label { Text = "to", AutoSize = true, Margin = new Padding(6, 6, 3, 0) });
            increment.Controls.Add(_numIncrementMax);
            fields.Controls.Add(increment, 0, 6); fields.SetColumnSpan(increment, 3);
            FlowLayoutPanel split = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            split.Controls.Add(new Label { Text = "Split counts X / Y / Z", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            split.Controls.Add(_numSplitX); split.Controls.Add(_numSplitY); split.Controls.Add(_numSplitZ);
            fields.Controls.Add(split, 0, 7); fields.SetColumnSpan(split, 3);
            options.Controls.Add(fields);
            root.Controls.Add(options, 0, 2);
            //
            GroupBox selected = new GroupBox { Text = "Volumes", Dock = DockStyle.Fill };
            selected.Controls.Add(_volumes);
            root.Controls.Add(selected, 0, 3);
            FlowLayoutPanel footer = FrmBaramMeshSurfaceRefinement.CreateFooter(out Button ok, out Button cancel);
            ok.Click += btnOk_Click;
            root.Controls.Add(footer, 0, 4);
            AcceptButton = ok;
            CancelButton = cancel;
            //
            _numLevel.ValueChanged += level_ValueChanged;
            _chkGap.CheckedChanged += option_CheckedChanged;
            _chkIncrement.CheckedChanged += option_CheckedChanged;
        }

        private void LoadValues()
        {
            BaramVolumeRefinement refinement = null;
            if (_groupId.HasValue) _configuration.Castellation.RefinementVolumes.TryGetValue(_groupId.Value, out refinement);
            if (refinement == null) refinement = new BaramVolumeRefinement();
            _txtName.Text = _groupId.HasValue ? refinement.GroupName : GetUniqueGroupName();
            _numLevel.Value = refinement.VolumeRefinementLevel;
            _chkGap.Checked = refinement.GapDirection != BaramGapRefinementMode.None;
            _numGapMinLayers.Value = Math.Max(4, refinement.GapMinCellLayers);
            _numGapStart.Value = refinement.GapDetectionStartLevel;
            _numGapMax.Value = refinement.GapMaxRefinementLevel;
            _cmbGapDirection.SelectedItem = refinement.GapDirection == BaramGapRefinementMode.None
                                           ? BaramGapRefinementMode.Mixed : refinement.GapDirection;
            _chkGapSelf.Checked = refinement.GapSelf;
            _chkIncrement.Checked = !refinement.LevelIncrementDisabled;
            _numIncrementMin.Value = refinement.MinLevel;
            _numIncrementMax.Value = refinement.MaxLevel;
            _numSplitX.Value = refinement.SplitCountX;
            _numSplitY.Value = refinement.SplitCountY;
            _numSplitZ.Value = refinement.SplitCountZ;
            //
            foreach (KeyValuePair<int, BaramGeometry> entry in _configuration.Geometry.OrderBy(e => e.Key))
            {
                BaramGeometry geometry = entry.Value;
                if (geometry == null || geometry.GeometryType != BaramGeometryType.Volume) continue;
                if (BaramMeshRefinementDialogTools.IsBaseGridGeometry(entry.Key, geometry, _configuration)) continue;
                if (geometry.CastellationGroup.HasValue && geometry.CastellationGroup != _groupId) continue;
                int index = _volumes.Items.Add(new GeometryItem(entry.Key, geometry.Name));
                if (geometry.CastellationGroup == _groupId) _volumes.SetItemChecked(index, true);
            }
            UpdateCellSize();
            UpdateEnabledState();
        }

        private string GetUniqueGroupName()
        {
            int index = 1;
            HashSet<string> names = new HashSet<string>(
                _configuration.Castellation.RefinementVolumes.Values.Select(r => r.GroupName),
                StringComparer.OrdinalIgnoreCase);
            while (names.Contains("Group_" + index.ToString(CultureInfo.InvariantCulture))) index++;
            return "Group_" + index.ToString(CultureInfo.InvariantCulture);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                string name = BaramGeometryFactory.SanitizeGeometryName(_txtName.Text);
                if (_configuration.Castellation.RefinementVolumes.Any(entry => entry.Key != _groupId &&
                    string.Equals(entry.Value.GroupName, name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("A volume refinement group named " + name + " already exists.");
                List<int> ids = new List<int>();
                foreach (object item in _volumes.CheckedItems) ids.Add(((GeometryItem)item).Id);
                if (ids.Count == 0) throw new InvalidOperationException("Select at least one volume.");
                if (_chkGap.Checked && _numGapStart.Value >= _numGapMax.Value)
                    throw new InvalidOperationException("Maximum gap level must be greater than detection start level.");
                if (_chkIncrement.Checked && _numIncrementMin.Value > _numIncrementMax.Value)
                    throw new InvalidOperationException("Directional increment minimum level cannot exceed maximum level.");
                //
                Refinement = new BaramVolumeRefinement
                {
                    GroupName = name,
                    VolumeRefinementLevel = Decimal.ToInt32(_numLevel.Value),
                    GapMinCellLayers = Decimal.ToInt32(_numGapMinLayers.Value),
                    GapDetectionStartLevel = Decimal.ToInt32(_numGapStart.Value),
                    GapMaxRefinementLevel = Decimal.ToInt32(_numGapMax.Value),
                    GapDirection = _chkGap.Checked ? (BaramGapRefinementMode)_cmbGapDirection.SelectedItem
                                                   : BaramGapRefinementMode.None,
                    GapSelf = _chkGapSelf.Checked,
                    LevelIncrementDisabled = !_chkIncrement.Checked,
                    MinLevel = Decimal.ToInt32(_numIncrementMin.Value),
                    MaxLevel = Decimal.ToInt32(_numIncrementMax.Value),
                    SplitCountX = Decimal.ToInt32(_numSplitX.Value),
                    SplitCountY = Decimal.ToInt32(_numSplitY.Value),
                    SplitCountZ = Decimal.ToInt32(_numSplitZ.Value)
                };
                Refinement.Validate();
                GeometryIds = ids.ToArray();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void level_ValueChanged(object sender, EventArgs e) { UpdateCellSize(); }
        private void option_CheckedChanged(object sender, EventArgs e) { UpdateEnabledState(); }
        private void UpdateCellSize()
        {
            _labCellSize.Text = BaramMeshRefinementDialogTools.FormatCellSize(_baseCellSize, (int)_numLevel.Value);
        }
        private void UpdateEnabledState()
        {
            _numGapMinLayers.Enabled = _chkGap.Checked;
            _numGapStart.Enabled = _chkGap.Checked;
            _numGapMax.Enabled = _chkGap.Checked;
            _cmbGapDirection.Enabled = _chkGap.Checked;
            _chkGapSelf.Enabled = _chkGap.Checked;
            _numIncrementMin.Enabled = _chkIncrement.Checked;
            _numIncrementMax.Enabled = _chkIncrement.Checked;
            _numSplitX.Enabled = _chkIncrement.Checked;
            _numSplitY.Enabled = _chkIncrement.Checked;
            _numSplitZ.Enabled = _chkIncrement.Checked;
        }
        private static NumericUpDown CreateIntegerInput(int min, int max)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, Width = 70 };
        }
        private class GeometryItem
        {
            public int Id { get; private set; }
            private readonly string _name;
            public GeometryItem(int id, string name) { Id = id; _name = name; }
            public override string ToString() { return _name; }
        }
    }

    internal static class BaramMeshRefinementDialogTools
    {
        public static bool IsBaseGridGeometry(int id, BaramGeometry geometry, BaramMeshConfiguration configuration)
        {
            if (geometry == null || configuration == null || configuration.BaseGrid == null ||
                !configuration.BaseGrid.BoundingHex6.HasValue) return false;
            int boundingId = configuration.BaseGrid.BoundingHex6.Value;
            return id == boundingId || (geometry.VolumeId.HasValue && geometry.VolumeId.Value == boundingId);
        }

        public static double[] GetBaseCellSize(BaramMeshConfiguration configuration)
        {
            if (configuration == null || configuration.BaseGrid == null ||
                !configuration.BaseGrid.BoundingHex6.HasValue) return null;
            BaramGeometry bounds;
            if (!configuration.Geometry.TryGetValue(configuration.BaseGrid.BoundingHex6.Value, out bounds)) return null;
            if (configuration.BaseGrid.NumCellsX <= 0 || configuration.BaseGrid.NumCellsY <= 0 ||
                configuration.BaseGrid.NumCellsZ <= 0) return null;
            return new double[]
            {
                Math.Abs(bounds.Point2.X - bounds.Point1.X) / configuration.BaseGrid.NumCellsX,
                Math.Abs(bounds.Point2.Y - bounds.Point1.Y) / configuration.BaseGrid.NumCellsY,
                Math.Abs(bounds.Point2.Z - bounds.Point1.Z) / configuration.BaseGrid.NumCellsZ
            };
        }

        public static string FormatCellSize(double[] baseCellSize, int level)
        {
            if (baseCellSize == null) return "Base Grid is not defined";
            double divisor = Math.Pow(2, level);
            return "Cell size: " + Format(baseCellSize[0] / divisor) + " x " +
                   Format(baseCellSize[1] / divisor) + " x " + Format(baseCellSize[2] / divisor);
        }

        private static string Format(double value)
        {
            return value.ToString("G6", CultureInfo.InvariantCulture);
        }
    }
}
