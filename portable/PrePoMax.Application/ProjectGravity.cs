using CaeGlobals;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record GravityLoadDefinition(string Step, string Name, string Region, string Kind, double A1, double A2, double A3);

/// <summary>Creates constant gravity loads on density-backed solid-section regions.</summary>
public static class ProjectGravity
{
    public static GravityLoadDefinition[] List(string path) => StaticProjectArchive.Open(path).Model.StepCollection.StepsList
        .SelectMany(step => step.Loads.Values.OfType<GravityLoad>().Select(load =>
            new GravityLoadDefinition(step.Name, load.Name, load.RegionName, Describe(load.RegionType),
                load.F1.Value, load.F2.Value, load.F3.Value))).ToArray();

    public static GravityLoadDefinition[] CreationTargets(string path)
    {
        var model = StaticProjectArchive.Open(path).Model;
        return Targets(model).SelectMany(target => model.StepCollection.StepsList.Select(step =>
            new GravityLoadDefinition(step.Name, "New gravity", target.Region, Describe(target.Type), 0, -9.81, 0))).ToArray();
    }

    public static void AddCopy(string source, string destination, string stepName, string name, string region,
                               double a1, double a2, double a3)
    {
        ValidateVector(a1, a2, a3);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a gravity-load name.", nameof(name));
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        var step = model.StepCollection.StepsList.SingleOrDefault(candidate => candidate.Name == stepName);
        var targets = Targets(model).Where(target => target.Region == region).ToArray();
        if (step == null || step.Loads.Keys.Any(existing => existing.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            targets.Length != 1 || (model.Properties.ModelSpace.IsTwoD() && a3 != 0))
            throw new NotSupportedException("Choose an existing step, one density-backed solid region and a unique load name.");
        var target = targets[0];
        step.AddLoad(new GravityLoad(name.Trim(), target.Region, target.Type, a1, a2, a3,
            model.Properties.ModelSpace.IsTwoD(), false, 0));
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveCopy(string source, string destination, string stepName, string name,
                                double a1, double a2, double a3)
    {
        ValidateVector(a1, a2, a3);
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(candidate => candidate.Name == stepName);
        if (step == null || !step.Loads.TryGetValue(name, out var selected) || selected is not GravityLoad gravity ||
            (project.Model.Properties.ModelSpace.IsTwoD() && a3 != 0))
            throw new NotSupportedException("Select an existing gravity load with valid acceleration components.");
        gravity.F1 = new EquationContainer(typeof(StringAccelerationConverter), a1);
        gravity.F2 = new EquationContainer(typeof(StringAccelerationConverter), a2);
        gravity.F3 = new EquationContainer(typeof(StringAccelerationConverter), a3);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string stepName, string name)
    {
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(candidate => candidate.Name == stepName);
        if (step == null || !step.Loads.TryGetValue(name, out var selected) || selected is not GravityLoad)
            throw new NotSupportedException("Select an existing gravity load.");
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

    private static void ValidateVector(double a1, double a2, double a3)
    {
        if (!double.IsFinite(a1) || !double.IsFinite(a2) || !double.IsFinite(a3) ||
            a1 * a1 + a2 * a2 + a3 * a3 == 0)
            throw new ArgumentOutOfRangeException(nameof(a1), "Gravity components must be finite and not all zero.");
    }
}
