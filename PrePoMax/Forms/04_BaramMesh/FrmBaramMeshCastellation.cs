// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeGlobals;
using CaeMesh.Meshing.BaramMesh;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    class FrmBaramMeshCastellation : Form
    {
        private const string ColId = "Id";
        private const string ColName = "Name";
        private const string ColMembers = "Members";
        private const string ColMinLevel = "MinLevel";
        private const string ColMaxLevel = "MaxLevel";
        private const string ColFeatureLevel = "FeatureLevel";
        private const string ColCurvatureEnabled = "CurvatureEnabled";
        private const string ColCurvatureCells = "CurvatureCells";
        private const string ColCurvatureMax = "CurvatureMax";
        private const string ColCurvatureExcludeSharp = "CurvatureExcludeSharp";
        private const string ColCurvatureMinRadius = "CurvatureMinRadius";
        private const string ColVolumeLevel = "VolumeLevel";
        private const string ColGapDirection = "GapDirection";
        private const string ColGapMinLayers = "GapMinLayers";
        private const string ColGapStartLevel = "GapStartLevel";
        private const string ColGapMaxLevel = "GapMaxLevel";
        private const string ColGapSelf = "GapSelf";
        private const string ColLevelIncrementEnabled = "LevelIncrementEnabled";
        private const string ColLevelIncrementMin = "LevelIncrementMin";
        private const string ColLevelIncrementMax = "LevelIncrementMax";
        private const string ColSplitX = "SplitX";
        private const string ColSplitY = "SplitY";
        private const string ColSplitZ = "SplitZ";

        private readonly NumericUpDown _numResolveFeatureAngle;
        private readonly NumericUpDown _numCellsBetweenLevels;
        private readonly NumericUpDown _numMaxLocalCells;
        private readonly NumericUpDown _numMaxGlobalCells;
        private readonly NumericUpDown _numMinRefinementCells;
        private readonly NumericUpDown _numMaxLoadUnbalance;
        private readonly CheckBox _chkAllowFreeStandingZoneFaces;
        private readonly TabControl _tabs;
        private readonly DataGridView _surfaceGrid;
        private readonly DataGridView _volumeGrid;
        private readonly Button _btnEnableAll;
        private readonly Button _btnDisableAll;
        private readonly Button _btnRemove;
        private readonly Button _btnRun;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private BaramMeshConfiguration _configuration;
        private BaramMeshConfiguration _workingConfiguration;
        private bool _runOpenFoam;

        public BaramMeshConfiguration Configuration { get { return _configuration; } }
        public bool RunOpenFoam { get { return _runOpenFoam; } }

        public FrmBaramMeshCastellation(BaramMeshConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            _configuration = configuration;
            _workingConfiguration = configuration.DeepClone();

            _numResolveFeatureAngle = CreateDecimalInput(0, 180, 1);
            _numCellsBetweenLevels = CreateIntegerInput(1, 1000000);
            _numMaxLocalCells = CreateIntegerInput(1, 2000000000);
            _numMaxGlobalCells = CreateIntegerInput(1, 2000000000);
            _numMinRefinementCells = CreateIntegerInput(0, 2000000000);
            _numMaxLoadUnbalance = CreateDecimalInput(0, 1, 0.1M);
            _chkAllowFreeStandingZoneFaces = new CheckBox();
            _tabs = new TabControl();
            _surfaceGrid = new DataGridView();
            _volumeGrid = new DataGridView();
            _btnEnableAll = new Button();
            _btnDisableAll = new Button();
            _btnRemove = new Button();
            _btnRun = new Button();
            _btnOk = new Button();
            _btnCancel = new Button();

            Text = "BaramMesh Castellation";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 560);
            Size = new Size(1040, 620);

            InitializeComponent();
            LoadValues();
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.Padding = new Padding(10);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            GroupBox global = new GroupBox();
            global.Text = "Castellation Controls";
            global.Dock = DockStyle.Fill;
            root.Controls.Add(global, 0, 0);

            TableLayoutPanel fields = new TableLayoutPanel();
            fields.Dock = DockStyle.Fill;
            fields.ColumnCount = 4;
            fields.RowCount = 4;
            fields.Padding = new Padding(8);
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            global.Controls.Add(fields);

            AddField(fields, 0, 0, "Feature angle", _numResolveFeatureAngle);
            AddField(fields, 0, 2, "Cells between levels", _numCellsBetweenLevels);
            AddField(fields, 1, 0, "Max local cells", _numMaxLocalCells);
            AddField(fields, 1, 2, "Max global cells", _numMaxGlobalCells);
            AddField(fields, 2, 0, "Min refinement cells", _numMinRefinementCells);
            AddField(fields, 2, 2, "Max load unbalance", _numMaxLoadUnbalance);
            AddLabel(fields, 3, 0, "Free-standing zone faces");
            _chkAllowFreeStandingZoneFaces.Dock = DockStyle.Left;
            fields.Controls.Add(_chkAllowFreeStandingZoneFaces, 1, 3);

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Fill;
            tools.FlowDirection = FlowDirection.LeftToRight;
            root.Controls.Add(tools, 0, 1);
            ConfigureButton(_btnEnableAll, "Add", 80, btnAdd_Click);
            ConfigureButton(_btnDisableAll, "Edit", 80, btnEdit_Click);
            ConfigureButton(_btnRemove, "Remove", 80, btnRemove_Click);
            tools.Controls.AddRange(new Control[] { _btnEnableAll, _btnDisableAll, _btnRemove });

            _tabs.Dock = DockStyle.Fill;
            _tabs.TabPages.Add(CreateGridTab("Surface Refinement", _surfaceGrid, AddSurfaceColumns));
            _tabs.TabPages.Add(CreateGridTab("Volume Refinement", _volumeGrid, AddVolumeColumns));
            root.Controls.Add(_tabs, 0, 2);

            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.Padding = new Padding(0, 8, 0, 0);
            root.Controls.Add(footer, 0, 3);
            ConfigureButton(_btnCancel, "Cancel", 75, btnCancel_Click);
            ConfigureButton(_btnOk, "OK", 75, btnOk_Click);
            ConfigureButton(_btnRun, "Run", 75, btnRun_Click);
            footer.Controls.AddRange(new Control[] { _btnCancel, _btnRun, _btnOk });

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private TabPage CreateGridTab(string text, DataGridView grid, Action<DataGridView> addColumns)
        {
            TabPage tab = new TabPage(text);
            ConfigureGrid(grid);
            addColumns(grid);
            tab.Controls.Add(grid);
            return tab;
        }

        private void ConfigureGrid(DataGridView grid)
        {
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.Dock = DockStyle.Fill;
            grid.MultiSelect = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.RowHeadersVisible = false;
            grid.ReadOnly = true;
            grid.CellDoubleClick += grid_CellDoubleClick;
            grid.DataError += grid_DataError;
        }

        private static NumericUpDown CreateIntegerInput(decimal minimum, decimal maximum)
        {
            NumericUpDown input = new NumericUpDown();
            input.DecimalPlaces = 0;
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.Increment = 1;
            input.Dock = DockStyle.Fill;
            return input;
        }

        private static NumericUpDown CreateDecimalInput(decimal minimum, decimal maximum, decimal increment)
        {
            NumericUpDown input = new NumericUpDown();
            input.DecimalPlaces = 3;
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.Increment = increment;
            input.Dock = DockStyle.Fill;
            return input;
        }

        private static void ConfigureButton(Button button, string text, int width, EventHandler click)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 24;
            button.Margin = new Padding(3, 3, 3, 0);
            button.Click += click;
        }

        private static void AddField(TableLayoutPanel panel, int row, int labelColumn, string text, Control control)
        {
            AddLabel(panel, row, labelColumn, text);
            panel.Controls.Add(control, labelColumn + 1, row);
        }

        private static void AddLabel(TableLayoutPanel panel, int row, int column, string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            panel.Controls.Add(label, column, row);
        }

        private void AddSurfaceColumns(DataGridView grid)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColId,
                HeaderText = "Id",
                Visible = false,
                ReadOnly = true
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColName,
                HeaderText = "Group",
                Width = 150,
                ReadOnly = true
            });
            grid.Columns.Add(CreateTextColumn(ColMembers, "Surfaces", 300));
            grid.Columns.Add(CreateLevelColumn(ColMinLevel, "Min", 70, 0));
            grid.Columns.Add(CreateLevelColumn(ColMaxLevel, "Max", 70, 1));
            grid.Columns.Add(CreateLevelColumn(ColFeatureLevel, "Feature", 80, 1));
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = ColCurvatureEnabled, HeaderText = "Curvature", Width = 75 });
            grid.Columns.Add(CreateIntegerTextColumn(ColCurvatureCells, "Curv cells", 80));
            grid.Columns.Add(CreateLevelColumn(ColCurvatureMax, "Curv max", 80, 0));
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = ColCurvatureExcludeSharp, HeaderText = "Exclude sharp", Width = 95 });
            grid.Columns.Add(CreateTextColumn(ColCurvatureMinRadius, "Min radius", 85));
        }

        private void AddVolumeColumns(DataGridView grid)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColId,
                HeaderText = "Id",
                Visible = false,
                ReadOnly = true
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColName,
                HeaderText = "Group",
                Width = 150,
                ReadOnly = true
            });
            grid.Columns.Add(CreateTextColumn(ColMembers, "Volumes", 300));
            grid.Columns.Add(CreateLevelColumn(ColVolumeLevel, "Level", 70, 0));
            grid.Columns.Add(CreateEnumColumn(ColGapDirection, "Gap", 80, typeof(BaramGapRefinementMode)));
            grid.Columns.Add(CreateIntegerTextColumn(ColGapMinLayers, "Min layers", 85));
            grid.Columns.Add(CreateLevelColumn(ColGapStartLevel, "Gap start", 80, 0));
            grid.Columns.Add(CreateLevelColumn(ColGapMaxLevel, "Gap max", 80, 1));
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = ColGapSelf, HeaderText = "Gap self", Width = 70 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = ColLevelIncrementEnabled, HeaderText = "Increment", Width = 80 });
            grid.Columns.Add(CreateLevelColumn(ColLevelIncrementMin, "Inc min", 70, 0));
            grid.Columns.Add(CreateLevelColumn(ColLevelIncrementMax, "Inc max", 70, 0));
            grid.Columns.Add(CreateIntegerTextColumn(ColSplitX, "Split X", 65));
            grid.Columns.Add(CreateIntegerTextColumn(ColSplitY, "Split Y", 65));
            grid.Columns.Add(CreateIntegerTextColumn(ColSplitZ, "Split Z", 65));
        }

        private static DataGridViewComboBoxColumn CreateLevelColumn(string name, string header, int width, int min)
        {
            DataGridViewComboBoxColumn column = new DataGridViewComboBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            for (int i = min; i <= 10; i++) column.Items.Add(i.ToString(CultureInfo.InvariantCulture));
            return column;
        }

        private static DataGridViewTextBoxColumn CreateIntegerTextColumn(string name, string header, int width)
        {
            return CreateTextColumn(name, header, width);
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string name, string header, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            return column;
        }

        private static DataGridViewComboBoxColumn CreateEnumColumn(string name, string header, int width, Type enumType)
        {
            DataGridViewComboBoxColumn column = new DataGridViewComboBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            foreach (object value in Enum.GetValues(enumType)) column.Items.Add(value.ToString());
            return column;
        }

        private void LoadValues()
        {
            BaramCastellationSettings settings = _workingConfiguration.Castellation;
            _numResolveFeatureAngle.Value = ClampDecimal(settings.ResolveFeatureAngle, _numResolveFeatureAngle.Minimum,
                                                         _numResolveFeatureAngle.Maximum);
            _numCellsBetweenLevels.Value = ClampDecimal(settings.NCellsBetweenLevels, _numCellsBetweenLevels.Minimum,
                                                        _numCellsBetweenLevels.Maximum);
            _numMaxLocalCells.Value = ClampDecimal(settings.MaxLocalCells, _numMaxLocalCells.Minimum,
                                                   _numMaxLocalCells.Maximum);
            _numMaxGlobalCells.Value = ClampDecimal(settings.MaxGlobalCells, _numMaxGlobalCells.Minimum,
                                                    _numMaxGlobalCells.Maximum);
            _numMinRefinementCells.Value = ClampDecimal(settings.MinRefinementCells, _numMinRefinementCells.Minimum,
                                                        _numMinRefinementCells.Maximum);
            _numMaxLoadUnbalance.Value = ClampDecimal(settings.MaxLoadUnbalance, _numMaxLoadUnbalance.Minimum,
                                                       _numMaxLoadUnbalance.Maximum);
            _chkAllowFreeStandingZoneFaces.Checked = settings.AllowFreeStandingZoneFaces;
            LoadRefinementRows();
        }

        private void LoadRefinementRows()
        {
            _surfaceGrid.Rows.Clear();
            foreach (KeyValuePair<int, BaramSurfaceRefinement> entry in
                     _workingConfiguration.Castellation.RefinementSurfaces.OrderBy(kvp => kvp.Key))
            {
                BaramSurfaceRefinement refinement = entry.Value;
                _surfaceGrid.Rows.Add(entry.Key,
                                      refinement.GroupName,
                                      GetGroupMemberNames(entry.Key, BaramGeometryType.Surface),
                                      refinement.MinimumLevel.ToString(CultureInfo.InvariantCulture),
                                      refinement.MaximumLevel.ToString(CultureInfo.InvariantCulture),
                                      refinement.FeatureEdgeRefinementLevel.ToString(CultureInfo.InvariantCulture),
                                      !refinement.CurvatureRefinementDisabled,
                                      refinement.CurvatureNumberOfCells.ToString(CultureInfo.InvariantCulture),
                                      refinement.CurvatureMaxLevel.ToString(CultureInfo.InvariantCulture),
                                      refinement.CurvatureExcludeSharpSurface,
                                      refinement.CurvatureMinRadius.ToString("G17", CultureInfo.InvariantCulture));
            }
            _volumeGrid.Rows.Clear();
            foreach (KeyValuePair<int, BaramVolumeRefinement> entry in
                     _workingConfiguration.Castellation.RefinementVolumes.OrderBy(kvp => kvp.Key))
            {
                BaramVolumeRefinement refinement = entry.Value;
                _volumeGrid.Rows.Add(entry.Key,
                                     refinement.GroupName,
                                     GetGroupMemberNames(entry.Key, BaramGeometryType.Volume),
                                     refinement.VolumeRefinementLevel.ToString(CultureInfo.InvariantCulture),
                                     refinement.GapDirection.ToString(),
                                     refinement.GapMinCellLayers.ToString(CultureInfo.InvariantCulture),
                                     refinement.GapDetectionStartLevel.ToString(CultureInfo.InvariantCulture),
                                     refinement.GapMaxRefinementLevel.ToString(CultureInfo.InvariantCulture),
                                     refinement.GapSelf,
                                     !refinement.LevelIncrementDisabled,
                                     refinement.MinLevel.ToString(CultureInfo.InvariantCulture),
                                     refinement.MaxLevel.ToString(CultureInfo.InvariantCulture),
                                     refinement.SplitCountX.ToString(CultureInfo.InvariantCulture),
                                     refinement.SplitCountY.ToString(CultureInfo.InvariantCulture),
                                     refinement.SplitCountZ.ToString(CultureInfo.InvariantCulture));
            }
        }

        private string GetGroupMemberNames(int groupId, BaramGeometryType geometryType)
        {
            return string.Join(", ", _workingConfiguration.Geometry
                                                           .Where(e => e.Value != null &&
                                                                       e.Value.GeometryType == geometryType &&
                                                                       e.Value.CastellationGroup == groupId)
                                                           .OrderBy(e => e.Key)
                                                           .Select(e => e.Value.Name));
        }

        private static decimal ClampDecimal(double value, decimal minimum, decimal maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return minimum;
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

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (GetActiveGrid() == _surfaceGrid) EditSurfaceRefinement(null);
            else EditVolumeRefinement(null);
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            int? groupId = GetSelectedGroupId();
            if (!groupId.HasValue) return;
            if (GetActiveGrid() == _surfaceGrid) EditSurfaceRefinement(groupId);
            else EditVolumeRefinement(groupId);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            int? groupId = GetSelectedGroupId();
            if (!groupId.HasValue) return;
            BaramGeometryType type = GetActiveGrid() == _surfaceGrid ? BaramGeometryType.Surface
                                                                     : BaramGeometryType.Volume;
            ClearGroupAssignments(groupId.Value, type);
            if (type == BaramGeometryType.Surface)
                _workingConfiguration.Castellation.RefinementSurfaces.Remove(groupId.Value);
            else
                _workingConfiguration.Castellation.RefinementVolumes.Remove(groupId.Value);
            LoadRefinementRows();
        }

        private void grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            btnEdit_Click(sender, EventArgs.Empty);
        }

        private int? GetSelectedGroupId()
        {
            DataGridView grid = GetActiveGrid();
            if (grid.CurrentRow == null || grid.CurrentRow.IsNewRow) return null;
            return ReadInt(grid.CurrentRow.Cells[ColId].Value, "Group id");
        }

        private void EditSurfaceRefinement(int? groupId)
        {
            using (FrmBaramMeshSurfaceRefinement form =
                   new FrmBaramMeshSurfaceRefinement(_workingConfiguration, groupId))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                int id = groupId ?? GetNextGroupId(_workingConfiguration.Castellation.RefinementSurfaces.Keys);
                ClearGroupAssignments(id, BaramGeometryType.Surface);
                _workingConfiguration.Castellation.RefinementSurfaces[id] = form.Refinement;
                foreach (int geometryId in form.GeometryIds)
                    _workingConfiguration.Geometry[geometryId].CastellationGroup = id;
                LoadRefinementRows();
                SelectGroupRow(_surfaceGrid, id);
            }
        }

        private void EditVolumeRefinement(int? groupId)
        {
            using (FrmBaramMeshVolumeRefinement form =
                   new FrmBaramMeshVolumeRefinement(_workingConfiguration, groupId))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                int id = groupId ?? GetNextGroupId(_workingConfiguration.Castellation.RefinementVolumes.Keys);
                ClearGroupAssignments(id, BaramGeometryType.Volume);
                _workingConfiguration.Castellation.RefinementVolumes[id] = form.Refinement;
                foreach (int geometryId in form.GeometryIds)
                    _workingConfiguration.Geometry[geometryId].CastellationGroup = id;
                LoadRefinementRows();
                SelectGroupRow(_volumeGrid, id);
            }
        }

        private void ClearGroupAssignments(int groupId, BaramGeometryType type)
        {
            foreach (BaramGeometry geometry in _workingConfiguration.Geometry.Values)
            {
                if (geometry != null && geometry.GeometryType == type && geometry.CastellationGroup == groupId)
                    geometry.CastellationGroup = null;
            }
        }

        private static int GetNextGroupId(IEnumerable<int> ids)
        {
            int next = 1;
            foreach (int id in ids) if (id >= next) next = id + 1;
            return next;
        }

        private static void SelectGroupRow(DataGridView grid, int groupId)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                bool selected = Convert.ToInt32(row.Cells[ColId].Value, CultureInfo.InvariantCulture) == groupId;
                row.Selected = selected;
                if (selected) grid.CurrentCell = row.Cells[ColName];
            }
        }

        private DataGridView GetActiveGrid()
        {
            if (_tabs.SelectedTab != null && _tabs.SelectedTab.Controls.Contains(_volumeGrid)) return _volumeGrid;
            return _surfaceGrid;
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
                ReadToConfiguration();
                _runOpenFoam = runOpenFoam;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramMesh castellation", MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        private void ReadToConfiguration()
        {
            BaramMeshConfiguration next = _workingConfiguration.DeepClone();
            next.Step = BaramStep.Castellation;
            next.Castellation.ResolveFeatureAngle = Decimal.ToDouble(_numResolveFeatureAngle.Value);
            next.Castellation.NCellsBetweenLevels = Decimal.ToInt32(_numCellsBetweenLevels.Value);
            next.Castellation.MaxLocalCells = Decimal.ToInt32(_numMaxLocalCells.Value);
            next.Castellation.MaxGlobalCells = Decimal.ToInt32(_numMaxGlobalCells.Value);
            next.Castellation.MinRefinementCells = Decimal.ToInt32(_numMinRefinementCells.Value);
            next.Castellation.MaxLoadUnbalance = Decimal.ToDouble(_numMaxLoadUnbalance.Value);
            next.Castellation.AllowFreeStandingZoneFaces = _chkAllowFreeStandingZoneFaces.Checked;
            next.Validate();
            _configuration = next;
        }

        private static int ReadInt(object value, string label)
        {
            int result;
            if (value == null || !int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                throw new InvalidOperationException(label + " is not a valid integer.");
            return result;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }
    }
}
