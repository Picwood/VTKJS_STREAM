using System.Security.Cryptography;
using CaeMesh;
using CaeModel;

namespace PrePoMax.Persistence;

/// <summary>Validates topology-specific sizing before opening or publishing an editable project.</summary>
public static class CadFaceSizingBinding
{
    public static bool SupportsSource(string fileName) =>
        new[] { ".step", ".stp", ".iges", ".igs", ".brep" }.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    public static string Fingerprint(ProjectCadTopology topology, byte[] source) =>
        Convert.ToHexString(SHA256.HashData(CadTopologyCodec.Serialize(topology, source)));

    public static void Validate(FeModel model, ProjectGeometrySource? geometry)
    {
        foreach (var setup in model.Mesh.MeshSetupItems.Values.OfType<CadFaceMeshSize>())
        {
            if (geometry?.Topology is not { } topology || !SupportsSource(geometry.FileName) ||
                !setup.IsProperlyDefined() || setup.SourceSha256 != topology.SourceSha256 ||
                setup.TopologySha256 != Fingerprint(topology, geometry.Data) ||
                !topology.Entities.Any(entity => entity.Dimension == 2 && entity.Tag == setup.FaceTag))
                throw new InvalidDataException("CAD face sizing does not match the retained source and topology. Select the face again in a freshly captured CAD project.");
        }
    }
}
