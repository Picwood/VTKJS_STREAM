using System;
using System.Runtime.Serialization;

namespace CaeMesh
{
    /// <summary>A coordinate-defined local sizing region, independent of CAD entity labels.</summary>
    [Serializable]
    public sealed class BoxMeshSize : MeshSetupItem, ISerializable
    {
        public double MeshSize { get; set; }
        public double XMin { get; set; }
        public double YMin { get; set; }
        public double ZMin { get; set; }
        public double XMax { get; set; }
        public double YMax { get; set; }
        public double ZMax { get; set; }

        public BoxMeshSize(string name) : base(name) { Reset(); }
        public BoxMeshSize(BoxMeshSize source) : base(source)
        {
            MeshSize = source.MeshSize;
            XMin = source.XMin; YMin = source.YMin; ZMin = source.ZMin;
            XMax = source.XMax; YMax = source.YMax; ZMax = source.ZMax;
        }
        private BoxMeshSize(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            MeshSize = info.GetDouble(nameof(MeshSize));
            XMin = info.GetDouble(nameof(XMin)); YMin = info.GetDouble(nameof(YMin)); ZMin = info.GetDouble(nameof(ZMin));
            XMax = info.GetDouble(nameof(XMax)); YMax = info.GetDouble(nameof(YMax)); ZMax = info.GetDouble(nameof(ZMax));
        }
        public override void Reset()
        {
            base.Reset();
            MeshSize = 1;
            XMin = YMin = ZMin = 0;
            XMax = YMax = ZMax = 1;
        }
        public bool IsProperlyDefined()
        {
            return Finite(MeshSize) && MeshSize > 0 &&
                Finite(XMin) && Finite(YMin) && Finite(ZMin) &&
                Finite(XMax) && Finite(YMax) && Finite(ZMax) &&
                XMin < XMax && YMin < YMax && ZMin < ZMax;
        }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        public BoxMeshSize DeepCopy() { return new BoxMeshSize(this); }
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(MeshSize), MeshSize);
            info.AddValue(nameof(XMin), XMin); info.AddValue(nameof(YMin), YMin); info.AddValue(nameof(ZMin), ZMin);
            info.AddValue(nameof(XMax), XMax); info.AddValue(nameof(YMax), YMax); info.AddValue(nameof(ZMax), ZMax);
        }
    }
}
