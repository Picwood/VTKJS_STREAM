// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmBaramMeshExport : Form
    {
        private readonly TextBox _txtProjectDirectory;
        private readonly Button _btnBrowse;
        private readonly Button _btnExport;
        private readonly Button _btnCancel;

        public string ProjectDirectory { get; private set; }

        public FrmBaramMeshExport(string initialDirectory)
        {
            _txtProjectDirectory = new TextBox { Dock = DockStyle.Fill };
            _btnBrowse = new Button();
            _btnExport = new Button();
            _btnCancel = new Button();
            //
            Text = "Export BaramMesh Project";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(650, 122);
            InitializeComponent();
            //
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                string parent = Directory.GetParent(initialDirectory) != null
                                    ? Directory.GetParent(initialDirectory).FullName
                                    : initialDirectory;
                _txtProjectDirectory.Text = Path.Combine(parent, "BaramFlow_Project.bf");
            }
        }

        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);
            //
            TableLayoutPanel path = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            path.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
            path.Controls.Add(new Label
            {
                Text = "Project directory",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            path.Controls.Add(_txtProjectDirectory, 1, 0);
            _btnBrowse.Text = "...";
            _btnBrowse.Dock = DockStyle.Fill;
            _btnBrowse.Click += btnBrowse_Click;
            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(_btnBrowse, "Choose BaramFlow project path");
            path.Controls.Add(_btnBrowse, 2, 0);
            root.Controls.Add(path, 0, 0);
            //
            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            ConfigureButton(_btnCancel, "Cancel", btnCancel_Click);
            ConfigureButton(_btnExport, "Export", btnExport_Click);
            footer.Controls.AddRange(new Control[] { _btnCancel, _btnExport });
            root.Controls.Add(footer, 0, 1);
            AcceptButton = _btnExport;
            CancelButton = _btnCancel;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export BaramFlow Project";
                dialog.Filter = "BaramFlow project (*.bf)|*.bf|All files (*.*)|*.*";
                dialog.DefaultExt = "bf";
                dialog.AddExtension = true;
                dialog.CheckFileExists = false;
                dialog.OverwritePrompt = false;
                string current = _txtProjectDirectory.Text.Trim();
                if (!string.IsNullOrWhiteSpace(current))
                {
                    string directory = Path.GetDirectoryName(current);
                    if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                        dialog.InitialDirectory = directory;
                    dialog.FileName = Path.GetFileName(current);
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _txtProjectDirectory.Text = dialog.FileName;
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            try
            {
                string directory = _txtProjectDirectory.Text.Trim();
                if (string.IsNullOrWhiteSpace(directory))
                    throw new InvalidOperationException("Select an export project directory.");
                directory = Path.GetFullPath(directory);
                if (!directory.EndsWith(".bf", StringComparison.OrdinalIgnoreCase)) directory += ".bf";
                if (Directory.Exists(directory) &&
                    (Directory.EnumerateDirectories(directory).Any() ||
                     Directory.EnumerateFiles(directory).Any()))
                    throw new InvalidOperationException("The export project directory must be new or empty.");
                ProjectDirectory = directory;
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

        private static void ConfigureButton(Button button, string text, EventHandler click)
        {
            button.Text = text;
            button.Width = 82;
            button.Height = 24;
            button.Click += click;
        }
    }
}
