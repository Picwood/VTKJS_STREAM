using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record ForceLoad(string Step, string Name, string Kind, string Region, double F1, double F2, double F3);
public enum ForceLoadCreationKind { ConcentratedForce, SurfaceTraction }

/// <summary>Creates nodal forces or total-force surface tractions and edits existing force components.</summary>
public static class ProjectLoads
{
    public static ForceLoad[] List(string path) => StaticProjectArchive.Open(path).Model.StepCollection.StepsList
        .SelectMany(step => step.Loads.Values.Where(load => load is CLoad or STLoad)
            .Select(load => Describe(step.Name, load))).ToArray();

    public static ForceLoad[] CreationTargets(string path)
    {
        var model = StaticProjectArchive.Open(path).Model;
        var nodal = model.Mesh.NodeSets.Values.Where(set => set.Labels.Length > 0)
            .Select(set => (Region: set.Name, Kind: "Concentrated force per node"));
        var surface = model.Mesh.Surfaces.Values.Where(value => value.Type == FeSurfaceType.Element &&
                value.ElementFaces is { Count: > 0 } && value.ElementFaces.Values.All(name =>
                    model.Mesh.ElementSets.TryGetValue(name, out var set) && set.Labels.Length > 0))
            .Select(value => (Region: value.Name, Kind: "Surface traction (total force)"));
        return model.StepCollection.StepsList.SelectMany(step => nodal.Concat(surface)
            .Select(target => new ForceLoad(step.Name, "New force", target.Kind, target.Region, 0, 0, 0))).ToArray();
    }

    public static void AddCopy(string source, string destination, string stepName, string name, string nodeSet, double f1, double f2, double f3)
        => AddCopy(source, destination, stepName, name, nodeSet, ForceLoadCreationKind.ConcentratedForce, f1, f2, f3);

    public static void AddCopy(string source, string destination, string stepName, string name, string region,
                               ForceLoadCreationKind kind, double f1, double f2, double f3)
    {
        if (!double.IsFinite(f1) || !double.IsFinite(f2) || !double.IsFinite(f3))
            throw new ArgumentOutOfRangeException(nameof(f1), "Force components must be finite.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a force name.", nameof(name));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        var step = model.StepCollection.StepsList.SingleOrDefault(step => step.Name == stepName);
        bool validRegion = kind switch
        {
            ForceLoadCreationKind.ConcentratedForce => model.Mesh.NodeSets.TryGetValue(region, out var set) && set.Labels.Length > 0,
            ForceLoadCreationKind.SurfaceTraction => model.Mesh.Surfaces.TryGetValue(region, out var surface) &&
                surface.Type == FeSurfaceType.Element && surface.ElementFaces is { Count: > 0 } &&
                surface.ElementFaces.Values.All(setName => model.Mesh.ElementSets.TryGetValue(setName, out var elementSet) && elementSet.Labels.Length > 0),
            _ => false
        };
        if (step == null || step.Loads.ContainsKey(name) || !validRegion)
            throw new NotSupportedException("Choose an existing step, a supported nonempty region and a unique load name.");
        step.AddLoad(kind == ForceLoadCreationKind.SurfaceTraction
            ? new STLoad(name, region, RegionTypeEnum.SurfaceName, f1, f2, f3, false, false, 0)
            : new CLoad(name, region, RegionTypeEnum.NodeSetName, f1, f2, f3, false, false, 0));
        ProjectEdits.SaveNew(destination, project);
    }

    private static ForceLoad Describe(string step, Load load) => load switch
    {
        CLoad force => new(step, force.Name, "Concentrated force", force.RegionName, force.F1.Value, force.F2.Value, force.F3.Value),
        STLoad force => new(step, force.Name, "Surface traction (total force)", force.SurfaceName, force.F1.Value, force.F2.Value, force.F3.Value),
        _ => throw new NotSupportedException("Select a supported force load.")
    };

    public static void SaveForceCopy(string source, string destination, string stepName, string name, double f1, double f2, double f3)
    {
        if (!double.IsFinite(f1) || !double.IsFinite(f2) || !double.IsFinite(f3))
            throw new ArgumentOutOfRangeException(nameof(f1), "Force components must be finite.");
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(step => step.Name == stepName);
        if (step == null || !step.Loads.TryGetValue(name, out var load))
            throw new NotSupportedException("Select an existing step and force load.");
        switch (load)
        {
            case CLoad force:
                force.F1 = new EquationContainer(typeof(StringForceConverter), f1);
                force.F2 = new EquationContainer(typeof(StringForceConverter), f2);
                force.F3 = new EquationContainer(typeof(StringForceConverter), f3);
                break;
            case STLoad force:
                force.F1 = new EquationContainer(typeof(StringForceConverter), f1);
                force.F2 = new EquationContainer(typeof(StringForceConverter), f2);
                force.F3 = new EquationContainer(typeof(StringForceConverter), f3);
                break;
            default: throw new NotSupportedException("Select a supported force load.");
        }
        ProjectEdits.SaveNew(destination, project);
    }
}
