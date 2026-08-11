// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CaeMesh;
using CaeGlobals;
using System.ComponentModel;
using DynamicTypeDescriptor;
using System.Drawing.Design;
using System.IO;
using System.Windows.Forms;

namespace PrePoMax.Settings
{
    [Serializable]
    public class ViewMeshingSettings : Forms.ViewMeshingParameters, IViewSettings, IReset
    {
        // Variables                                                                                                                
        MeshingSettings _meshingSettings;


        // Properties                                                                                                               
        [CategoryAttribute("OpenFOAM")]
        [OrderedDisplayName(0, 10, "OpenFOAM root directory")]
        [DescriptionAttribute("Select the OpenFOAM installation root directory.")]
        [EditorAttribute(typeof(System.Windows.Forms.Design.FolderNameEditor), typeof(UITypeEditor))]
        [Id(1, 10)]
        public string OpenFoamRootDirectory
        {
            get { return _meshingSettings != null ? _meshingSettings.OpenFoamRootDirectory : ""; }
            set
            {
                if (_meshingSettings == null) return;
                if (value != _meshingSettings.OpenFoamRootDirectory)
                {
                    if (!string.IsNullOrWhiteSpace(value) && !Directory.Exists(value) &&
                        MessageBoxes.ShowWarningQuestionOKCancel("The selected OpenFOAM root directory does not exist.") ==
                        DialogResult.Cancel)
                        return;
                    //
                    _meshingSettings.OpenFoamRootDirectory = value;
                }
            }
        }
        //
        [CategoryAttribute("OpenFOAM")]
        [OrderedDisplayName(1, 10, "Number of processors")]
        [DescriptionAttribute("Set the default number of processors for OpenFOAM meshing utilities.")]
        [Id(2, 10)]
        public int OpenFoamNumCPUs
        {
            get { return _meshingSettings != null ? _meshingSettings.OpenFoamNumCPUs : 1; }
            set
            {
                if (_meshingSettings != null) _meshingSettings.OpenFoamNumCPUs = value;
            }
        }


        // Constructors                                                                                                             
        public ViewMeshingSettings(MeshingSettings meshingSettings)
            : this(EnsureMeshingSettings(meshingSettings), true)
        {
        }
        private ViewMeshingSettings(MeshingSettings meshingSettings, bool normalized)
            : base(meshingSettings.MeshingParameters)
        {
            _meshingSettings = meshingSettings;
            //
            _settingsView = true;
            UpdateVisibility(); // apply settings view
            //
            _dctd.GetProperty(nameof(AdvancedView)).SetIsBrowsable(false);
            _dctd.GetProperty(nameof(Name)).SetIsBrowsable(false);
            _dctd.GetProperty(nameof(MaxH)).SetIsBrowsable(false);
            _dctd.GetProperty(nameof(MinH)).SetIsBrowsable(false);
            _dctd.GetProperty(nameof(Hausdorff)).SetIsBrowsable(false);
        }


        // Methods                                                                                                                  
        private static MeshingSettings EnsureMeshingSettings(MeshingSettings meshingSettings)
        {
            if (meshingSettings == null) meshingSettings = new MeshingSettings();
            meshingSettings.CheckValues();
            return meshingSettings;
        }
        new public ISettings GetBase()
        {
            return _meshingSettings;
        }
        public void Reset()
        {
            _meshingSettings.Reset();
            UpdateVisibility(); // apply settings view
        }
    }
}
