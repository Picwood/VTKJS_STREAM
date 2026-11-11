using CaeGlobals;
using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record PrescribedDisplacement(string Step, string Name, int Direction, string Kind, string Region, double Value);

public sealed record TranslationDirection(string Step, string Name, int Direction, string Kind, string Region, double? Value);
public sealed record FixedSupport(string Step, string Name, string Region);
public sealed record FixedSupportTarget(string Step, string Region);

/// <summary>Edit translations in existing displacement boundaries.</summary>
public static class ProjectBoundaries
{
    public static FixedSupport[] ListFixed(string path) => StaticProjectArchive.Open(path).Model.StepCollection.StepsList
        .SelectMany(step => step.BoundaryConditions.Values.OfType<FixedBC>()
            .Select(boundary => new FixedSupport(step.Name, boundary.Name, boundary.RegionName))).ToArray();

    public static FixedSupportTarget[] FixedCreationTargets(string path)
    {
        var model = StaticProjectArchive.Open(path).Model;
        return model.StepCollection.StepsList.SelectMany(step => model.Mesh.NodeSets.Values
            .Where(set => set.Labels.Length > 0).Select(set => new FixedSupportTarget(step.Name, set.Name))).ToArray();
    }

    public static void AddFixedCopy(string source, string destination, string stepName, string name, string nodeSet)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a fixed-support name.", nameof(name));
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        var step = model.StepCollection.StepsList.SingleOrDefault(step => step.Name == stepName);
        if (step == null || step.BoundaryConditions.Keys.Any(existing => existing.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            !model.Mesh.NodeSets.TryGetValue(nodeSet, out var set) || set.Labels.Length == 0)
            throw new NotSupportedException("Choose an existing step, a nonempty node set and a unique boundary name.");
        step.AddBoundaryCondition(new FixedBC(name, nodeSet, RegionTypeEnum.NodeSetName,
            model.Properties.ModelSpace.IsTwoD()));
        ProjectEdits.SaveNew(destination, project);
    }

    public static PrescribedDisplacement[] List(string path) => StaticProjectArchive.Open(path).Model.StepCollection.StepsList
        .SelectMany(step => step.BoundaryConditions.Values.OfType<DisplacementRotation>()
            .SelectMany(boundary => new[] { boundary.U1, boundary.U2, boundary.U3 }
                .Select((value, index) => new PrescribedDisplacement(step.Name, boundary.Name, index + 1,
                    "Displacement U" + (index + 1), boundary.RegionName, value.Value))
                .Where(value => double.IsFinite(value.Value)))).ToArray();

    public static TranslationDirection[] ListDirections(string path) => StaticProjectArchive.Open(path).Model.StepCollection.StepsList
        .SelectMany(step => step.BoundaryConditions.Values.OfType<DisplacementRotation>()
            .Where(boundary => boundary.GetFixedDirections().Length == 0)
            .SelectMany(boundary => new[] { boundary.U1, boundary.U2, boundary.U3 }
                .Select((value, index) => new TranslationDirection(step.Name, boundary.Name, index + 1,
                    "Translation U" + (index + 1), boundary.RegionName, double.IsNaN(value.Value) ? null : value.Value)))).ToArray();

    public static TranslationDirection[] CreationTargets(string path)
    {
        var model = StaticProjectArchive.Open(path).Model;
        return model.StepCollection.StepsList.SelectMany(step => model.Mesh.NodeSets.Values
            .Where(set => set.Labels.Length > 0).SelectMany(set => Enumerable.Range(1, 3)
                .Select(direction => new TranslationDirection(step.Name, "New boundary", direction,
                    "Translation U" + direction, set.Name, 0)))).ToArray();
    }

    public static void AddCopy(string source, string destination, string stepName, string name, string nodeSet, int direction, double value)
    {
        if (direction < 1 || direction > 3 || !double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Enter a finite translation.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a boundary name.", nameof(name));
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        var step = model.StepCollection.StepsList.SingleOrDefault(step => step.Name == stepName);
        if (step == null || step.BoundaryConditions.ContainsKey(name) ||
            !model.Mesh.NodeSets.TryGetValue(nodeSet, out var set) || set.Labels.Length == 0)
            throw new NotSupportedException("Choose an existing step, a nonempty node set and a unique boundary name.");
        var boundary = new DisplacementRotation(name, nodeSet, RegionTypeEnum.NodeSetName, false, false, 0);
        var prescribed = new EquationContainer(typeof(StringLengthDOFConverter), value);
        switch (direction)
        {
            case 1: boundary.U1 = prescribed; break;
            case 2: boundary.U2 = prescribed; break;
            case 3: boundary.U3 = prescribed; break;
        }
        step.AddBoundaryCondition(boundary);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveDirectionCopy(string source, string destination, string stepName, string name, int direction, double? value)
        => Save(source, destination, stepName, name, direction, value, true);

    public static void SaveDisplacementCopy(string source, string destination, string stepName, string name, int direction, double value)
        => Save(source, destination, stepName, name, direction, value, false);

    private static void Save(string source, string destination, string stepName, string name, int direction, double? value, bool allowFree)
    {
        if (direction < 1 || direction > 3 || (value.HasValue && !double.IsFinite(value.Value)))
            throw new ArgumentOutOfRangeException(nameof(value), "Select a translation and enter a finite displacement.");
        var project = StaticProjectArchive.Open(source);
        var step = project.Model.StepCollection.StepsList.SingleOrDefault(step => step.Name == stepName);
        if (step == null || !step.BoundaryConditions.TryGetValue(name, out var boundary) || boundary is not DisplacementRotation displacement)
            throw new NotSupportedException("Select an existing prescribed displacement.");
        var current = new[] { displacement.U1, displacement.U2, displacement.U3 }[direction - 1];
        if (displacement.GetFixedDirections().Length != 0 || (!allowFree && !double.IsFinite(current.Value))) throw new NotSupportedException("Free or fixed directions cannot be changed here.");
        var replacement = new EquationContainer(typeof(StringLengthDOFConverter), value ?? double.NaN);
        switch (direction)
        {
            case 1: displacement.U1 = replacement; break;
            case 2: displacement.U2 = replacement; break;
            case 3: displacement.U3 = replacement; break;
        }
        if (!displacement.IsProperlyDefined(out var error)) throw new NotSupportedException(error);
        ProjectEdits.SaveNew(destination, project);
    }
}
