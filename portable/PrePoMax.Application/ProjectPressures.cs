using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record PressureLoad(string Step, string Name, string Region, double Magnitude);

/// <summary>Creates and edits finite constant pressures on named element surfaces.</summary>
public static class ProjectPressures
{
    public static PressureLoad[] List(string path) => StaticProjectArchive.Open(path).Model.StepCollection.StepsList
        .SelectMany(step => step.Loads.Values.OfType<DLoad>()
            .Select(load => new PressureLoad(step.Name, load.Name, load.SurfaceName, load.Magnitude.Value))).ToArray();

    public static PressureLoad[] CreationTargets(string path)
    {
        var model = StaticProjectArchive.Open(path).Model;
        var surfaces = model.Mesh.Surfaces.Values.Where(surface => ValidSurface(model.Mesh, surface));
        return model.StepCollection.StepsList.SelectMany(step => surfaces.Select(surface =>
            new PressureLoad(step.Name, "New pressure", surface.Name, 0))).ToArray();
    }

    public static void AddCopy(string source, string destination, string stepName, string name, string surfaceName,
                               double magnitude)
    {
        ValidateMagnitude(magnitude);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a pressure name.", nameof(name));
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(value => value.Name == stepName);
        if (step == null || step.Loads.ContainsKey(name) || !project.Model.Mesh.Surfaces.TryGetValue(surfaceName, out var surface) ||
            !ValidSurface(project.Model.Mesh, surface))
            throw new NotSupportedException("Choose an existing step, a valid element surface and a unique pressure name.");
        step.AddLoad(new DLoad(name, surfaceName, RegionTypeEnum.SurfaceName, magnitude, false, false, 0));
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveMagnitudeCopy(string source, string destination, string stepName, string name, double magnitude)
    {
        ValidateMagnitude(magnitude);
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(value => value.Name == stepName);
        if (step == null || !step.Loads.TryGetValue(name, out var load) || load is not DLoad pressure)
            throw new NotSupportedException("Select an existing pressure load.");
        pressure.Magnitude = new EquationContainer(typeof(StringPressureConverter), magnitude);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string stepName, string name)
    {
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(value => value.Name == stepName);
        if (step == null || !step.Loads.TryGetValue(name, out var load) || load is not DLoad)
            throw new NotSupportedException("Select an existing pressure load.");
        step.Loads.Remove(name);
        ProjectEdits.SaveNew(destination, project);
    }

    private static void ValidateMagnitude(double magnitude)
    {
        if (!double.IsFinite(magnitude)) throw new ArgumentOutOfRangeException(nameof(magnitude), "Pressure must be finite.");
    }

    private static bool ValidSurface(FeMesh mesh, FeSurface surface) => surface.Type == FeSurfaceType.Element &&
        surface.ElementFaces is { Count: > 0 } && surface.ElementFaces.Values.All(name =>
            mesh.ElementSets.TryGetValue(name, out var set) && set.Labels.Length > 0);
}
