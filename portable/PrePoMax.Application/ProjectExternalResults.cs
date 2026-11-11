using PrePoMax.Persistence;

namespace PrePoMax.Application;

/// <summary>Attach externally computed FRD fields only after validating their mesh against the editable model.</summary>
public static class ProjectExternalResults
{
    public static void AttachCopy(string source, string destination, string frdPath)
    {
        if (new FileInfo(frdPath).Length is <= 0 or > 64L * 1024 * 1024)
            throw new InvalidDataException("Upload a nonempty FRD file of at most 64 MiB.");
        var project = StaticProjectArchive.Open(source);
        string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!,
            ".results-" + Guid.NewGuid().ToString("N") + ".pma");
        try
        {
            // Existing DAT/binary fields describe the previous run and must not survive replacement.
            StaticProjectArchive.Save(temporary, project.Model, File.ReadAllBytes(frdPath),
                geometrySource: project.GeometrySource, materialIdentities: project.MaterialIdentities);
            if (ProjectResults.List(temporary).Length == 0)
                throw new InvalidDataException("The FRD contains no supported static nodal fields.");
            File.Move(temporary, destination, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
