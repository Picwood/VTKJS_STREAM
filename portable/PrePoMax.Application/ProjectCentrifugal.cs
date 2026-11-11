using CaeGlobals;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record CentrifugalLoadDefinition(string Step, string Name, string Region, string Kind,
    double X, double Y, double Z, double N1, double N2, double N3, double RotationalSpeed);

/// <summary>Creates centrifugal loads on density-backed three-dimensional solid-section regions.</summary>
public static class ProjectCentrifugal
{
    public static CentrifugalLoadDefinition[] List(string path) => StaticProjectArchive.Open(path).Model.StepCollection.StepsList
        .SelectMany(step => step.Loads.Values.OfType<CentrifLoad>().Select(load =>
            new CentrifugalLoadDefinition(step.Name, load.Name, load.RegionName, Describe(load.RegionType),
                load.X.Value, load.Y.Value, load.Z.Value, load.N1.Value, load.N2.Value, load.N3.Value,
                load.RotationalSpeed.Value))).ToArray();

    public static CentrifugalLoadDefinition[] CreationTargets(string path)
    {
        var model = StaticProjectArchive.Open(path).Model;
        if (model.Properties.ModelSpace != ModelSpaceEnum.ThreeD) return [];
        return Targets(model).SelectMany(target => model.StepCollection.StepsList.Select(step =>
            new CentrifugalLoadDefinition(step.Name, "New centrifugal load", target.Region, Describe(target.Type),
                0, 0, 0, 0, 0, 1, 1))).ToArray();
    }

    public static void AddCopy(string source, string destination, string stepName, string name, string region,
                               double x, double y, double z, double n1, double n2, double n3, double rotationalSpeed)
    {
        Validate(x, y, z, n1, n2, n3, rotationalSpeed);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a centrifugal-load name.", nameof(name));
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        var step = model.StepCollection.StepsList.SingleOrDefault(candidate => candidate.Name == stepName);
        var targets = Targets(model).Where(target => target.Region == region).ToArray();
        if (model.Properties.ModelSpace != ModelSpaceEnum.ThreeD || step == null ||
            step.Loads.Keys.Any(existing => existing.Equals(name, StringComparison.OrdinalIgnoreCase)) || targets.Length != 1)
            throw new NotSupportedException("Choose an existing step, one density-backed 3D solid region and a unique load name.");
        var target = targets[0];
        step.AddLoad(new CentrifLoad(name.Trim(), target.Region, target.Type, [x, y, z], [n1, n2, n3],
            rotationalSpeed, false, false, false, 0));
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveCopy(string source, string destination, string stepName, string name,
                                double x, double y, double z, double n1, double n2, double n3, double rotationalSpeed)
    {
        Validate(x, y, z, n1, n2, n3, rotationalSpeed);
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(candidate => candidate.Name == stepName);
        if (project.Model.Properties.ModelSpace != ModelSpaceEnum.ThreeD || step == null ||
            !step.Loads.TryGetValue(name, out var selected) || selected is not CentrifLoad load || load.Axisymmetric)
            throw new NotSupportedException("Select an existing three-dimensional centrifugal load.");
        load.X = new EquationContainer(typeof(StringLengthConverter), x);
        load.Y = new EquationContainer(typeof(StringLengthConverter), y);
        load.Z = new EquationContainer(typeof(StringLengthConverter), z);
        load.N1 = new EquationContainer(typeof(StringLengthConverter), n1);
        load.N2 = new EquationContainer(typeof(StringLengthConverter), n2);
        load.N3 = new EquationContainer(typeof(StringLengthConverter), n3);
        load.RotationalSpeed = new EquationContainer(typeof(StringRotationalSpeedConverter), rotationalSpeed);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string stepName, string name)
    {
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(candidate => candidate.Name == stepName);
        if (step == null || !step.Loads.TryGetValue(name, out var selected) || selected is not CentrifLoad)
            throw new NotSupportedException("Select an existing centrifugal load.");
        step.Loads.Remove(name);
        ProjectEdits.SaveNew(destination, project);
    }

    private static (string Region, RegionTypeEnum Type)[] Targets(FeModel model) => model.Sections.Values
        .OfType<SolidSection>()
        .Where(section => model.Materials.TryGetValue(section.MaterialName, out var material) &&
            material.Properties.OfType<Density>().SingleOrDefault()?.DensityTemp is { Length: 1 } density &&
            double.IsFinite(density[0][0].Value) && density[0][0].Value > 0)
        .Where(section => section.RegionType switch
        {
            RegionTypeEnum.ElementSetName => model.Mesh.ElementSets.TryGetValue(section.RegionName, out var set) && set.Labels.Length > 0,
            RegionTypeEnum.PartName => model.Mesh.Parts.TryGetValue(section.RegionName, out var part) && part.Labels.Length > 0,
            _ => false
        })
        .Select(section => (section.RegionName, section.RegionType)).Distinct().ToArray();

    private static string Describe(RegionTypeEnum type) => type switch
    {
        RegionTypeEnum.ElementSetName => "Element set",
        RegionTypeEnum.PartName => "Part",
        _ => type.ToString()
    };

    private static void Validate(double x, double y, double z, double n1, double n2, double n3, double rotationalSpeed)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) ||
            !double.IsFinite(n1) || !double.IsFinite(n2) || !double.IsFinite(n3) ||
            n1 * n1 + n2 * n2 + n3 * n3 == 0 || !double.IsFinite(rotationalSpeed) || rotationalSpeed <= 0)
            throw new ArgumentOutOfRangeException(nameof(rotationalSpeed),
                "Axis point and direction must be finite, direction must be nonzero and rotational speed must be positive.");
    }
}
