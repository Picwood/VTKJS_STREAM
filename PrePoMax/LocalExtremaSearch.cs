// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

namespace PrePoMax
{
    internal enum LocalExtremaSearchType
    {
        Maximum,
        Minimum
    }

    internal class LocalExtremaSearchParameters
    {
        public LocalExtremaSearchType Type { get; set; }
        public double Threshold { get; set; }
        public double Tolerance { get; set; }
        public int MaxPoints { get; set; }
        public bool VisiblePartsOnly { get; set; }
    }

    internal class LocalExtremaSearchResult
    {
        public int NodeId { get; set; }
        public float Value { get; set; }
        public int RegionNodeCount { get; set; }
    }
}
