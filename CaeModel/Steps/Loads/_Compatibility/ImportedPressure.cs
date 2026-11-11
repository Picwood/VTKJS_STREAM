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
using CaeResults;
using System.IO;
using System.Runtime.Serialization;

namespace CaeModel
{
    [Serializable]
    public partial class ImportedPressure : VariablePressure, ISerializable
    {
        // Variables                                                                                                                
        private string _fileName;                       //ISerializable
        private string _pressureTime;                   //ISerializable
        private string _pressureVariableName;           //ISerializable
        private InterpolatorEnum _interpolatorType;     //ISerializable
        private EquationContainer _magnitudeFactor;     //ISerializable
        private EquationContainer _geomScaleFactor;     //ISerializable
        //
        private FileInfo _oldFileInfo;                  //ISerializable
        //
        [NonSerialized]
        private ResultsInterpolator _interpolator;


        // Properties                                                                                                               
        public string FileName { get { return _fileName; } set { _fileName = value; } }
        public string PressureTime { get { return _pressureTime; } set { _pressureTime = value; } }
        public string PressureVariableName { get { return _pressureVariableName; } set { _pressureVariableName = value; } }
        public InterpolatorEnum InterpolatorType { get { return _interpolatorType; } set { _interpolatorType = value; } }
        public EquationContainer MagnitudeFactor
        {
            get { return _magnitudeFactor; }
            set { SetMagnitudeFactor(value); }
        }
        public EquationContainer GeometryScaleFactor
        {
            get { return _geomScaleFactor; }
            set { SetGeomScaleFactor(value); }
        }


        // Constructors                                                                                                             
        public ImportedPressure(string name, string surfaceName, RegionTypeEnum regionType, bool twoD,
                                bool complex, double phaseDeg)
            : base(name, surfaceName, regionType, twoD, complex, phaseDeg)
        {
            _fileName = null;
            _pressureTime = null;
            _pressureVariableName = null;
            _interpolatorType = InterpolatorEnum.ClosestNode;
            MagnitudeFactor = new EquationContainer(typeof(StringDoubleConverter), 1);
            GeometryScaleFactor = new EquationContainer(typeof(StringDoubleConverter), 1);
            //
            _oldFileInfo = null;
        }
        public ImportedPressure(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            // Compatibility for version v1.4.0
            if (_regionType == RegionTypeEnum.PartName) _regionType = RegionTypeEnum.Selection;
            //
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "_fileName":
                        _fileName = (string)entry.Value; break;
                    case "_pressureTime":
                        _pressureTime = (string)entry.Value; break;
                    case "_pressureVariableName":
                        _pressureVariableName = (string)entry.Value; break;
                    case "_interpolatorType":
                        _interpolatorType = (InterpolatorEnum)entry.Value; break;
                    case "_magnitudeFactor":
                        // Compatibility for version v1.4.0
                        if (entry.Value is double valueMag)
                            MagnitudeFactor = new EquationContainer(typeof(StringDoubleConverter), valueMag);
                        else
                            SetMagnitudeFactor((EquationContainer)entry.Value, false);
                        break;
                    case "_geomScaleFactor":
                        // Compatibility for version v1.4.0
                        if (entry.Value is double valueSF)
                            GeometryScaleFactor = new EquationContainer(typeof(StringDoubleConverter), valueSF);
                        else
                            SetGeomScaleFactor((EquationContainer)entry.Value, false);
                        break;
                    case "_oldFileInfo":
                        _oldFileInfo = (FileInfo)entry.Value; break;
                    default:
                        break;
                }
            }
            // Compatibility for version v1.4.0
            if (_magnitudeFactor == null) MagnitudeFactor = new EquationContainer(typeof(StringDoubleConverter), 1);
            if (_geomScaleFactor == null) GeometryScaleFactor = new EquationContainer(typeof(StringDoubleConverter), 1);
        }


        // Methods                                                                                                                  
        private void SetMagnitudeFactor(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _magnitudeFactor, value, null, checkEquation);
        }
        private void SetGeomScaleFactor(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _geomScaleFactor, value, null, checkEquation);
        }
        // IContainsEquations
        public override void CheckEquations()
        {
            base.CheckEquations();
            //
            _magnitudeFactor.CheckEquation();
            _geomScaleFactor.CheckEquation();
        }
        //
        // ISerialization
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // Using typeof() works also for null fields
            base.GetObjectData(info, context);
            //
            info.AddValue("_fileName", _fileName, typeof(string));
            info.AddValue("_pressureTime", _pressureTime, typeof(string));
            info.AddValue("_pressureVariableName", _pressureVariableName, typeof(string));
            info.AddValue("_interpolatorType", _interpolatorType, typeof(InterpolatorEnum));
            info.AddValue("_magnitudeFactor", _magnitudeFactor, typeof(EquationContainer));
            info.AddValue("_geomScaleFactor", _geomScaleFactor, typeof(EquationContainer));
            info.AddValue("_oldFileInfo", _oldFileInfo, typeof(FileInfo));
        }
    }
}
