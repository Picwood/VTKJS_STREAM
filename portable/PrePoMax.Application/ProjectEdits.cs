using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

internal static class ProjectEdits
{
    internal static void SaveNew(string destination, FeModel model)
        => SaveNew(destination, model, null);

    internal static void SaveNew(string destination, ReopenedStaticProject project)
        => SaveNew(destination, project.Model, project.GeometrySource, ProjectMaterialIdentities.Reconcile(project.Model, project.MaterialIdentities));

    internal static void SaveNew(string destination, FeModel model, ProjectGeometrySource? geometrySource,
                                 ProjectMaterialIdentity[]? identities = null)
    {
        string target = Path.GetFullPath(destination);
        string temporary = Path.Combine(Path.GetDirectoryName(target)!, ".edit-" + Guid.NewGuid().ToString("N") + ".pma");
        try
        {
            // Rebuild solver input and metadata. Old results no longer describe this model.
            StaticProjectArchive.Save(temporary, model, geometrySource: geometrySource, materialIdentities: identities);
            File.Move(temporary, target, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
