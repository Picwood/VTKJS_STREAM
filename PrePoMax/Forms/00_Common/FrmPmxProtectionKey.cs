// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeGlobals;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    internal class FrmPmxProtectionKey : Form
    {
        private readonly bool _confirmKey;
        private readonly TextBox _tbKey;
        private readonly TextBox _tbConfirmKey;

        public string HashKey { get; private set; }

        private FrmPmxProtectionKey(string title, string message, bool confirmKey)
        {
            _confirmKey = confirmKey;
            //
            Text = title;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, confirmKey ? 176 : 132);
            //
            Label lblMessage = new Label();
            lblMessage.AutoSize = false;
            lblMessage.Location = new Point(12, 12);
            lblMessage.Size = new Size(396, 36);
            lblMessage.Text = message;
            //
            Label lblKey = new Label();
            lblKey.AutoSize = true;
            lblKey.Location = new Point(12, 58);
            lblKey.Text = "Hash key:";
            //
            _tbKey = new TextBox();
            _tbKey.Location = new Point(104, 55);
            _tbKey.Size = new Size(304, 23);
            _tbKey.UseSystemPasswordChar = true;
            _tbKey.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            //
            Label lblConfirmKey = new Label();
            lblConfirmKey.AutoSize = true;
            lblConfirmKey.Location = new Point(12, 91);
            lblConfirmKey.Text = "Confirm key:";
            lblConfirmKey.Visible = confirmKey;
            //
            _tbConfirmKey = new TextBox();
            _tbConfirmKey.Location = new Point(104, 88);
            _tbConfirmKey.Size = new Size(304, 23);
            _tbConfirmKey.UseSystemPasswordChar = true;
            _tbConfirmKey.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _tbConfirmKey.Visible = confirmKey;
            //
            Button btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.DialogResult = DialogResult.OK;
            btnOk.Size = new Size(75, 25);
            btnOk.Location = new Point(252, confirmKey ? 136 : 92);
            btnOk.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            btnOk.Click += btnOk_Click;
            //
            Button btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Size = new Size(75, 25);
            btnCancel.Location = new Point(333, confirmKey ? 136 : 92);
            btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            //
            Controls.Add(lblMessage);
            Controls.Add(lblKey);
            Controls.Add(_tbKey);
            Controls.Add(lblConfirmKey);
            Controls.Add(_tbConfirmKey);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            //
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public static string GetHashKey(IWin32Window owner, string title, string message, bool confirmKey)
        {
            using (FrmPmxProtectionKey form = new FrmPmxProtectionKey(title, message, confirmKey))
            {
                if (form.ShowDialog(owner) == DialogResult.OK) return form.HashKey;
                else return null;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            string hashKey = _tbKey.Text;
            if (string.IsNullOrEmpty(hashKey))
            {
                MessageBoxes.ShowError("The hash key must not be empty.");
                DialogResult = DialogResult.None;
                _tbKey.Focus();
                return;
            }
            if (_confirmKey && hashKey != _tbConfirmKey.Text)
            {
                MessageBoxes.ShowError("The entered hash keys do not match.");
                DialogResult = DialogResult.None;
                _tbConfirmKey.Focus();
                return;
            }
            //
            HashKey = hashKey;
        }
    }
}
