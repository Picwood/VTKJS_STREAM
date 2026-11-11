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
using System.ComponentModel;
using DynamicTypeDescriptor;
using CaeGlobals;
using System.Runtime.Serialization;

namespace CaeModel
{
    [Serializable]
    public enum DefinedTemperatureTypeEnum
    {
        [StandardValue("ByValue", DisplayName = "By value")]
        ByValue,
        [StandardValue("FromFile", DisplayName = "From file")]
        FromFile
    }
    //
    [Serializable]
                
    public partial class DefinedTemperature : DefinedField, IDistribution, ISerializable
    {
        // Variables                                                                                                                
        private DefinedTemperatureTypeEnum _definedTemperatureType;     //ISerializable
        private int _nodeId;                                            //ISerializable
        private EquationContainer _temperature;                         //ISerializable
        private string _fileName;                                       //ISerializable
        private int _stepNumber;                                        //ISerializable
        private string _distributionName;                               //ISerializable


        // Properties                                                                                                               
        public DefinedTemperatureTypeEnum Type { get { return _definedTemperatureType; } set { _definedTemperatureType = value; } }
        public int NodeId { get { return _nodeId; } set { _nodeId = value; } }
        public EquationContainer Temperature { get { return _temperature; } set { SetTemp(value); } }
        public string FileName { get { return _fileName; } set { _fileName = value; } }
        public int StepNumber
        {
            get { return _stepNumber; }
            set
            {
                _stepNumber = value;
                if (_stepNumber < 1) _stepNumber = 1;
            }
        }
        public string DistributionName { get { return _distributionName; } set { _distributionName = value; } }


        // Constructors                                                                                                             
        public DefinedTemperature(string name, int nodeId, double temperature, bool constant = false)
           : this(name, "", RegionTypeEnum.NodeId, temperature, constant)
        {
            _nodeId = nodeId;
        }
        public DefinedTemperature(string name, string regionName, RegionTypeEnum regionType, double temperature,
                                  bool constant = false)
            : base(name, regionName, regionType)
        {
            _definedTemperatureType = DefinedTemperatureTypeEnum.ByValue;
            _nodeId = -1;
            Temperature = new EquationContainer(typeof(StringTemperatureConverter), temperature, null, constant);
            _fileName = null;
            _stepNumber = 1;
            _distributionName = Distribution.DefaultDistributionName;
        }
        public DefinedTemperature(SerializationInfo info, StreamingContext context)
           : base(info, context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "_definedTemperatureType":
                        _definedTemperatureType = (DefinedTemperatureTypeEnum)entry.Value; break;
                    case "_nodeId":
                        _nodeId = (int)entry.Value; break;
                    case "_temperature":
                        // Compatibility for version v2.2.3
                        if (entry.Value is double valueT)
                            Temperature = new EquationContainer(typeof(StringTemperatureConverter), valueT);
                        else
                            SetTemp((EquationContainer)entry.Value, false);
                        break;
                    case "_fileName":
                        _fileName = (string)entry.Value; break;
                    case "_stepNumber":
                        _stepNumber = (int)entry.Value; break;
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
        private void SetTemp(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _temperature, value, null, null, checkEquation);
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
            info.AddValue("_definedTemperatureType", _definedTemperatureType, typeof(DefinedTemperatureTypeEnum));
            info.AddValue("_nodeId", _nodeId, typeof(int));
            info.AddValue("_temperature", _temperature, typeof(EquationContainer));
            info.AddValue("_fileName", _fileName, typeof(string));
            info.AddValue("_stepNumber", _stepNumber, typeof(int));
            info.AddValue("_distributionName", _distributionName, typeof(string));
        }
    }
}
