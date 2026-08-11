// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinsek
// Contributors:

using System;

namespace PrePoMax
{
    public class ContactFaceAreaStatistics
    {
        public int Count { get; set; }
        public double Average { get; set; }
        public double StandardDeviation { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double CoefficientOfVariation
        {
            get
            {
                if (Average == 0) return double.NaN;
                return StandardDeviation / Average;
            }
        }
    }

    public class ContactPairValidationResult
    {
        public string ContactPairName { get; set; }
        public ContactFaceAreaStatistics Master { get; set; }
        public ContactFaceAreaStatistics Slave { get; set; }
        public bool MasterAverageAreaOk { get; set; }
        public bool MasterVariationOk { get; set; }
        public bool SlaveVariationOk { get; set; }
        public string Error { get; set; }
        public string Warning { get; set; }
        public bool Passed
        {
            get
            {
                return Error == null && MasterAverageAreaOk && MasterVariationOk && SlaveVariationOk;
            }
        }
    }
}
