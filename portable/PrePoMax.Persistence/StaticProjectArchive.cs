using System.Text;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;

namespace PrePoMax.Persistence;

public sealed record ProjectGeometrySource(string FileName, byte[] Data, ProjectCadTopology? Topology = null);
public sealed record ReopenedStaticProject(FeModel Model, byte[]? Frd, byte[]? Dat, byte[]? BinaryResults,
                                           ProjectGeometrySource? GeometrySource,
                                           ProjectMaterialIdentity[] MaterialIdentities);

public static class StaticProjectArchive
{
    /// <summary>Resave an opened project while preserving entity IDs and retained artifacts.</summary>
    public static void Save(string path, ReopenedStaticProject project)
        => Save(path, project.Model, project.Frd, project.Dat, project.BinaryResults, project.GeometrySource,
            ProjectMaterialIdentities.Reconcile(project.Model, project.MaterialIdentities));

    /// <summary>Save a model. New models get new IDs; callers resaving an opened project should
    /// use the project overload or supply its identity map explicitly.</summary>
    public static void Save(string path, FeModel model, byte[]? frd = null, byte[]? dat = null,
                            byte[]? binaryResults = null, ProjectGeometrySource? geometrySource = null,
                            ProjectMaterialIdentity[]? materialIdentities = null)
    {
        CadFaceSizingBinding.Validate(model, geometrySource);
        var archive = StaticModelMetadata.SerializeArchive(model);
        byte[] identities = ProjectMaterialIdentities.Serialize(materialIdentities ?? ProjectMaterialIdentities.Reconcile(model, []), model);
        byte[] input = Encoding.UTF8.GetBytes(CalculixFileWriter.GetInputText(model, ConvertPyramidsToEnum.Wedges));
        AnalysisArchive.Save(path, new AnalysisSnapshot(model.Name, input, frd, dat, archive.Metadata, archive.Mesh,
            binaryResults, geometrySource?.Data, geometrySource?.FileName,
            geometrySource?.Topology is { } topology ? CadTopologyCodec.Serialize(topology, geometrySource.Data) : null, identities));
    }

    public static ReopenedStaticProject Open(string path)
    {
        var snapshot = AnalysisArchive.Open(path);
        if (snapshot.ModelMetadata == null) throw new InvalidDataException("Archive contains no editable model metadata.");
        var model = StaticModelMetadata.Deserialize(snapshot.ModelMetadata, snapshot.ModelMesh);
        string generated = CalculixFileWriter.GetInputText(model, ConvertPyramidsToEnum.Wedges);
        if (snapshot.Name != model.Name || StaticModelMetadata.CanonicalInput(generated) != StaticModelMetadata.CanonicalInput(new UTF8Encoding(false, true).GetString(snapshot.Input)))
            throw new InvalidDataException("Editable metadata does not match archived solver input.");
        ProjectGeometrySource? geometry = snapshot.GeometrySource == null ? null :
            new(snapshot.GeometryFileName!, snapshot.GeometrySource, snapshot.GeometryTopology == null ? null :
                CadTopologyCodec.Deserialize(snapshot.GeometryTopology, snapshot.GeometrySource));
        CadFaceSizingBinding.Validate(model, geometry);
        var identities = snapshot.MaterialIdentities == null
            ? ProjectMaterialIdentities.FromLegacy(snapshot.ModelMetadata, snapshot.ModelMesh, model)
            : ProjectMaterialIdentities.Deserialize(snapshot.MaterialIdentities, model);
        return new ReopenedStaticProject(model, snapshot.Frd, snapshot.Dat, snapshot.BinaryResults, geometry, identities);
    }

    /// <summary>Validates the full archive while retaining only payloads required for geometry publication.</summary>
    public static FeModel OpenModelForScene(string path)
    {
        var description = AnalysisArchive.Describe(path);
        if (!description.Payloads.Contains("model.mesh", StringComparer.Ordinal)) return Open(path).Model;
        string directory = Path.Combine(Path.GetTempPath(), "prepomax-scene-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var metadata = new MemoryStream();
            using var identities = new MemoryStream();
            string meshPath = Path.Combine(directory, "model.mesh");
            using (var meshOutput = new FileStream(meshPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var destinations = new Dictionary<string, Stream>(StringComparer.Ordinal)
                {
                    ["model.json"] = metadata,
                    ["model.mesh"] = meshOutput
                };
                if (description.Payloads.Contains("material-identities.json", StringComparer.Ordinal))
                    destinations.Add("material-identities.json", identities);
                var info = AnalysisArchive.CopyPayloads(path, destinations);
                if (!info.Payloads.Contains("model.json", StringComparer.Ordinal) ||
                    !info.Payloads.Contains("model.mesh", StringComparer.Ordinal))
                    throw new InvalidDataException("External mesh requires editable model metadata.");
                if (info.Name != description.Name || info.Version != description.Version)
                    throw new InvalidDataException("Archive identity changed while opening.");
            }
            var model = StaticModelMetadata.Deserialize(metadata.ToArray(), File.ReadAllBytes(meshPath));
            if (model.Name != description.Name) throw new InvalidDataException("Model identity does not match archive metadata.");
            if (description.Payloads.Contains("material-identities.json", StringComparer.Ordinal))
                ProjectMaterialIdentities.Deserialize(identities.ToArray(), model);
            return model;
        }
        finally { Directory.Delete(directory, true); }
    }
}
