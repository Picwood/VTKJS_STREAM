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
using System.Runtime.Serialization;
using System.Drawing;

namespace CaeModel
{
    [Serializable]
    public partial class DLoad : VariablePressure, IDistribution, ISerializable
    {
        // Variables                                                                                                                
        private int _elementId;                     //ISerializable
        private FeFaceName _elementFaceName;        //ISerializable
        private EquationContainer _magnitude;       //ISerializable
        private string _distributionName;           //ISerializable


        // Properties                                                                                                               
        public override string RegionName { get { return _surfaceName; } set { _surfaceName = value; } }
        public int  ElementId { get { return _elementId; } set { _elementId = value; } }
        public FeFaceName ElementFaceName { get { return _elementFaceName; } set { _elementFaceName = value; } }
        public EquationContainer Magnitude { get { return _magnitude; } set { SetMagnitude(value); } }
        public string DistributionName { get { return _distributionName; } set { _distributionName = value; } }


        // Constructors                                                                                                             
        public DLoad(string name, int elementId, FeFaceName elementFaceName, double magnitude,
                     bool twoD, bool complex, double phaseDeg, bool constant = false)
            : this(name, "", RegionTypeEnum.ElementId, magnitude, twoD, complex, phaseDeg, constant)
        {
            _elementId = elementId;
            _elementFaceName = elementFaceName;
        }
        public DLoad(string name, string surfaceName, RegionTypeEnum regionType, double magnitude,
                     bool twoD, bool complex, double phaseDeg, bool constant = false)
            : base(name, surfaceName, regionType, twoD, complex, phaseDeg, constant)
        {
            _elementId = -1;
            _elementFaceName = FeFaceName.Empty;
            Magnitude = new EquationContainer(typeof(StringPressureConverter), magnitude, null, constant);
            _distributionName = Distribution.DefaultDistributionName;
        }
        public DLoad(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "_regionName": //Compatibility for version v2.4.4
                        _surfaceName = (string)entry.Value; break;
                    case "_elementId":
                        _elementId = (int)entry.Value; break;
                    case "_elementFaceName":
                        _elementFaceName = (FeFaceName)entry.Value; break;
                    case "_magnitude":
                        // Compatibility for version v1.4.0
                        if (entry.Value is double valueDouble)
                            Magnitude = new EquationContainer(typeof(StringPressureConverter), valueDouble);
                        else
                            SetMagnitude((EquationContainer)entry.Value, false);
                        break;
                    case "_distributionName":
                        _distributionName = (string)entry.Value; break;
                    default:
                        break;
                }
            }
            // Compatibility for version v2.2.4
            if (_distributionName == null) _distributionName = Distribution.DefaultDistributionName;
        }


        // Methods                                                                                                                  
        private void SetMagnitude(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _magnitude, value, null, checkEquation);
        }
        // IContainsEquations
        public override void CheckEquations()
        {
            base.CheckEquations();
            //
            _magnitude.CheckEquation();
        }
        //
        // ISerialization
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // Using typeof() works also for null fields
            base.GetObjectData(info, context);
            //
            info.AddValue("_elementId", _elementId, typeof(int));
            info.AddValue("_elementFaceName", _elementFaceName, typeof(FeFaceName));
            info.AddValue("_magnitude", _magnitude, typeof(EquationContainer));
            info.AddValue("_distributionName", _distributionName, typeof(string));
        }
    }
}
