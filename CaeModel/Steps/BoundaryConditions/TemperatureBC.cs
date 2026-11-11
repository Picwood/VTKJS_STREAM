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

namespace CaeModel
{    
    [Serializable]
    public partial class TemperatureBC : BoundaryCondition, IDistribution, ISerializable
    {
        // Variables                                                                                                                
        private int _nodeId;                            //ISerializable
        private EquationContainer _temperature;         //ISerializable
        private string _distributionName;               //ISerializable


        // Properties                                                                                                               
        public int NodeId { get { return _nodeId; } set { _nodeId = value; } }
        public EquationContainer Temperature { get { return _temperature; } set { SetTemperature(value); } }
        public string DistributionName { get { return _distributionName; } set { _distributionName = value; } }


        // Constructors                                                                                                             
        public TemperatureBC(string name, int nodeId, double temperature, bool constant = false)
           : this(name, "", RegionTypeEnum.NodeId, temperature, constant)
        {
            _nodeId = nodeId;
        }
        public TemperatureBC(string name, string regionName, RegionTypeEnum regionType, double temperature, bool twoD,
                             bool constant = false)
            : base(name, regionName, regionType, twoD, false, 0, constant) 
        {
            _nodeId = -1;
            Temperature = new EquationContainer(typeof(StringTemperatureConverter), temperature, null, constant);
            _distributionName = Distribution.DefaultDistributionName;
        }
        public TemperatureBC(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "_nodeId":
                        _nodeId = (int)entry.Value; break;
                    case "_temperature":
                        // Compatibility for version v1.4.0
                        if (entry.Value is double value)
                            Temperature = new EquationContainer(typeof(StringTemperatureConverter), value);
                        else
                            SetTemperature((EquationContainer)entry.Value, false);
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
        private void SetTemperature(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _temperature, value, null, checkEquation);
        }
        // IContainsEquations
        public override void CheckEquations()
        {
            base.CheckEquations();
            //
            _temperature.CheckEquation();
        }
        // IPreviewable
        // ISerialization
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // Using typeof() works also for null fields
            base.GetObjectData(info, context);
            //
            info.AddValue("_nodeId", _nodeId, typeof(int));
            info.AddValue("_temperature", _temperature, typeof(EquationContainer));
            info.AddValue("_distributionName", _distributionName, typeof(string));
        }
    }
}
