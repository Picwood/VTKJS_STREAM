// PrePoMax - Copyright (C) 2016-2026 Matej BorovinÅ¡ek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej BorovinÅ¡ek
// Contributors:

using CaeGlobals;
using CaeResults;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    public class FrmExportDisplayedFrd : Form
    {
        private readonly CheckedListBox clbFields;
        private readonly ComboBox cbStep;
        private readonly ComboBox cbIncrement;
        private readonly CheckBox chkAllSteps;
        private readonly Button btnOk;
        private readonly Button btnCancel;
        private readonly Dictionary<int, int[]> stepIncrementIds;

        public string[] SelectedFieldNames
        {
            get { return clbFields.CheckedItems.Cast<string>().ToArray(); }
        }
        public bool AllStepsAndIncrements
        {
            get { return chkAllSteps.Checked; }
        }
        public int SelectedStepId
        {
            get { return cbStep.SelectedItem is int value ? value : -1; }
        }
        public int SelectedStepIncrementId
        {
            get { return cbIncrement.SelectedItem is int value ? value : -1; }
        }

        public FrmExportDisplayedFrd(string[] fieldNames, int[] stepIds, Func<int, int[]> getStepIncrementIds)
        {
            stepIncrementIds = new Dictionary<int, int[]>();
            foreach (int stepId in stepIds)
            {
                int[] incrementIds = getStepIncrementIds(stepId);
                int[] withoutArtificialZero = incrementIds.Where(id => !(stepId == 1 && id == 0)).ToArray();
                stepIncrementIds.Add(stepId, withoutArtificialZero.Length > 0 ? withoutArtificialZero : incrementIds);
            }
            //
            Text = "Export displayed results FRD";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(360, 370);
            //
            Label lblFields = new Label { Left = 12, Top = 12, Width = 330, Height = 18, Text = "Fields" };
            clbFields = new CheckedListBox
            {
                Left = 12,
                Top = 34,
                Width = 330,
                Height = 150,
                CheckOnClick = true,
                IntegralHeight = false
            };
            foreach (string fieldName in fieldNames) clbFields.Items.Add(fieldName, true);
            //
            chkAllSteps = new CheckBox
            {
                Left = 12,
                Top = 198,
                Width = 330,
                Height = 22,
                Text = "Export all steps and increments"
            };
            chkAllSteps.CheckedChanged += chkAllSteps_CheckedChanged;
            //
            Label lblStep = new Label { Left = 12, Top = 232, Width = 150, Height = 18, Text = "Step" };
            cbStep = new ComboBox
            {
                Left = 12,
                Top = 254,
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (int stepId in stepIds) cbStep.Items.Add(stepId);
            cbStep.SelectedIndexChanged += cbStep_SelectedIndexChanged;
            //
            Label lblIncrement = new Label { Left = 192, Top = 232, Width = 150, Height = 18, Text = "Increment" };
            cbIncrement = new ComboBox
            {
                Left = 192,
                Top = 254,
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            //
            btnOk = new Button { Left = 186, Top = 326, Width = 75, Height = 28, Text = "OK", DialogResult = DialogResult.OK };
            btnCancel = new Button { Left = 267, Top = 326, Width = 75, Height = 28, Text = "Cancel", DialogResult = DialogResult.Cancel };
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            btnOk.Click += btnOk_Click;
            //
            Controls.Add(lblFields);
            Controls.Add(clbFields);
            Controls.Add(chkAllSteps);
            Controls.Add(lblStep);
            Controls.Add(cbStep);
            Controls.Add(lblIncrement);
            Controls.Add(cbIncrement);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            //
            if (cbStep.Items.Count > 0) cbStep.SelectedIndex = cbStep.Items.Count - 1;
        }

        private void cbStep_SelectedIndexChanged(object sender, EventArgs e)
        {
            cbIncrement.Items.Clear();
            if (cbStep.SelectedItem is int stepId && stepIncrementIds.TryGetValue(stepId, out int[] incrementIds))
            {
                foreach (int incrementId in incrementIds) cbIncrement.Items.Add(incrementId);
                if (cbIncrement.Items.Count > 0) cbIncrement.SelectedIndex = cbIncrement.Items.Count - 1;
            }
        }

        private void chkAllSteps_CheckedChanged(object sender, EventArgs e)
        {
            cbStep.Enabled = !chkAllSteps.Checked;
            cbIncrement.Enabled = !chkAllSteps.Checked;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (SelectedFieldNames.Length == 0)
            {
                MessageBoxes.ShowError("Select at least one field to export.");
                DialogResult = DialogResult.None;
            }
        }
    }
}
