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
using System.IO;
using System.Runtime.InteropServices;

namespace CaeModel
{
    [Serializable]
    public partial class STLoad : Load, IDistribution, ISerializable
    {
        // Variables                                                                                                                
        private string _surfaceName;            //ISerializable
        private RegionTypeEnum _regionType;     //ISerializable
        private EquationContainer _f1;          //ISerializable
        private EquationContainer _f2;          //ISerializable
        private EquationContainer _f3;          //ISerializable
        private EquationContainer _fMagnitude;  //ISerializable
        private EquationContainer _p1;          //ISerializable
        private EquationContainer _p2;          //ISerializable
        private EquationContainer _p3;          //ISerializable
        private EquationContainer _pMagnitude;  //ISerializable
        private string _distributionName;       //ISerializable


        // Properties                                                                                                               
        public override string RegionName { get { return _surfaceName; } set { _surfaceName = value; } }
        public override RegionTypeEnum RegionType { get { return _regionType; } set { _regionType = value; } }
        public string SurfaceName { get { return _surfaceName; } set { _surfaceName = value; } }
        public EquationContainer F1 { get { UpdateEquations(); return _f1; } set { SetF1(value); } }
        public EquationContainer F2 { get { UpdateEquations(); return _f2; } set { SetF2(value); } }
        public EquationContainer F3 { get { UpdateEquations(); return _f3; } set { SetF3(value); } }
        public EquationContainer FMagnitude { get { UpdateEquations(); return _fMagnitude; } set { SetFMagnitude(value); } }
        public EquationContainer P1 { get { UpdateEquations(); return _p1; } set { SetP1(value); } }
        public EquationContainer P2 { get { UpdateEquations(); return _p2; } set { SetP2(value); } }
        public EquationContainer P3 { get { UpdateEquations(); return _p3; } set { SetP3(value); } }
        public EquationContainer PMagnitude { get { UpdateEquations(); return _pMagnitude; } set { SetPMagnitude(value); } }
        public string DistributionName { get { return _distributionName; } set { _distributionName = value; } }


