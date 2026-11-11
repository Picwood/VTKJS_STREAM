using CaeMesh;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

/// <summary>Reuses validated mesh/topology while selecting in one immutable project snapshot.</summary>
public sealed class ProjectMeshSelectionSession
{
    private string? path;
    private (long Length, DateTime Written) stamp;
    private FeMesh? mesh;

    public FeMesh GetMesh(string project, bool restart = false)
    {
        string absolute = Path.GetFullPath(project);
        var before = Stamp(absolute);
        if (restart || mesh == null || path != absolute || stamp != before)
        {
            Clear();
            var loaded = StaticProjectArchive.Open(absolute).Model.Mesh;
            if (Stamp(absolute) != before) throw new IOException("The project changed while opening mesh selection. Reopen the editor.");
            mesh = loaded; path = absolute; stamp = before;
        }
        return mesh;
    }

    public void Clear() { mesh = null; path = null; }

    private static (long, DateTime) Stamp(string path)
    {
        var info = new FileInfo(path);
        return (info.Length, info.LastWriteTimeUtc);
    }
}
