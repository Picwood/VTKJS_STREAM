using System;
using System.Runtime.Serialization;

namespace CaeMesh
{
    /// <summary>A face refinement bound to one retained CAD source and topology snapshot.</summary>
    [Serializable]
    public sealed class CadFaceMeshSize : MeshSetupItem, ISerializable
    {
        public int FaceTag { get; set; }
        public double MeshSize { get; set; }
        public double TransitionDistance { get; set; }
        public string SourceSha256 { get; set; }
        public string TopologySha256 { get; set; }
        public CadFaceMeshSize(string name) : base(name) { Reset(); }
        public CadFaceMeshSize(CadFaceMeshSize source) : base(source)
        {
            FaceTag = source.FaceTag; MeshSize = source.MeshSize; TransitionDistance = source.TransitionDistance;
            SourceSha256 = source.SourceSha256; TopologySha256 = source.TopologySha256;
        }
        private CadFaceMeshSize(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            FaceTag = info.GetInt32(nameof(FaceTag)); MeshSize = info.GetDouble(nameof(MeshSize));
            TransitionDistance = info.GetDouble(nameof(TransitionDistance));
            SourceSha256 = info.GetString(nameof(SourceSha256)); TopologySha256 = info.GetString(nameof(TopologySha256));
        }
        public override void Reset()
        {
            base.Reset(); FaceTag = 0; MeshSize = 1; TransitionDistance = 1;
            SourceSha256 = TopologySha256 = "";
        }
        public bool IsProperlyDefined()
        {
            return FaceTag > 0 && Finite(MeshSize) && MeshSize > 0 &&
                Finite(TransitionDistance) && TransitionDistance > 0 && Hash(SourceSha256) && Hash(TopologySha256);
        }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static bool Hash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char character in value) if ("0123456789ABCDEF".IndexOf(character) < 0) return false;
            return true;
        }
        public CadFaceMeshSize DeepCopy() { return new CadFaceMeshSize(this); }
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(FaceTag), FaceTag); info.AddValue(nameof(MeshSize), MeshSize);
            info.AddValue(nameof(TransitionDistance), TransitionDistance);
            info.AddValue(nameof(SourceSha256), SourceSha256); info.AddValue(nameof(TopologySha256), TopologySha256);
        }
    }
}
