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
    public static partial class Tools
    {
        // Rounding
        public static double RoundToSignificantDigits(double d, int digits)
        {
            //double[] stdValues = new double[] { 0.1, 0.2, 0.25, 0.5 };
            if (d == 0.0)
            {
                return 0.0;
            }
            else
            {
                double leftSideNumbers = Math.Floor(Math.Log10(Math.Abs(d))) + 1;
                double scale = Math.Pow(10, leftSideNumbers);
                double result = scale * Math.Round(d / scale, digits, MidpointRounding.AwayFromZero);
                // Clean possible precision error.
                if ((int)leftSideNumbers >= digits)
                {
                    return Math.Round(result, 0, MidpointRounding.AwayFromZero);
                }
                else
                {
                    int roundingDigits = digits - (int)leftSideNumbers;
                    roundingDigits = Math.Max(0, Math.Min(roundingDigits, 15));
                    return Math.Round(result, roundingDigits, MidpointRounding.AwayFromZero);
                }
            }
        }
    }
}
