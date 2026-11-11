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
using System.Security.Cryptography;
using System.IO;

namespace CaeResults
{
    [Serializable]
    public class FieldComponent : NamedClass
    {
        // Variables                                                                                                                
        public float[] Values;
        public bool Invariant;
        public IDValuePair Max;
        public IDValuePair Min;

        // Constructor                                                                                                              
        public FieldComponent(string name, float[] values, bool invariant = false)
            : base()
        {
            _checkName = false;
            Name = name;
            //
            Values = values;
            //
            Invariant = invariant;
            //
            UpdateMaxMin();
        }
        public FieldComponent(FieldComponent component)
            : base(component)
        {
            if (component.Values != null) Values = component.Values.ToArray();
            Invariant = component.Invariant;
            Max = component.Max;
            Min = component.Min;
        }

        
        // Static methods                                                                                                           
        public static void WriteToBinaryWriter(FieldComponent component, System.IO.BinaryWriter bw)
        {
            bw.Write(component.Name);
            bw.Write(component.Invariant);
            bw.Write(component.Values.Length);
            for (int i = 0; i < component.Values.Length; i++) bw.Write(component.Values[i]);
        }
        public static FieldComponent ReadFromBinaryReader(System.IO.BinaryReader br, int version)
        {
            string name = br.ReadString();
            bool invariant = br.ReadBoolean();
            int numValues = br.ReadInt32();
            if (numValues < 0 || numValues > 100_000_000)
                throw new InvalidDataException("Invalid result component value count.");
            //
            float[] values = new float[numValues];
            for (int i = 0; i < numValues; i++) values[i] = br.ReadSingle();
            //
            return new FieldComponent(name, values, invariant);
        }
        public void UpdateMaxMin()
        {
            // Missing samples must not poison an otherwise valid field range.
            // An unavailable range has no corresponding sample index.
            Max = new IDValuePair { Id = -1, Value = float.NaN };
            Min = new IDValuePair { Id = -1, Value = float.NaN };
            if (Values == null) return;
            for (int i = 0; i < Values.Length; i++)
            {
                float value = Values[i];
                if (float.IsNaN(value)) continue;
                if (Max.Id == -1 || value > Max.Value)
                    Max = new IDValuePair { Id = i, Value = value };
                if (Min.Id == -1 || value < Min.Value)
                    Min = new IDValuePair { Id = i, Value = value };
            }
        }

        public void SetValuesToZero()
        {
            SetValuesTo(0);
        }
        public void SetValuesTo(float value)
        {
            for (int i = 0; i < Values.Length; i++) Values[i] = value;
            UpdateMaxMin();
        }

    }
}
