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
using System.ComponentModel;
using CaeGlobals;
using System.IO;
using DynamicTypeDescriptor;
using System.Drawing;
using CaeMesh;

namespace PrePoMax
{
    [Serializable]
    public class MeshingSettings : ISettings
    {
        // Variables                                                                                                                
        private MeshingParameters _meshingParameters;
        private string _openFoamRootDirectory;
        private int _openFoamNumCPUs;


        // Properties                                                                                                               
        public MeshingParameters MeshingParameters { get { return _meshingParameters; } set { _meshingParameters = value; } }
        public string OpenFoamRootDirectory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_openFoamRootDirectory)) return "";
                return Tools.GetGlobalPath(_openFoamRootDirectory);
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _openFoamRootDirectory = null;
                    return;
                }

                string path = Tools.GetGlobalPath(value);
                if (string.IsNullOrWhiteSpace(path))
                {
                    _openFoamRootDirectory = null;
                    return;
                }
                if (!Directory.Exists(path))
                    throw new Exception("The selected OpenFOAM root directory does not exist.");
                _openFoamRootDirectory = Tools.GetLocalPath(path);
            }
        }
        public int OpenFoamNumCPUs
        {
            get { return _openFoamNumCPUs < 1 ? 1 : _openFoamNumCPUs; }
            set
            {
                _openFoamNumCPUs = value;
                if (_openFoamNumCPUs < 1) _openFoamNumCPUs = 1;
            }
        }


        // Constructors                                                                                                             
        public MeshingSettings()
        {
            _meshingParameters = new MeshingParameters("Settings");
            _openFoamRootDirectory = null;
            _openFoamNumCPUs = 1;
        }
        public MeshingSettings(MeshingParameters meshingParameters)
        {
            _meshingParameters = meshingParameters;
            _openFoamRootDirectory = null;
            _openFoamNumCPUs = 1;
        }


        // Methods                                                                                                                  
        public void CheckValues()
        {
            if (_meshingParameters == null) _meshingParameters = new MeshingParameters("Settings");
            if (_openFoamNumCPUs < 1) _openFoamNumCPUs = 1;
        }
        public void Reset()
        {
            if (_meshingParameters == null) _meshingParameters = new MeshingParameters("Settings");
            _meshingParameters.Reset();
            _openFoamRootDirectory = null;
            _openFoamNumCPUs = 1;
        }
        
    }
}
