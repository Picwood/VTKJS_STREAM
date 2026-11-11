// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;

namespace CaeGlobals
{
    public static partial class ExtensionMethods
    {
        public static string GetDisplayedName<T>(this T enumerationValue) where T : struct
        {
            Type type = enumerationValue.GetType();
            if (!type.IsEnum)
            {
                throw new ArgumentException("EnumerationValue must be of Enum type", "enumerationValue");
            }

            //Tries to find a DescriptionAttribute for a potential friendly name
            //for the enum
            System.Reflection.MemberInfo[] memberInfo = type.GetMember(enumerationValue.ToString());
            if (memberInfo != null && memberInfo.Length > 0)
            {
                object[] attrs = memberInfo[0].GetCustomAttributes(typeof(DynamicTypeDescriptor.StandardValueAttribute), false);

                if (attrs != null && attrs.Length > 0)
                {
                    //Pull out the description value
                    return ((DynamicTypeDescriptor.StandardValueAttribute)attrs[0]).DisplayName;
                }
            }
            //If we have no description attribute, just return the ToString of the enum
            return enumerationValue.ToString();
        }
        public static string ToCalculiX16String(this double value, bool enforceSeparator = false)
        {
            string result = value.ToString(System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
            if (result.Length > 16) result = value.ToString("E8", System.Globalization.CultureInfo.InvariantCulture);
            else if (enforceSeparator && !result.Contains("."))
            {
                int location = result.IndexOf("E");
                // The first letter must not be E so > 0
                if (location > 0) result = result.Insert(location, ".");
                else result += ".";
            }
            return result;
        }
        // Float Array
        public static float[] ToFloat(this double[] arr) => Array.ConvertAll(arr, x => (float)x);
        // Double Array
        public static double[] ToDouble(this bool[] arr) => Array.ConvertAll(arr, x => x == true ? 1d : 0d);
        public static double[] ToDouble(this byte[] arr) => Array.ConvertAll(arr, x => (double)x);
        public static double[] ToDouble(this decimal[] arr) => Array.ConvertAll(arr, x => (double)x);
        public static double[] ToDouble(this int[] arr) => Array.ConvertAll(arr, x => (double)x);
        public static double[] ToDouble(this float[] arr) => Array.ConvertAll(arr, x => (double)x);
        //
        public static double[] ToFlatArray(this double[][] arr)
        {
            int index = 0;
            double[] result = new double[arr.Length * arr[0].Length];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i].CopyTo(result, index);
                index += arr[i].Length;
            }
            return result;
        }
        public static double[][] ToJaggedArray(this double[] arr, int numRows)
        {
            int numCol = arr.Length / numRows;
            double[][] result = new double[numRows][];
            for (int i = 0; i < numRows; i++)
            {
                result[i] = new double[numCol];
                Array.Copy(arr, i * numCol, result[i], 0, numCol);
            }
            return result;
        }
    }
}
