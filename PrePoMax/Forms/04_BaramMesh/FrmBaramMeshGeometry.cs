// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    class FrmBaramMeshGeometry : Form
    {
        private const string ColId = "Id";
        private const string ColName = "Name";
        private const string ColGeometryType = "GeometryType";
        private const string ColShape = "Shape";
        private const string ColCfdType = "CfdType";
        private const string ColVolumeId = "VolumeId";
        private const string ColPath = "Path";
        private const string ColP1X = "P1X";
        private const string ColP1Y = "P1Y";
        private const string ColP1Z = "P1Z";
        private const string ColP2X = "P2X";
        private const string ColP2Y = "P2Y";
        private const string ColP2Z = "P2Z";
        private const string ColNormalX = "NormalX";
        private const string ColNormalY = "NormalY";
        private const string ColNormalZ = "NormalZ";
        private const string ColWidth = "Width";
        private const string ColHeight = "Height";
        private const string ColRadius = "Radius";
        private const string ColNonConformal = "NonConformal";
        private const string ColInterRegion = "InterRegion";

        private readonly DataGridView _grid;
        private readonly Button _btnAddModel;
        private readonly Button _btnAddModelParts;
        private readonly Button _btnImportStl;
        private readonly Button _btnSplitStl;
        private readonly Button _btnAssignSurface;
        private readonly Button _btnAssignRemaining;
        private readonly Button _btnAddHex6;
        private readonly Button _btnAddPlane;
        private readonly Button _btnAddBox;
        private readonly Button _btnAddSphere;
        private readonly Button _btnAddCylinder;
        private readonly Button _btnEdit;
        private readonly Button _btnRemove;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private readonly BaramBounds _modelBounds;
        private readonly string[] _modelPartNames;
        private readonly string _projectDirectory;
        private BaramMeshConfiguration _configuration;

        public BaramMeshConfiguration Configuration { get { return _configuration; } }

        public FrmBaramMeshGeometry(BaramMeshConfiguration configuration, BaramBounds modelBounds, bool seedModelIfEmpty,
                                    string[] modelPartNames = null, string projectDirectory = null)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");

            _configuration = configuration;
            _modelBounds = modelBounds;
            _modelPartNames = modelPartNames ?? new string[0];
            _projectDirectory = projectDirectory;

            Text = "BaramMesh Geometry";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(980, 520);
            Size = new Size(1180, 620);

            _grid = new DataGridView();
            _btnAddModel = new Button();
            _btnAddModelParts = new Button();
            _btnImportStl = new Button();
            _btnSplitStl = new Button();
            _btnAssignSurface = new Button();
            _btnAssignRemaining = new Button();
            _btnAddHex6 = new Button();
            _btnAddPlane = new Button();
            _btnAddBox = new Button();
            _btnAddSphere = new Button();
            _btnAddCylinder = new Button();
            _btnEdit = new Button();
            _btnRemove = new Button();
            _btnOk = new Button();
            _btnCancel = new Button();

            InitializeComponent();

            LoadGrid();
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 3;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.Padding = new Padding(6, 4, 6, 2);
            toolbar.WrapContents = false;
            root.Controls.Add(toolbar, 0, 0);

            ConfigureButton(_btnAddModel, "Add PrePoMax model", 130, btnAddModel_Click);
            ConfigureButton(_btnAddModelParts, "Add PrePoMax part", 130, btnAddModelParts_Click);
            ConfigureButton(_btnImportStl, "Import STL", 80, btnImportStl_Click);
            ConfigureButton(_btnSplitStl, "Split STL", 75, btnSplitStl_Click);
            ConfigureButton(_btnAssignSurface, "Assign Surface", 105, btnAssignSurface_Click);
            ConfigureButton(_btnAssignRemaining, "Assign Remaining", 120, btnAssignRemaining_Click);
            ConfigureButton(_btnAddHex6, "Add Hex6 bounds", 110, btnAddHex6_Click);
            ConfigureButton(_btnAddPlane, "Add Plane", 80, btnAddPlane_Click);
            ConfigureButton(_btnAddBox, "Add Box", 75, btnAddBox_Click);
            ConfigureButton(_btnAddSphere, "Add Sphere", 85, btnAddSphere_Click);
            ConfigureButton(_btnAddCylinder, "Add Cylinder", 90, btnAddCylinder_Click);
            ConfigureButton(_btnEdit, "Edit", 60, btnEdit_Click);
            ConfigureButton(_btnRemove, "Remove", 75, btnRemove_Click);
            _btnAddModelParts.Enabled = true;
            toolbar.Controls.AddRange(new Control[] { _btnAddModelParts, _btnImportStl, _btnAddBox, _btnAddSphere,
                                                      _btnAddCylinder, _btnEdit, _btnRemove });

            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.Dock = DockStyle.Fill;
            _grid.MultiSelect = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.RowHeadersVisible = false;
            _grid.ReadOnly = true;
            _grid.EditMode = DataGridViewEditMode.EditProgrammatically;
            _grid.DataError += grid_DataError;
            _grid.CellDoubleClick += grid_CellDoubleClick;
            AddColumns();
            root.Controls.Add(_grid, 0, 1);

            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.Padding = new Padding(6, 6, 6, 6);
            root.Controls.Add(footer, 0, 2);

            ConfigureButton(_btnCancel, "Cancel", 75, btnCancel_Click);
            ConfigureButton(_btnOk, "OK", 75, btnOk_Click);
            footer.Controls.AddRange(new Control[] { _btnCancel, _btnOk });

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private static void ConfigureButton(Button button, string text, int width, EventHandler click)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 24;
            button.Margin = new Padding(3, 0, 3, 0);
            button.Click += click;
        }

        private void AddColumns()
        {
            AddTextColumn(ColId, "Id", 45, true);
            AddTextColumn(ColName, "Name", 150, false);
            AddComboColumn(ColGeometryType, "Type", 75, typeof(BaramGeometryType));
            AddComboColumn(ColShape, "Shape", 105, typeof(BaramShape));
            AddComboColumn(ColCfdType, "CFD", 80, typeof(BaramCfdType));
            AddTextColumn(ColVolumeId, "Volume", 55, false);
            AddTextColumn(ColPath, "Path", 125, false);
            AddTextColumn(ColP1X, "P1 X", 70, false);
            AddTextColumn(ColP1Y, "P1 Y", 70, false);
            AddTextColumn(ColP1Z, "P1 Z", 70, false);
            AddTextColumn(ColP2X, "P2 X", 70, false);
            AddTextColumn(ColP2Y, "P2 Y", 70, false);
            AddTextColumn(ColP2Z, "P2 Z", 70, false);
            AddTextColumn(ColNormalX, "N X", 60, false);
            AddTextColumn(ColNormalY, "N Y", 60, false);
            AddTextColumn(ColNormalZ, "N Z", 60, false);
            AddTextColumn(ColWidth, "Width", 65, false);
            AddTextColumn(ColHeight, "Height", 65, false);
            AddTextColumn(ColRadius, "Radius", 65, false);
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = ColNonConformal, HeaderText = "Non-conformal", Width = 95 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = ColInterRegion, HeaderText = "Inter-region", Width = 80 });
        }

        private void AddTextColumn(string name, string header, int width, bool readOnly)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                ReadOnly = readOnly
            });
        }

        private void AddComboColumn(string name, string header, int width, Type enumType)
        {
            DataGridViewComboBoxColumn column = new DataGridViewComboBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.Width = width;
            column.Items.AddRange(Enum.GetNames(enumType));
            _grid.Columns.Add(column);
        }

        private void LoadGrid()
        {
            _grid.Rows.Clear();
            foreach (KeyValuePair<int, BaramGeometry> entry in _configuration.Geometry)
            {
                if (IsBaseGridGeometry(entry.Key, entry.Value, _configuration)) continue;
                AddRow(entry.Key, entry.Value);
            }
        }

        private void AddRow(int id, BaramGeometry geometry)
        {
            _grid.Rows.Add(id,
                           geometry.Name,
                           geometry.GeometryType.ToString(),
                           geometry.Shape.ToString(),
                           geometry.CfdType.ToString(),
                           geometry.VolumeId.HasValue ? geometry.VolumeId.Value.ToString() : "",
                           geometry.Path,
                           FormatDouble(geometry.Point1.X),
                           FormatDouble(geometry.Point1.Y),
                           FormatDouble(geometry.Point1.Z),
                           FormatDouble(geometry.Point2.X),
                           FormatDouble(geometry.Point2.Y),
                           FormatDouble(geometry.Point2.Z),
                           FormatDouble(geometry.PlaneNormal.X),
                           FormatDouble(geometry.PlaneNormal.Y),
                           FormatDouble(geometry.PlaneNormal.Z),
                           FormatDouble(geometry.PlaneWidth),
                           FormatDouble(geometry.PlaneHeight),
                           FormatDouble(geometry.Radius),
                           geometry.NonConformal,
                           geometry.InterRegion);
        }

        private void btnAddModel_Click(object sender, EventArgs e)
        {
            ReadGridToConfiguration();
            BaramGeometryFactory.SeedModelGeometry(_configuration);
            LoadGrid();
        }

        private void btnAddModelParts_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, "Adding PrePoMax geometry parts to BaramMesh will be completed in a later step.",
                            "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnImportStl_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_projectDirectory))
            {
                MessageBox.Show(this, "Create a BaramMesh case before importing STL files.",
                                "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            //
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Stereolithography files|*.stl";
                dialog.Multiselect = true;
                dialog.Title = "Import STL";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                SplitAndAddStlFiles(dialog.FileNames);
            }
        }

        private void btnSplitStl_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_projectDirectory))
            {
                MessageBox.Show(this, "Create a BaramMesh case before splitting STL files.",
                                "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Stereolithography files|*.stl";
                dialog.Multiselect = true;
                dialog.Title = "Split STL";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                SplitAndAddStlFiles(dialog.FileNames);
            }
        }
        private void SplitAndAddStlFiles(string[] fileNames)
        {
            using (FrmBaramMeshStlSplit splitForm = new FrmBaramMeshStlSplit(fileNames, _projectDirectory))
            {
                if (splitForm.ShowDialog(this) != DialogResult.OK) return;

                ReadGridToConfiguration();
                foreach (AssignedSplitStlFile file in splitForm.AssignedFiles)
                    BaramGeometryFactory.AddImportedStlSurface(_configuration, file.FileName, file.Name);
                LoadGrid();
            }
        }

        private void btnAddHex6_Click(object sender, EventArgs e)
        {
            ReadGridToConfiguration();
            BaramGeometryFactory.AddHex6FromBounds(_configuration, _modelBounds);
            LoadGrid();
        }

        private void btnAssignSurface_Click(object sender, EventArgs e)
        {
            try
            {
                int[] ids = GetSelectedGeometryIds();
                if (ids.Length == 0)
                {
                    MessageBox.Show(this, "Select STL segment rows to assign.",
                                    "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                AssignExternalStlRows(ids, "wall");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnAssignRemaining_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_projectDirectory))
                    throw new InvalidOperationException("Create a BaramMesh case before assigning STL surfaces.");
                //
                ReadGridToConfiguration();
                List<int> ids = new List<int>();
                foreach (KeyValuePair<int, BaramGeometry> entry in _configuration.Geometry)
                {
                    if (IsSplitSegmentGeometry(entry.Value)) ids.Add(entry.Key);
                }
                if (ids.Count == 0)
                {
                    MessageBox.Show(this, "There are no remaining split STL segment rows.",
                                    "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                AssignExternalStlRows(ids.ToArray(), "wall");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AssignExternalStlRows(int[] ids, string defaultName)
        {
            if (string.IsNullOrWhiteSpace(_projectDirectory))
                throw new InvalidOperationException("Create a BaramMesh case before assigning STL surfaces.");
            //
            ReadGridToConfiguration();
            List<string> fileNames = new List<string>();
            foreach (int id in ids)
            {
                BaramGeometry geometry;
                if (!_configuration.Geometry.TryGetValue(id, out geometry)) continue;
                string fileName;
                if (!IsExternalStlSurface(geometry, out fileName))
                    throw new InvalidOperationException("Only external STL surface rows can be assigned.");
                fileNames.Add(fileName);
            }
            if (fileNames.Count == 0) throw new InvalidOperationException("No external STL surface rows were selected.");
            //
            string surfaceName = PromptSurfaceName(defaultName);
            if (string.IsNullOrWhiteSpace(surfaceName)) return;
            //
            string target = Path.Combine(_projectDirectory, "geometry", "assigned");
            AssignedSplitStlFile file = BaramStlFeatureSplitter.WriteMergedStlFiles(fileNames, surfaceName, target);
            foreach (int id in ids) _configuration.Geometry.Remove(id);
            int newId = BaramGeometryFactory.AddImportedStlSurface(_configuration, file.FileName, file.Name);
            LoadGrid();
            SelectGeometryRow(newId);
        }

        private int[] GetSelectedGeometryIds()
        {
            List<int> ids = new List<int>();
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (!row.IsNewRow) ids.Add(ReadInt(row, ColId, "Id"));
            }
            if (ids.Count == 0 && _grid.CurrentRow != null && !_grid.CurrentRow.IsNewRow)
                ids.Add(ReadInt(_grid.CurrentRow, ColId, "Id"));
            return ids.Distinct().ToArray();
        }

        private bool IsSplitSegmentGeometry(BaramGeometry geometry)
        {
            string fileName;
            if (!IsExternalStlSurface(geometry, out fileName)) return false;
            //
            string splitDirectory = Path.GetFullPath(Path.Combine(_projectDirectory, "geometry", "split"));
            string fullFileName = Path.GetFullPath(fileName);
            return fullFileName.StartsWith(splitDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExternalStlSurface(BaramGeometry geometry, out string fileName)
        {
            fileName = null;
            if (geometry == null ||
                geometry.GeometryType != BaramGeometryType.Surface ||
                geometry.Shape != BaramShape.TriSurfaceMesh)
                return false;
            return BaramGeometryFactory.TryGetExternalStlFileName(geometry.Path, out fileName);
        }

        private void SelectGeometryRow(int id)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                row.Selected = false;
                if (!row.IsNewRow && ReadInt(row, ColId, "Id") == id)
                {
                    row.Selected = true;
                    _grid.CurrentCell = row.Cells[ColName];
                }
            }
        }

        private string PromptSurfaceName(string defaultName)
        {
            using (Form form = new Form())
            using (TextBox textBox = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                form.Text = "Assign Surface";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(320, 86);
                //
                Label label = new Label();
                label.Text = "Surface name";
                label.AutoSize = true;
                label.Location = new Point(10, 13);
                form.Controls.Add(label);
                //
                textBox.Text = BaramGeometryFactory.SanitizeGeometryName(defaultName);
                textBox.Location = new Point(100, 10);
                textBox.Width = 205;
                form.Controls.Add(textBox);
                //
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(149, 50);
                ok.Width = 75;
                form.Controls.Add(ok);
                //
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(230, 50);
                cancel.Width = 75;
                form.Controls.Add(cancel);
                //
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog(this) != DialogResult.OK) return null;
                return BaramGeometryFactory.SanitizeGeometryName(textBox.Text);
            }
        }

        private void btnAddPlane_Click(object sender, EventArgs e)
        {
            ReadGridToConfiguration();
            BaramGeometryFactory.AddPlane(_configuration, _modelBounds);
            LoadGrid();
        }

        private void btnAddBox_Click(object sender, EventArgs e)
        {
            AddRefinementVolume(BaramShape.Hex);
        }

        private void btnAddSphere_Click(object sender, EventArgs e)
        {
            AddRefinementVolume(BaramShape.Sphere);
        }

        private void btnAddCylinder_Click(object sender, EventArgs e)
        {
            AddRefinementVolume(BaramShape.Cylinder);
        }

        private void AddRefinementVolume(BaramShape shape)
        {
            try
            {
                ReadGridToConfiguration();
                int id;
                if (shape == BaramShape.Hex) id = BaramGeometryFactory.AddRefinementBox(_configuration, _modelBounds);
                else if (shape == BaramShape.Sphere)
                    id = BaramGeometryFactory.AddRefinementSphere(_configuration, _modelBounds);
                else if (shape == BaramShape.Cylinder)
                    id = BaramGeometryFactory.AddRefinementCylinder(_configuration, _modelBounds);
                else throw new InvalidOperationException("Unsupported refinement volume shape: " + shape + ".");
                //
                BaramGeometry geometry = _configuration.Geometry[id];
                using (FrmBaramMeshRefinementVolume form =
                       new FrmBaramMeshRefinementVolume(_configuration, id, geometry))
                {
                    if (form.ShowDialog(this) == DialogResult.OK) _configuration.Geometry[id] = form.Geometry;
                    else _configuration.Geometry.Remove(id);
                }
                LoadGrid();
                if (_configuration.Geometry.ContainsKey(id)) SelectGeometryRow(id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            try
            {
                int[] ids = GetSelectedGeometryIds();
                if (ids.Length != 1)
                {
                    MessageBox.Show(this, "Select one Box, Sphere, or Cylinder refinement volume.",
                                    "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ReadGridToConfiguration();
                BaramGeometry geometry;
                if (!_configuration.Geometry.TryGetValue(ids[0], out geometry) ||
                    geometry.GeometryType != BaramGeometryType.Volume ||
                    (geometry.Shape != BaramShape.Hex && geometry.Shape != BaramShape.Sphere &&
                     geometry.Shape != BaramShape.Cylinder))
                    throw new InvalidOperationException("Only Box, Sphere, and Cylinder refinement volumes can be edited here.");
                using (FrmBaramMeshRefinementVolume form =
                       new FrmBaramMeshRefinementVolume(_configuration, ids[0], geometry))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;
                    _configuration.Geometry[ids[0]] = form.Geometry;
                }
                LoadGrid();
                SelectGeometryRow(ids[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) btnEdit_Click(sender, EventArgs.Empty);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (!row.IsNewRow) _grid.Rows.Remove(row);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                ReadGridToConfiguration();
                _configuration.Step = BaramStep.Geometry;
                _configuration.Validate();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "BaramMesh geometry", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
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

        private void ReadGridToConfiguration()
        {
            BaramMeshConfiguration next = new BaramMeshConfiguration();
            next.Step = _configuration.Step;
            next.BaseGrid = _configuration.BaseGrid;
            next.Castellation = _configuration.Castellation;
            next.Snap = _configuration.Snap;
            next.BoundaryLayer = _configuration.BoundaryLayer;
            next.MeshQuality = _configuration.MeshQuality;
            foreach (KeyValuePair<int, BaramRegion> entry in _configuration.Regions)
                next.Regions[entry.Key] = entry.Value;

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<int, BaramGeometry> entry in _configuration.Geometry)
            {
                if (!IsBaseGridGeometry(entry.Key, entry.Value, _configuration)) continue;
                next.Geometry[entry.Key] = entry.Value;
                names.Add(entry.Value.Name);
            }
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                int id = ReadInt(row, ColId, "Id");
                BaramGeometry geometry = ReadGeometry(row);
                if (names.Contains(geometry.Name)) throw new InvalidOperationException("Duplicate geometry name: " + geometry.Name);
                if (geometry.Name.IndexOf(' ') >= 0) throw new InvalidOperationException("Geometry name cannot contain spaces: " + geometry.Name);
                names.Add(geometry.Name);
                next.Geometry[id] = geometry;
            }

            if (next.BaseGrid.BoundingHex6.HasValue)
            {
                BaramGeometry geometry;
                if (!next.Geometry.TryGetValue(next.BaseGrid.BoundingHex6.Value, out geometry) ||
                    geometry.GeometryType != BaramGeometryType.Volume ||
                    geometry.Shape != BaramShape.Hex6)
                    next.BaseGrid.BoundingHex6 = null;
            }

            RemoveUnusedRefinementGroups(next);

            _configuration = next;
        }
        private static void RemoveUnusedRefinementGroups(BaramMeshConfiguration configuration)
        {
            HashSet<int> surfaceGroups = new HashSet<int>(configuration.Geometry.Values
                .Where(g => g != null && g.GeometryType == BaramGeometryType.Surface && g.CastellationGroup.HasValue)
                .Select(g => g.CastellationGroup.Value));
            HashSet<int> volumeGroups = new HashSet<int>(configuration.Geometry.Values
                .Where(g => g != null && g.GeometryType == BaramGeometryType.Volume && g.CastellationGroup.HasValue)
                .Select(g => g.CastellationGroup.Value));
            foreach (int group in configuration.Castellation.RefinementSurfaces.Keys.ToArray())
                if (!surfaceGroups.Contains(group)) configuration.Castellation.RefinementSurfaces.Remove(group);
            foreach (int group in configuration.Castellation.RefinementVolumes.Keys.ToArray())
                if (!volumeGroups.Contains(group)) configuration.Castellation.RefinementVolumes.Remove(group);
        }
        private static bool IsBaseGridGeometry(int id, BaramGeometry geometry, BaramMeshConfiguration configuration)
        {
            if (geometry == null || configuration == null || configuration.BaseGrid == null ||
                !configuration.BaseGrid.BoundingHex6.HasValue)
                return false;
            //
            int boundingHex6 = configuration.BaseGrid.BoundingHex6.Value;
            if (id == boundingHex6) return true;
            return geometry.VolumeId.HasValue && geometry.VolumeId.Value == boundingHex6;
        }

        private BaramGeometry ReadGeometry(DataGridViewRow row)
        {
            int id = ReadInt(row, ColId, "Id");
            BaramGeometry geometry = new BaramGeometry();
            BaramGeometry previous;
            if (_configuration.Geometry.TryGetValue(id, out previous))
            {
                geometry.CastellationGroup = previous.CastellationGroup;
                geometry.LayerGroup = previous.LayerGroup;
                geometry.SlaveLayerGroup = previous.SlaveLayerGroup;
                geometry.AddBufferLayers = previous.AddBufferLayers;
                geometry.BoundaryRole = previous.BoundaryRole;
            }
            geometry.Name = ReadString(row, ColName);
            geometry.GeometryType = ReadEnum<BaramGeometryType>(row, ColGeometryType);
            geometry.Shape = ReadEnum<BaramShape>(row, ColShape);
            geometry.CfdType = ReadEnum<BaramCfdType>(row, ColCfdType);
            if (geometry.CfdType != BaramCfdType.Boundary)
                geometry.BoundaryRole = BaramBoundaryRole.Unassigned;
            geometry.VolumeId = ReadNullableInt(row, ColVolumeId);
            geometry.Path = ReadString(row, ColPath);
            geometry.Point1 = new BaramVector(ReadDouble(row, ColP1X), ReadDouble(row, ColP1Y), ReadDouble(row, ColP1Z));
            geometry.Point2 = new BaramVector(ReadDouble(row, ColP2X), ReadDouble(row, ColP2Y), ReadDouble(row, ColP2Z));
            geometry.PlaneNormal = new BaramVector(ReadDouble(row, ColNormalX), ReadDouble(row, ColNormalY),
                                                   ReadDouble(row, ColNormalZ));
            geometry.PlaneWidth = ReadDouble(row, ColWidth);
            geometry.PlaneHeight = ReadDouble(row, ColHeight);
            geometry.Radius = ReadDouble(row, ColRadius);
            geometry.NonConformal = ReadBool(row, ColNonConformal);
            geometry.InterRegion = ReadBool(row, ColInterRegion);
            return geometry;
        }

        private static string ReadString(DataGridViewRow row, string columnName)
        {
            object value = row.Cells[columnName].Value;
            return value == null ? "" : value.ToString();
        }

        private static int ReadInt(DataGridViewRow row, string columnName, string label)
        {
            int value;
            if (!int.TryParse(ReadString(row, columnName), out value))
                throw new InvalidOperationException(label + " is not a valid integer.");
            return value;
        }

        private static int? ReadNullableInt(DataGridViewRow row, string columnName)
        {
            string text = ReadString(row, columnName);
            if (string.IsNullOrWhiteSpace(text)) return null;
            int value;
            if (!int.TryParse(text, out value))
                throw new InvalidOperationException(columnName + " is not a valid integer.");
            return value;
        }

        private static double ReadDouble(DataGridViewRow row, string columnName)
        {
            double value;
            if (!double.TryParse(ReadString(row, columnName), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                !double.TryParse(ReadString(row, columnName), out value))
                throw new InvalidOperationException(columnName + " is not a valid number.");
            return value;
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static bool ReadBool(DataGridViewRow row, string columnName)
        {
            object value = row.Cells[columnName].Value;
            return value is bool && (bool)value;
        }

        private static T ReadEnum<T>(DataGridViewRow row, string columnName)
        {
            string text = ReadString(row, columnName);
            if (string.IsNullOrWhiteSpace(text)) text = Enum.GetNames(typeof(T))[0];
            return (T)Enum.Parse(typeof(T), text);
        }
    }
}
