// PrePoMax - Copyright (C) 2016-2026 Matej BorovinÅ¡ek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej BorovinÅ¡ek
// Contributors:

using CaeGlobals;
using CaeModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using UserControls;

namespace PrePoMax.Forms
{
    class FrmCylinder : FrmProperties, IFormBase
    {
        private HashSet<string> _cylinderNames;
        private string _cylinderToEditName;
        private ViewFeCylinder _viewCylinder;
        private Controller _controller;


        // Properties
        public FeCylinder Cylinder
        {
            get { return _viewCylinder.GetBase(); }
            set { _viewCylinder = new ViewFeCylinder(value.DeepClone()); }
        }


        // Constructors
        public FrmCylinder(Controller controller)
        {
            InitializeComponent();
            //
            _controller = controller;
            _viewCylinder = null;
            _cylinderNames = new HashSet<string>();
        }
        private void InitializeComponent()
        {
            this.gbProperties.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbProperties
            // 
            this.gbProperties.Size = new System.Drawing.Size(310, 551);
            // 
            // propertyGrid
            // 
            this.propertyGrid.Size = new System.Drawing.Size(298, 523);
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(160, 563);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(241, 563);
            // 
            // btnOkAddNew
            // 
            this.btnOkAddNew.Location = new System.Drawing.Point(79, 563);
            // 
            // FrmCylinder
            // 
            this.ClientSize = new System.Drawing.Size(334, 598);
            this.Name = "FrmCylinder";
            this.Text = "Edit Cylinder";
            this.Controls.SetChildIndex(this.gbProperties, 0);
            this.Controls.SetChildIndex(this.btnCancel, 0);
            this.Controls.SetChildIndex(this.btnOK, 0);
            this.Controls.SetChildIndex(this.btnOkAddNew, 0);
            this.gbProperties.ResumeLayout(false);
            this.ResumeLayout(false);
        }


        // Overrides
        protected override bool OnPrepareForm(string stepName, string cylinderToEditName)
        {
            btnOkAddNew.Visible = cylinderToEditName == null;
            //
            _propertyItemChanged = false;
            _cylinderNames.Clear();
            _cylinderToEditName = cylinderToEditName;
            _viewCylinder = null;
            //
            string[] cylinderNames = _controller.GetModelCylinderNames();
            if (cylinderNames != null) _cylinderNames.UnionWith(cylinderNames);
            //
            if (_cylinderToEditName == null)
            {
                Cylinder = new FeCylinder(GetCylinderName());
            }
            else
            {
                Cylinder = _controller.GetModelCylinder(_cylinderToEditName);
            }
            //
            propertyGrid.SelectedObject = _viewCylinder;
            return true;
        }
        protected override void OnApply(bool onOkAddNew)
        {
            _viewCylinder = (ViewFeCylinder)propertyGrid.SelectedObject;
            CheckName(_cylinderToEditName, _viewCylinder.Name, _cylinderNames, "cylinder");
            //
            FeCylinder cylinder = Cylinder;
            if (!cylinder.IsProperlyDefined(out string error)) throw new CaeException(error);
            //
            if (_cylinderToEditName == null)
            {
                _controller.AddModelCylinderCommand(cylinder);
            }
            else if (_propertyItemChanged)
            {
                _controller.ReplaceModelCylinderCommand(_cylinderToEditName, cylinder);
                _cylinderToEditName = null;
            }
        }


        // Methods
        private string GetCylinderName()
        {
            string name = "Cylinder";
            if (_cylinderNames.Contains(name)) name = _cylinderNames.GetNextNumberedKey(name);
            return name;
        }
    }
}
