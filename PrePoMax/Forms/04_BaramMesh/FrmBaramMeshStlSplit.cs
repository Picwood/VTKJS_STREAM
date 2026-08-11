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
    class FrmBaramMeshStlSplit : Form
    {
        private const string ColId = "Id";
        private const string ColArea = "Area";
        private const string ColName = "Name";

        private readonly string[] _fileNames;
        private readonly string _outputDirectory;
        private readonly NumericUpDown _nudFeatureAngle;
        private readonly NumericUpDown _nudMinArea;
        private readonly Button _btnApply;
        private readonly DataGridView _grid;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private List<BaramStlSplitSegment> _segments;

        public List<AssignedSplitStlFile> AssignedFiles { get; private set; }

        public FrmBaramMeshStlSplit(string[] fileNames, string outputDirectory)
        {
            if (fileNames == null || fileNames.Length == 0) throw new ArgumentException("At least one STL file is required.", "fileNames");
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory is required.", "outputDirectory");

            _fileNames = fileNames;
            _outputDirectory = outputDirectory;
            _segments = new List<BaramStlSplitSegment>();
            AssignedFiles = new List<AssignedSplitStlFile>();

            Text = "Split STL by Feature Angle";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 520);
            Size = new Size(820, 580);

            _nudFeatureAngle = new NumericUpDown();
            _nudMinArea = new NumericUpDown();
            _btnApply = new Button();
            _grid = new DataGridView();
            _btnOk = new Button();
            _btnCancel = new Button();

            InitializeComponent();
            ApplySplit();
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            FlowLayoutPanel splitBar = new FlowLayoutPanel();
            splitBar.Dock = DockStyle.Fill;
            splitBar.Padding = new Padding(8, 8, 8, 4);
            splitBar.WrapContents = false;
            root.Controls.Add(splitBar, 0, 0);

            splitBar.Controls.Add(new Label { Text = "Feature angle", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 4, 2, 0) });
            _nudFeatureAngle.Minimum = 1;
            _nudFeatureAngle.Maximum = 90;
            _nudFeatureAngle.Value = 30;
            _nudFeatureAngle.Width = 60;
            splitBar.Controls.Add(_nudFeatureAngle);

            splitBar.Controls.Add(new Label { Text = "Min area (%)", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 4, 2, 0) });
            _nudMinArea.Minimum = 0;
            _nudMinArea.Maximum = 100;
            _nudMinArea.DecimalPlaces = 3;
            _nudMinArea.Increment = 0.1M;
            _nudMinArea.Value = 0;
            _nudMinArea.Width = 70;
            splitBar.Controls.Add(_nudMinArea);

            ConfigureButton(_btnApply, "Apply", 70, btnApply_Click);
            splitBar.Controls.Add(_btnApply);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.MultiSelect = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColId, HeaderText = "Segment", ReadOnly = true, Width = 70, FillWeight = 15 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColArea, HeaderText = "Area (%)", ReadOnly = true, Width = 90, FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ColName, HeaderText = "Segment Name", ReadOnly = true, FillWeight = 50 });
            root.Controls.Add(_grid, 0, 1);

            FlowLayoutPanel footer = new FlowLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.FlowDirection = FlowDirection.RightToLeft;
            footer.Padding = new Padding(8, 8, 8, 6);
            root.Controls.Add(footer, 0, 2);

            ConfigureButton(_btnCancel, "Cancel", 75, btnCancel_Click);
            ConfigureButton(_btnOk, "OK", 75, btnOk_Click);
            footer.Controls.Add(_btnCancel);
            footer.Controls.Add(_btnOk);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private static void ConfigureButton(Button button, string text, int width, EventHandler click)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 24;
            button.Margin = new Padding(6, 0, 0, 0);
            button.Click += click;
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            try
            {
                ApplySplit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Split STL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplySplit()
        {
            _segments.Clear();
            foreach (string fileName in _fileNames)
            {
                BaramStlFeatureSplitter splitter = new BaramStlFeatureSplitter(fileName);
                _segments.AddRange(splitter.Split((double)_nudFeatureAngle.Value, (double)_nudMinArea.Value));
            }

            // Keep segment ids unique across multiple input files.
            for (int i = 0; i < _segments.Count; i++)
            {
                BaramStlSplitSegment segment = _segments[i];
                if (segment.Id != i)
                    _segments[i] = new BaramStlSplitSegment(i, segment.SurfaceName, segment.AreaPercent, segment.Facets);
            }
            LoadGrid();
        }

        private void LoadGrid()
        {
            _grid.Rows.Clear();
            foreach (BaramStlSplitSegment segment in _segments)
            {
                _grid.Rows.Add(segment.Id.ToString(CultureInfo.InvariantCulture),
                               segment.AreaPercent.ToString("G6", CultureInfo.InvariantCulture),
                               segment.SurfaceName);
            }
            if (_grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
        }

        private void ReadGrid()
        {
            Dictionary<int, BaramStlSplitSegment> segmentById = _segments.ToDictionary(s => s.Id);
            foreach (DataGridViewRow row in _grid.Rows)
            {
                int id = int.Parse(Convert.ToString(row.Cells[ColId].Value), CultureInfo.InvariantCulture);
                BaramStlSplitSegment segment = segmentById[id];
                segment.SurfaceName = SanitizeName(Convert.ToString(row.Cells[ColName].Value));
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                ReadGrid();
                foreach (BaramStlSplitSegment segment in _segments)
                {
                    if (string.IsNullOrWhiteSpace(segment.SurfaceName))
                        throw new InvalidOperationException("Every segment must have a surface name.");
                }

                string target = Path.Combine(_outputDirectory, "geometry", "split");
                AssignedFiles = BaramStlFeatureSplitter.WriteSplitSegments(_segments, target);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Split STL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private static string SanitizeName(string name)
        {
            return BaramGeometryFactory.SanitizeGeometryName(name);
        }
    }
}
