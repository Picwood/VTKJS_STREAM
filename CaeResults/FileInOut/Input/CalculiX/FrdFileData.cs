using System;
using System.Collections.Generic;
using CaeGlobals;
using CaeMesh;

namespace CaeResults
{
    /// <summary>FRD mesh and raw fields, before application result preprocessing.</summary>
    public sealed class FrdFileData
    {
        public string HashName { get; internal set; }
        public DateTime DateTime { get; internal set; }
        public UnitSystem UnitSystem { get; internal set; }
        public FeMesh Mesh { get; internal set; }
        public Dictionary<int, int> NodeIdsLookUp { get; internal set; }
        public List<KeyValuePair<FieldData, Field>> Fields { get; } = new List<KeyValuePair<FieldData, Field>>();
    }
}
