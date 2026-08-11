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
using DynamicTypeDescriptor;

namespace PrePoMax
{
    [Serializable]
    public class ViewStaticStep : ViewStep
    {
        // Variables                                                                                                                
        private const string CustomInitialTimeIncrement = "Custom...";
        private static readonly KeyValuePair<string, double>[] _initialTimeIncrementPresets =
            new KeyValuePair<string, double>[]
            {
                new KeyValuePair<string, double>("0.02", 0.02),
                new KeyValuePair<string, double>("0.031", 0.031),
                new KeyValuePair<string, double>("0.046", 0.046),
                new KeyValuePair<string, double>("0.071", 0.071),
                new KeyValuePair<string, double>("0.11", 0.11),
                new KeyValuePair<string, double>("0.174", 0.174),
                new KeyValuePair<string, double>("0.286", 0.286),
                new KeyValuePair<string, double>("0.5", 0.5)
            };
        private CaeModel.StaticStep _staticStep;
        private string _initialTimeIncrementPreset;


        // Properties                                                                                                               
        [CategoryAttribute("Data")]
        [OrderedDisplayName(3, 10, "Nlgeom")]
        [DescriptionAttribute("Enable/disable the nonlinear effects of large deformations and large displacements.")]
        [Id(4, 1)]
        public bool Nlgeom { get { return _staticStep.Nlgeom; } set { _staticStep.Nlgeom = value; } }
        //
        [CategoryAttribute("Incrementation")]
        [OrderedDisplayName(0, 10, "Type")]
        [DescriptionAttribute("Select the incrementation type.")]
        [Id(1, 2)]
        public CaeModel.IncrementationTypeEnum IncrementationType
        { 
            get { return _staticStep.IncrementationType; }
            set
            {
                _staticStep.IncrementationType = value;
                _staticStep.Direct = _staticStep.IncrementationType == CaeModel.IncrementationTypeEnum.Direct;
            }
        }
        //
        [CategoryAttribute("Incrementation")]
        [OrderedDisplayName(1, 10, "Direct")]
        [DescriptionAttribute("By using the 'Direct' keyword automatic incrementation of nonlinear step is switched off.")]
        [Id(2, 2)]
        public bool Direct { get { return _staticStep.Direct; } set { _staticStep.Direct = value; } }
        //
        [CategoryAttribute("Incrementation")]
        [OrderedDisplayName(2, 10, "Max increments")]
        [DescriptionAttribute("The maximum number of increments in the step.")]
        [TypeConverter(typeof(StringIntegerConverter))]
        [Id(3, 2)]
        public int MaxIncrements { get { return _staticStep.MaxIncrements; } set { _staticStep.MaxIncrements = value; } }
        //
        [CategoryAttribute("Incrementation")]
        [OrderedDisplayName(3, 10, "Time period")]
        [DescriptionAttribute("Time period of the step.")]
        [TypeConverter(typeof(StringTimeConverter))]
        [Id(4, 2)]
        public double TimePeriod
        {
            get { return _staticStep.TimePeriod; }
            set
            {
                _staticStep.TimePeriod = value;
                SynchronizeInitialTimeIncrementPreset();
            }
        }
        //
        [CategoryAttribute("Incrementation")]
        [OrderedDisplayName(4, 10, "Initial time increment")]
        [DescriptionAttribute("Select an optimized initial time increment or choose Custom to enter another value.")]
        [Id(5, 2)]
        public string InitialTimeIncrementPreset
        {
            get { return _initialTimeIncrementPreset; }
            set
            {
                _initialTimeIncrementPreset = value;
                // Keep the underlying model value unchanged when the custom editor is selected.
                if (value != CustomInitialTimeIncrement)
                {
                    foreach (KeyValuePair<string, double> entry in _initialTimeIncrementPresets)
                    {
                        if (value == entry.Key)
                        {
                            _staticStep.InitialTimeIncrement = entry.Value;
                            SynchronizeInitialTimeIncrementPreset();
                            break;
                        }
                    }
                }
            }
        }
        //
        [CategoryAttribute("Incrementation")]
        [OrderedDisplayName(5, 10, "Custom initial time increment")]
        [DescriptionAttribute("Enter a custom initial time increment of the step.")]
        [TypeConverter(typeof(StringTimeConverter))]
        [Id(6, 2)]
        public double InitialTimeIncrement
        {
            get { return _staticStep.InitialTimeIncrement; }
            set { _staticStep.InitialTimeIncrement = value; }
        }
        //
        [CategoryAttribute("Incrementation")]
        [OrderedDisplayName(6, 10, "Min time increment")]
        [DescriptionAttribute("Minimum time increment allowed.")]
        [TypeConverter(typeof(StringTimeConverter))]
        [Id(7, 2)]
        public double MinTimeIncrement
        {
            get { return _staticStep.MinTimeIncrement; }
            set { _staticStep.MinTimeIncrement = value; }
        }
        //
        [CategoryAttribute("Incrementation")]
        [OrderedDisplayName(7, 10, "Max time increment")]
        [DescriptionAttribute("Maximum time increment allowed.")]
        [TypeConverter(typeof(StringTimeConverter))]
        [Id(8, 2)]
        public double MaxTimeIncrement
        {
            get { return _staticStep.MaxTimeIncrement; }
            set
            {
                _staticStep.MaxTimeIncrement = value;
                SynchronizeInitialTimeIncrementPreset();
            }
        }


