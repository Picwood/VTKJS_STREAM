// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeGlobals;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmLocalExtremaSearch : Form
    {
        private readonly ComboBox _cbType;
        private readonly TextBox _tbThreshold;
        private readonly TextBox _tbTolerance;
        private readonly NumericUpDown _numMaxPoints;
        private readonly CheckBox _cbVisiblePartsOnly;

        public LocalExtremaSearchParameters Parameters { get; private set; }

        public FrmLocalExtremaSearch(string fieldName, string componentName, string unit)
        {
            Text = "Find Local Extrema";
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(390, 244);
            //
            Label lblField = new Label();
            lblField.AutoSize = false;
            lblField.Location = new Point(12, 12);
            lblField.Size = new Size(366, 32);
            lblField.Text = "Active result: " + fieldName + " / " + componentName +
                            (string.IsNullOrEmpty(unit) || unit == "/" ? "" : " [" + unit + "]");
            //
            Label lblType = new Label();
            lblType.AutoSize = true;
            lblType.Location = new Point(12, 55);
            lblType.Text = "Search for:";
            //
            _cbType = new ComboBox();
            _cbType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cbType.Items.AddRange(new object[] { "Local maximum", "Local minimum" });
            _cbType.SelectedIndex = 0;
            _cbType.Location = new Point(118, 52);
            _cbType.Size = new Size(260, 23);
            //
            Label lblThreshold = new Label();
            lblThreshold.AutoSize = true;
            lblThreshold.Location = new Point(12, 84);
            lblThreshold.Text = "Threshold:";
            //
            _tbThreshold = new TextBox();
            _tbThreshold.Location = new Point(118, 81);
            _tbThreshold.Size = new Size(260, 23);
            //
            Label lblTolerance = new Label();
            lblTolerance.AutoSize = true;
            lblTolerance.Location = new Point(12, 113);
            lblTolerance.Text = "Tolerance:";
            //
            _tbTolerance = new TextBox();
            _tbTolerance.Location = new Point(118, 110);
            _tbTolerance.Size = new Size(260, 23);
            _tbTolerance.Text = "0";
            //
            Label lblMaxPoints = new Label();
            lblMaxPoints.AutoSize = true;
            lblMaxPoints.Location = new Point(12, 142);
            lblMaxPoints.Text = "Max points:";
            //
            _numMaxPoints = new NumericUpDown();
            _numMaxPoints.Location = new Point(118, 139);
            _numMaxPoints.Size = new Size(90, 23);
            _numMaxPoints.Minimum = 1;
            _numMaxPoints.Maximum = 10000;
            _numMaxPoints.Value = 10;
            //
            _cbVisiblePartsOnly = new CheckBox();
            _cbVisiblePartsOnly.AutoSize = true;
            _cbVisiblePartsOnly.Location = new Point(118, 169);
            _cbVisiblePartsOnly.Text = "Visible parts only";
            _cbVisiblePartsOnly.Checked = true;
            //
            Button btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.DialogResult = DialogResult.OK;
            btnOk.Size = new Size(75, 25);
            btnOk.Location = new Point(222, 207);
            btnOk.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            btnOk.Click += btnOk_Click;
            //
            Button btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Size = new Size(75, 25);
            btnCancel.Location = new Point(303, 207);
            btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            //
            Controls.Add(lblField);
            Controls.Add(lblType);
            Controls.Add(_cbType);
            Controls.Add(lblThreshold);
            Controls.Add(_tbThreshold);
            Controls.Add(lblTolerance);
            Controls.Add(_tbTolerance);
            Controls.Add(lblMaxPoints);
            Controls.Add(_numMaxPoints);
            Controls.Add(_cbVisiblePartsOnly);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            //
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (!TryParseDouble(_tbThreshold.Text, out double threshold))
            {
                MessageBoxes.ShowError("Enter a valid threshold.");
                DialogResult = DialogResult.None;
                _tbThreshold.Focus();
                return;
            }
            if (!TryParseDouble(_tbTolerance.Text, out double tolerance) || tolerance < 0)
            {
                MessageBoxes.ShowError("Enter a valid non-negative tolerance.");
                DialogResult = DialogResult.None;
                _tbTolerance.Focus();
                return;
            }
            //
            Parameters = new LocalExtremaSearchParameters()
            {
                Type = _cbType.SelectedIndex == 0 ? LocalExtremaSearchType.Maximum : LocalExtremaSearchType.Minimum,
                Threshold = threshold,
                Tolerance = tolerance,
                MaxPoints = (int)_numMaxPoints.Value,
                VisiblePartsOnly = _cbVisiblePartsOnly.Checked
            };
        }

        private bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                   double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
