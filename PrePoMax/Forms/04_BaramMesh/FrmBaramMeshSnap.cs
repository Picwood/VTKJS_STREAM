// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeGlobals;
using CaeMesh.Meshing.BaramMesh;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramMeshSnap : Form
    {
        private readonly NumericUpDown _numSmoothPatch;
        private readonly NumericUpDown _numSmoothInternal;
        private readonly NumericUpDown _numSolveIter;
        private readonly NumericUpDown _numRelaxIter;
        private readonly NumericUpDown _numFeatureSnapIter;
        private readonly ComboBox _cmbFeatureSnapType;
        private readonly CheckBox _chkMultiRegionFeatureSnap;
        private readonly NumericUpDown _numTolerance;
        private readonly NumericUpDown _numConcaveAngle;
        private readonly NumericUpDown _numMinAreaRatio;
        private readonly CheckBox _chkBufferLayer;
        private readonly ComboBox _cmbPointSmoothingMethod;
        private readonly NumericUpDown _numPointSmoothingIter;
        private readonly NumericUpDown _numGetMeParameter;
        private readonly CheckedListBox _surfaces;
        private readonly Button _btnDefaults;
        private readonly Button _btnRun;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private readonly HashSet<int> _mandatorySurfaceIds;
        private BaramMeshConfiguration _workingConfiguration;
        private bool _loading;
        private bool _runOpenFoam;

        public BaramMeshConfiguration Configuration { get; private set; }
        public bool RunOpenFoam { get { return _runOpenFoam; } }

        public FrmBaramMeshSnap(BaramMeshConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            _workingConfiguration = configuration.DeepClone();
            _mandatorySurfaceIds = new HashSet<int>();
            //
            _numSmoothPatch = CreateIntegerInput(0, 1000000);
            _numSmoothInternal = CreateIntegerInput(0, 1000000);
            _numSolveIter = CreateIntegerInput(0, 1000000);
            _numRelaxIter = CreateIntegerInput(0, 1000000);
            _numFeatureSnapIter = CreateIntegerInput(0, 1000000);
            _cmbFeatureSnapType = CreateDropDown();
            _cmbFeatureSnapType.Items.Add(new EnumItem<BaramFeatureSnapType>("Explicit",
                                                                            BaramFeatureSnapType.Explicit));
            _cmbFeatureSnapType.Items.Add(new EnumItem<BaramFeatureSnapType>("Implicit",
                                                                            BaramFeatureSnapType.Implicit));
            _chkMultiRegionFeatureSnap = new CheckBox { Text = "Multi-surface feature snap", AutoSize = true };
            _numTolerance = CreateDecimalInput(0.000001M, 1000000, 0.1M);
            _numConcaveAngle = CreateDecimalInput(0, 180, 1);
            _numMinAreaRatio = CreateDecimalInput(0, 1, 0.05M);
            _chkBufferLayer = new CheckBox { Text = "Enable buffer layer snapping", AutoSize = true };
            _cmbPointSmoothingMethod = CreateDropDown();
            _cmbPointSmoothingMethod.Items.Add(
                new EnumItem<BaramBufferLayerPointSmoothingMethod>(
                    "Laplacian", BaramBufferLayerPointSmoothingMethod.Laplacian));
            _cmbPointSmoothingMethod.Items.Add(
                new EnumItem<BaramBufferLayerPointSmoothingMethod>(
                    "GETMe", BaramBufferLayerPointSmoothingMethod.GeometricElementTransform));
            _numPointSmoothingIter = CreateIntegerInput(10, 100);
            _numGetMeParameter = CreateDecimalInput(0.000001M, 1, 0.01M);
            _surfaces = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
            _btnDefaults = new Button();
            _btnRun = new Button();
            _btnOk = new Button();
            _btnCancel = new Button();
            //
            Text = "BaramMesh Snap";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 590);
            Size = new Size(860, 680);
            InitializeComponent();
            LoadSurfaces();
            LoadSettings(_workingConfiguration.Snap);
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);
            //
            FlowLayoutPanel tools = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            ConfigureButton(_btnDefaults, "Defaults", 85, btnDefaults_Click);
            tools.Controls.Add(_btnDefaults);
            root.Controls.Add(tools, 0, 0);
            //
            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(CreateSnapControlsTab());
            tabs.TabPages.Add(CreateBufferLayerTab());
            root.Controls.Add(tabs, 0, 1);
            //
            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            ConfigureButton(_btnCancel, "Cancel", 80, btnCancel_Click);
            ConfigureButton(_btnRun, "Run", 80, btnRun_Click);
            ConfigureButton(_btnOk, "OK", 80, btnOk_Click);
            footer.Controls.AddRange(new Control[] { _btnCancel, _btnRun, _btnOk });
            root.Controls.Add(footer, 0, 2);
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
            //
            _cmbFeatureSnapType.SelectedIndexChanged += option_Changed;
            _chkBufferLayer.CheckedChanged += option_Changed;
            _cmbPointSmoothingMethod.SelectedIndexChanged += option_Changed;
            _surfaces.ItemCheck += surfaces_ItemCheck;
        }

        private TabPage CreateSnapControlsTab()
        {
            TabPage tab = new TabPage("Snap Controls");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
            tab.Controls.Add(layout);
            //
            GroupBox smoothing = CreateGroup("Smoothing");
            TableLayoutPanel smoothingFields = CreateFieldPanel(4);
            AddField(smoothingFields, 0, "Smoothing for surface", _numSmoothPatch);
            AddField(smoothingFields, 1, "Smoothing for internal", _numSmoothInternal);
            AddField(smoothingFields, 2, "Mesh displacement relaxation", _numSolveIter);
            AddField(smoothingFields, 3, "Global snapping relaxation", _numRelaxIter);
            smoothing.Controls.Add(smoothingFields);
            layout.Controls.Add(smoothing, 0, 0);
            //
            GroupBox feature = CreateGroup("Feature Snapping");
            TableLayoutPanel featureFields = CreateFieldPanel(3);
            AddField(featureFields, 0, "Feature snapping relaxation", _numFeatureSnapIter);
            AddField(featureFields, 1, "Feature snap type", _cmbFeatureSnapType);
            featureFields.Controls.Add(_chkMultiRegionFeatureSnap, 1, 2);
            feature.Controls.Add(featureFields);
            layout.Controls.Add(feature, 0, 1);
            //
            GroupBox geometry = CreateGroup("Geometry Detection");
            TableLayoutPanel geometryFields = CreateFieldPanel(3);
            AddField(geometryFields, 0, "Tolerance", _numTolerance);
            AddField(geometryFields, 1, "Concave angle (degrees)", _numConcaveAngle);
            AddField(geometryFields, 2, "Minimum area ratio", _numMinAreaRatio);
            geometry.Controls.Add(geometryFields);
            layout.Controls.Add(geometry, 0, 2);
            return tab;
        }

        private TabPage CreateBufferLayerTab()
        {
            TabPage tab = new TabPage("Buffer Layer");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tab.Controls.Add(layout);
            layout.Controls.Add(_chkBufferLayer, 0, 0);
            //
            GroupBox controls = CreateGroup("Point Smoothing");
            TableLayoutPanel fields = CreateFieldPanel(3);
            AddField(fields, 0, "Smoothing method", _cmbPointSmoothingMethod);
            AddField(fields, 1, "Point smoother iterations", _numPointSmoothingIter);
            AddField(fields, 2, "GETMe transformation parameter", _numGetMeParameter);
            controls.Controls.Add(fields);
            layout.Controls.Add(controls, 0, 1);
            //
            GroupBox selected = CreateGroup("Buffer Layer Surfaces");
            selected.Controls.Add(_surfaces);
            layout.Controls.Add(selected, 0, 2);
            return tab;
        }

        private void LoadSurfaces()
        {
            _loading = true;
            try
            {
                _surfaces.Items.Clear();
                _mandatorySurfaceIds.Clear();
                foreach (KeyValuePair<int, BaramGeometry> entry in
                         _workingConfiguration.Geometry.OrderBy(e => e.Key))
                {
                    BaramGeometry geometry = entry.Value;
                    if (geometry == null || geometry.GeometryType != BaramGeometryType.Surface) continue;
                    if (BaramMeshRefinementDialogTools.IsBaseGridGeometry(entry.Key, geometry,
                                                                         _workingConfiguration)) continue;
                    bool mandatory = geometry.CfdType == BaramCfdType.Interface;
                    if (mandatory) _mandatorySurfaceIds.Add(entry.Key);
                    int index = _surfaces.Items.Add(new SurfaceItem(entry.Key, geometry.Name, mandatory));
                    if (geometry.AddBufferLayers || mandatory) _surfaces.SetItemChecked(index, true);
                }
            }
            finally
            {
                _loading = false;
            }
        }

        private void LoadSettings(BaramSnapSettings settings)
        {
            _loading = true;
            try
            {
                _numSmoothPatch.Value = Clamp(settings.NSmoothPatch, _numSmoothPatch);
                _numSmoothInternal.Value = Clamp(settings.NSmoothInternal, _numSmoothInternal);
                _numSolveIter.Value = Clamp(settings.NSolveIter, _numSolveIter);
                _numRelaxIter.Value = Clamp(settings.NRelaxIter, _numRelaxIter);
                _numFeatureSnapIter.Value = Clamp(settings.NFeatureSnapIter, _numFeatureSnapIter);
                SelectEnum(_cmbFeatureSnapType, settings.FeatureSnapType);
                _chkMultiRegionFeatureSnap.Checked = settings.MultiRegionFeatureSnap;
                _numTolerance.Value = Clamp(settings.Tolerance, _numTolerance);
                _numConcaveAngle.Value = Clamp(settings.ConcaveAngle, _numConcaveAngle);
                _numMinAreaRatio.Value = Clamp(settings.MinAreaRatio, _numMinAreaRatio);
                _chkBufferLayer.Checked = !settings.BufferLayer.Disabled;
                SelectEnum(_cmbPointSmoothingMethod, settings.BufferLayer.PointSmoothingMethod);
                _numPointSmoothingIter.Value = Clamp(settings.BufferLayer.NumberOfPointSmoothingIteration,
                                                     _numPointSmoothingIter);
                _numGetMeParameter.Value = Clamp(settings.BufferLayer.GetMeTransformationParameter,
                                                 _numGetMeParameter);
            }
            finally
            {
                _loading = false;
            }
            UpdateEnabledState();
        }

        private void btnDefaults_Click(object sender, EventArgs e)
        {
            LoadSettings(new BaramSnapSettings());
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
                BaramMeshConfiguration next = _workingConfiguration.DeepClone();
                BaramSnapSettings snap = next.Snap;
                snap.NSmoothPatch = Decimal.ToInt32(_numSmoothPatch.Value);
                snap.NSmoothInternal = Decimal.ToInt32(_numSmoothInternal.Value);
                snap.NSolveIter = Decimal.ToInt32(_numSolveIter.Value);
                snap.NRelaxIter = Decimal.ToInt32(_numRelaxIter.Value);
                snap.NFeatureSnapIter = Decimal.ToInt32(_numFeatureSnapIter.Value);
                snap.FeatureSnapType = SelectedEnum<BaramFeatureSnapType>(_cmbFeatureSnapType);
                snap.MultiRegionFeatureSnap = _chkMultiRegionFeatureSnap.Checked;
                snap.Tolerance = Decimal.ToDouble(_numTolerance.Value);
                snap.ConcaveAngle = Decimal.ToDouble(_numConcaveAngle.Value);
                snap.MinAreaRatio = Decimal.ToDouble(_numMinAreaRatio.Value);
                snap.BufferLayer.Disabled = !_chkBufferLayer.Checked;
                snap.BufferLayer.PointSmoothingMethod =
                    SelectedEnum<BaramBufferLayerPointSmoothingMethod>(_cmbPointSmoothingMethod);
                snap.BufferLayer.NumberOfPointSmoothingIteration =
                    Decimal.ToInt32(_numPointSmoothingIter.Value);
                snap.BufferLayer.GetMeTransformationParameter = Decimal.ToDouble(_numGetMeParameter.Value);
                //
                if (_chkBufferLayer.Checked)
                {
                    HashSet<int> selected = new HashSet<int>();
                    foreach (object item in _surfaces.CheckedItems) selected.Add(((SurfaceItem)item).Id);
                    selected.UnionWith(_mandatorySurfaceIds);
                    if (selected.Count == 0)
                        throw new InvalidOperationException("Select at least one surface for the buffer layer.");
                    foreach (KeyValuePair<int, BaramGeometry> entry in next.Geometry)
                    {
                        if (entry.Value != null && entry.Value.GeometryType == BaramGeometryType.Surface)
                            entry.Value.AddBufferLayers = selected.Contains(entry.Key);
                    }
                }
                next.Step = BaramStep.Snap;
                next.Validate();
                Configuration = next;
                _runOpenFoam = runOpenFoam;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void option_Changed(object sender, EventArgs e)
        {
            if (!_loading) UpdateEnabledState();
        }

        private void surfaces_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_loading || e.Index < 0 || e.Index >= _surfaces.Items.Count) return;
            SurfaceItem item = (SurfaceItem)_surfaces.Items[e.Index];
            if (item.Mandatory && e.NewValue != CheckState.Checked) e.NewValue = CheckState.Checked;
        }

        private void UpdateEnabledState()
        {
            BaramFeatureSnapType featureType = SelectedEnum<BaramFeatureSnapType>(_cmbFeatureSnapType);
            _chkMultiRegionFeatureSnap.Enabled = featureType == BaramFeatureSnapType.Explicit;
            bool buffer = _chkBufferLayer.Checked;
            _cmbPointSmoothingMethod.Enabled = buffer;
            _numPointSmoothingIter.Enabled = buffer;
            _surfaces.Enabled = buffer;
            BaramBufferLayerPointSmoothingMethod method =
                SelectedEnum<BaramBufferLayerPointSmoothingMethod>(_cmbPointSmoothingMethod);
            _numGetMeParameter.Enabled = buffer &&
                                         method == BaramBufferLayerPointSmoothingMethod.GeometricElementTransform;
        }

        private static GroupBox CreateGroup(string text)
        {
            return new GroupBox { Text = text, Dock = DockStyle.Fill };
        }

        private static TableLayoutPanel CreateFieldPanel(int rows)
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = rows,
                Padding = new Padding(8)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return panel;
        }

        private static void AddField(TableLayoutPanel panel, int row, string text, Control control)
        {
            panel.Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            panel.Controls.Add(control, 1, row);
        }

        private static NumericUpDown CreateIntegerInput(decimal minimum, decimal maximum)
        {
            return new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = 0,
                Increment = 1,
                Dock = DockStyle.Fill
            };
        }

        private static NumericUpDown CreateDecimalInput(decimal minimum, decimal maximum, decimal increment)
        {
            return new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = 6,
                Increment = increment,
                Dock = DockStyle.Fill
            };
        }

        private static ComboBox CreateDropDown()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        }

        private static void ConfigureButton(Button button, string text, int width, EventHandler click)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 24;
            button.Click += click;
        }

        private static decimal Clamp(double value, NumericUpDown input)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return input.Minimum;
            decimal result = (decimal)value;
            return Math.Min(input.Maximum, Math.Max(input.Minimum, result));
        }

        private static void SelectEnum<T>(ComboBox combo, T value) where T : struct
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                EnumItem<T> item = combo.Items[i] as EnumItem<T>;
                if (item != null && EqualityComparer<T>.Default.Equals(item.Value, value))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static T SelectedEnum<T>(ComboBox combo) where T : struct
        {
            EnumItem<T> item = combo.SelectedItem as EnumItem<T>;
            return item != null ? item.Value : default(T);
        }

        private class EnumItem<T> where T : struct
        {
            public T Value { get; private set; }
            private readonly string _text;
            public EnumItem(string text, T value) { _text = text; Value = value; }
            public override string ToString() { return _text; }
        }

        private class SurfaceItem
        {
            public int Id { get; private set; }
            public bool Mandatory { get; private set; }
            private readonly string _name;
            public SurfaceItem(int id, string name, bool mandatory)
            {
                Id = id;
                _name = name;
                Mandatory = mandatory;
            }
            public override string ToString()
            {
                return Mandatory ? _name + " (interface)" : _name;
            }
        }
    }
}