        // Constructors                                                                                                             
        public ViewStaticStep(CaeModel.StaticStep step, bool installProvider = true)
            : base(step)
        {
            _staticStep = step;
            _initialTimeIncrementPreset = GetInitialTimeIncrementPreset(step.InitialTimeIncrement);
            //
            if (installProvider)
            {
                InstallProvider();
                UpdateVisibility();
            }
        }


        // Methods
        public override CaeModel.Step GetBase()
        {
            return _staticStep;
        }
        public override void InstallProvider()
        {
            base.InstallProvider();
            //
            _dctd.RenameBooleanPropertyToOnOff(nameof(Nlgeom));
            _dctd.RenameBooleanPropertyToOnOff(nameof(Direct));
            // The optimized presets apply only to the structural Static Step editor.
            if (UsesInitialTimeIncrementPresets())
            {
                List<string> values = new List<string>();
                foreach (KeyValuePair<string, double> entry in _initialTimeIncrementPresets) values.Add(entry.Key);
                values.Add(CustomInitialTimeIncrement);
                _dctd.PopulateProperty(nameof(InitialTimeIncrementPreset), values.ToArray());
            }
        }
        public override void UpdateVisibility()
        {
            base.UpdateVisibility();
            //
            _dctd.GetProperty(nameof(Direct)).SetIsBrowsable(false);
            bool usesPresets = UsesInitialTimeIncrementPresets();
            _dctd.GetProperty(nameof(InitialTimeIncrementPreset)).SetIsBrowsable(false);
            _dctd.GetProperty(nameof(InitialTimeIncrement)).SetDisplayName("Initial time increment");
            //
            if (_staticStep.IncrementationType == CaeModel.IncrementationTypeEnum.Default)
            {
                _dctd.GetProperty(nameof(TimePeriod)).SetIsBrowsable(false);
                _dctd.GetProperty(nameof(MaxIncrements)).SetIsBrowsable(false);
                _dctd.GetProperty(nameof(InitialTimeIncrement)).SetIsBrowsable(false);
                _dctd.GetProperty(nameof(MinTimeIncrement)).SetIsBrowsable(false);
                _dctd.GetProperty(nameof(MaxTimeIncrement)).SetIsBrowsable(false);
            }
            else if (_staticStep.IncrementationType == CaeModel.IncrementationTypeEnum.Automatic)
            {
                _dctd.GetProperty(nameof(TimePeriod)).SetIsBrowsable(true);
                _dctd.GetProperty(nameof(MaxIncrements)).SetIsBrowsable(true);
                _dctd.GetProperty(nameof(InitialTimeIncrementPreset)).SetIsBrowsable(usesPresets);
                _dctd.GetProperty(nameof(InitialTimeIncrement)).SetIsBrowsable(!usesPresets ||
                    _initialTimeIncrementPreset == CustomInitialTimeIncrement);
                if (usesPresets)
                    _dctd.GetProperty(nameof(InitialTimeIncrement)).SetDisplayName("Custom initial time increment");
                _dctd.GetProperty(nameof(MinTimeIncrement)).SetIsBrowsable(true);
                _dctd.GetProperty(nameof(MaxTimeIncrement)).SetIsBrowsable(true);
            }
            else if (_staticStep.IncrementationType == CaeModel.IncrementationTypeEnum.Direct)
            {
                _dctd.GetProperty(nameof(TimePeriod)).SetIsBrowsable(true);
                _dctd.GetProperty(nameof(MaxIncrements)).SetIsBrowsable(true);
                _dctd.GetProperty(nameof(InitialTimeIncrement)).SetIsBrowsable(true);
                _dctd.GetProperty(nameof(MinTimeIncrement)).SetIsBrowsable(false);
                _dctd.GetProperty(nameof(MaxTimeIncrement)).SetIsBrowsable(false);
            }
            else throw new NotSupportedException();
        }
        private bool UsesInitialTimeIncrementPresets()
        {
            return _staticStep.GetType() == typeof(CaeModel.StaticStep);
        }
        private static string GetInitialTimeIncrementPreset(double value)
        {
            foreach (KeyValuePair<string, double> entry in _initialTimeIncrementPresets)
            {
                if (value == entry.Value) return entry.Key;
            }
            return CustomInitialTimeIncrement;
        }
        private void SynchronizeInitialTimeIncrementPreset()
        {
            if (UsesInitialTimeIncrementPresets())
                _initialTimeIncrementPreset = GetInitialTimeIncrementPreset(_staticStep.InitialTimeIncrement);
        }

    }
}