        // Constructors                                                                                                             
        public STLoad(string name, string surfaceName, RegionTypeEnum regionType, double f1, double f2, double f3, bool twoD,
                      bool complex, double phaseDeg)
            : base(name, twoD, complex, phaseDeg)
        {
            _surfaceName = surfaceName;
            _regionType = regionType;
            //
            double mag = Math.Sqrt(f1 * f1 + f2 * f2 + f3 * f3);
            F1 = new EquationContainer(typeof(StringForceConverter), f1);
            F2 = new EquationContainer(typeof(StringForceConverter), f2);
            F3 = new EquationContainer(typeof(StringForceConverter), f3);
            FMagnitude = new EquationContainer(typeof(StringForceConverter), mag);
            //
            P1 = new EquationContainer(typeof(StringPressureConverter), 0);
            P2 = new EquationContainer(typeof(StringPressureConverter), 0);
            P3 = new EquationContainer(typeof(StringPressureConverter), 0);
            PMagnitude = new EquationContainer(typeof(StringPressureConverter), 0);
            //
            _distributionName = Distribution.DefaultDistributionName;
        }
        public STLoad(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case "_surfaceName":
                        _surfaceName = (string)entry.Value; break;
                    case "_regionType":
                        _regionType = (RegionTypeEnum)entry.Value; break;
                    case "_f1":
                        // Compatibility for version v1.4.0
                        if (entry.Value is double valueF1)
                            F1 = new EquationContainer(typeof(StringForceConverter), valueF1);
                        else
                            SetF1((EquationContainer)entry.Value, false);
                        break;
                    case "_f2":
                        // Compatibility for version v1.4.0
                        if (entry.Value is double valueF2)
                            F2 = new EquationContainer(typeof(StringForceConverter), valueF2);
                        else
                            SetF2((EquationContainer)entry.Value, false);
                        break;
                    case "_f3":
                        // Compatibility for version v1.4.0
                        if (entry.Value is double valueF3)
                            F3 = new EquationContainer(typeof(StringForceConverter), valueF3);
                        else
                            SetF3((EquationContainer)entry.Value, false);
                        break;
                    case "_fMagnitude":
                    case "_magnitude":  // Compatibility for version v2.2.4
                        SetFMagnitude((EquationContainer)entry.Value, false); break;
                    case "_p1":
                        SetP1((EquationContainer)entry.Value, false);
                        break;
                    case "_p2":
                        SetP2((EquationContainer)entry.Value, false);
                        break;
                    case "_p3":
                        SetP3((EquationContainer)entry.Value, false);
                        break;
                    case "_pMagnitude":
                        SetPMagnitude((EquationContainer)entry.Value, false); break;
                    case "_distributionName":
                        _distributionName = (string)entry.Value; break;
                    default:
                        break;
                }
            }
            // Compatibility for version v1.4.0
            if (_fMagnitude == null)
            {
                double mag = Math.Sqrt(_f1.Value * _f1.Value + _f2.Value * _f2.Value + _f3.Value * _f3.Value);
                FMagnitude = new EquationContainer(typeof(StringForceConverter), mag);
            }
            // Compatibility for version v2.2.4
            if (_distributionName == null) _distributionName = Distribution.DefaultDistributionName;
            if (_p1 == null) P1 = new EquationContainer(typeof(StringPressureConverter), 0);
            if (_p2 == null) P2 = new EquationContainer(typeof(StringPressureConverter), 0);
            if (_p3 == null) P3 = new EquationContainer(typeof(StringPressureConverter), 0);
            if (_pMagnitude == null) PMagnitude = new EquationContainer(typeof(StringPressureConverter), 0);
        }


        // Methods                                                                                                                  
        private void UpdateEquations()
        {
            try
            {
                // If error catch it silently
                if (_f1.IsEquation() || _f2.IsEquation() || _f3.IsEquation()) FEquationChanged();
                else if (_fMagnitude.IsEquation()) FMagnitudeEquationChanged();
                //
                if (_p1.IsEquation() || _p2.IsEquation() || _p3.IsEquation()) PEquationChanged();
                else if (_pMagnitude.IsEquation()) PMagnitudeEquationChanged();
            }
            catch (Exception ex) { }
        }
        private void SetF1(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _f1, value, null, FEquationChanged, checkEquation);
        }
        private void SetF2(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _f2, value, null, FEquationChanged, checkEquation);
        }
        private void SetF3(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _f3, value, Check2D, FEquationChanged, checkEquation);
        }
        private void SetFMagnitude(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _fMagnitude, value, CheckMagnitude, FMagnitudeEquationChanged, checkEquation);
        }
        //
        private void SetP1(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _p1, value, null, PEquationChanged, checkEquation);
        }
        private void SetP2(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _p2, value, null, PEquationChanged, checkEquation);
        }
        private void SetP3(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _p3, value, Check2D, PEquationChanged, checkEquation);
        }
        private void SetPMagnitude(EquationContainer value, bool checkEquation = true)
        {
            EquationContainer.SetAndCheck(ref _pMagnitude, value, CheckMagnitude, PMagnitudeEquationChanged, checkEquation);
        }
        //
        private void FEquationChanged()
        {
            double mag = Math.Sqrt(_f1.Value * _f1.Value + _f2.Value * _f2.Value + _f3.Value * _f3.Value);
            _fMagnitude.SetEquationFromValue(mag, false);
        }
        private void PEquationChanged()
        {
            double mag = Math.Sqrt(_p1.Value * _p1.Value + _p2.Value * _p2.Value + _p3.Value * _p3.Value);
            _pMagnitude.SetEquationFromValue(mag, false);
        }
        private void FMagnitudeEquationChanged()
        {
            double c1;
            double c2;
            double c3;
            //
            if (_fMagnitude.Value == 0)
            {
                c1 = 0;
                c2 = 0;
                c3 = 0;
            }
            else
            {
                double r;
                c1 = _f1.Value;
                c2 = _f2.Value;
                c3 = _f3.Value;
                double mag = Math.Sqrt(c1 * c1 + c2 * c2 + c3 * c3);
                //
                if (mag != 0)
                {
                    r = _fMagnitude.Value / mag;
                    c1 *= r;
                    c2 *= r;
                    c3 *= r;
                }
                else
                {
                    r = Math.Sqrt(Math.Pow(_fMagnitude.Value, 2) / 3);
                    c1 = r;
                    c2 = r;
                    c3 = r;
                }
            }
            //
            _f1.SetEquationFromValue(c1, false);
            _f2.SetEquationFromValue(c2, false);
            _f3.SetEquationFromValue(c3, false);
        }
        private void PMagnitudeEquationChanged()
        {
            double c1;
            double c2;
            double c3;
            //
            if (_pMagnitude.Value == 0)
            {
                c1 = 0;
                c2 = 0;
                c3 = 0;
            }
            else
            {
                double r;
                c1 = _p1.Value;
                c2 = _p2.Value;
                c3 = _p3.Value;
                double mag = Math.Sqrt(c1 * c1 + c2 * c2 + c3 * c3);
                //
                if (mag != 0)
                {
                    r = _pMagnitude.Value / mag;
                    c1 *= r;
                    c2 *= r;
                    c3 *= r;
                }
                else
                {
                    r = Math.Sqrt(Math.Pow(_pMagnitude.Value, 2) / 3);
                    c1 = r;
                    c2 = r;
                    c3 = r;
                }
            }
            //
            _p1.SetEquationFromValue(c1, false);
            _p2.SetEquationFromValue(c2, false);
            _p3.SetEquationFromValue(c3, false);
        }
        //
        private double Check2D(double value)
        {
            if (_twoD) return 0;
            else return value;
        }
        private double CheckMagnitude(double value)
        {
            if (value < 0) throw new Exception("Value of the force load magnitude must be non-negative.");
            else return value;
        }
        public double[] GetDirection()
        {
            double d1;
            double d2;
            double d3;
            if (_distributionName == Distribution.DefaultDistributionName)
            {
                d1 = _f1.Value;
                d2 = _f2.Value;
                d3 = _f3.Value;
            }
            else
            {
                d1 = _p1.Value;
                d2 = _p2.Value;
                d3 = _p3.Value;
            }
            return new double[] { d1, d2, d3 };
        }
        // IContainsEquations
        public override void CheckEquations()
        {
            base.CheckEquations();
            //
            _f1.CheckEquation();
            _f2.CheckEquation();
            _f3.CheckEquation();
            _fMagnitude.CheckEquation();
            //
            _p1.CheckEquation();
            _p2.CheckEquation();
            _p3.CheckEquation();
            _pMagnitude.CheckEquation();
        }
        // IPreviewable
        // ISerialization
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // Using typeof() works also for null fields
            base.GetObjectData(info, context);
            //
            info.AddValue("_surfaceName", _surfaceName, typeof(string));
            info.AddValue("_regionType", _regionType, typeof(RegionTypeEnum));
            info.AddValue("_f1", _f1, typeof(EquationContainer));
            info.AddValue("_f2", _f2, typeof(EquationContainer));
            info.AddValue("_f3", _f3, typeof(EquationContainer));
            info.AddValue("_fMagnitude", _fMagnitude, typeof(EquationContainer));
            info.AddValue("_p1", _p1, typeof(EquationContainer));
            info.AddValue("_p2", _p2, typeof(EquationContainer));
            info.AddValue("_p3", _p3, typeof(EquationContainer));
            info.AddValue("_pMagnitude", _pMagnitude, typeof(EquationContainer));
            info.AddValue("_distributionName", _distributionName, typeof(string));
        }
    }
}
