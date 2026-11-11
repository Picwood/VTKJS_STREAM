using CaeModel;
using System.Text.Json.Serialization;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record StaticStepSettings(string Name, bool Nlgeom = false,
    [property: JsonConverter(typeof(JsonStringEnumConverter<IncrementationTypeEnum>))]
    IncrementationTypeEnum IncrementationType = IncrementationTypeEnum.Default,
    double TimePeriod = 1, double InitialTimeIncrement = 1, double MinTimeIncrement = 1e-5,
    double MaxTimeIncrement = 1e30, int MaxIncrements = 100);
public sealed record StepEditorData(StaticStepSummary[] Steps, string? EditingError);
public sealed record StaticStepSummary(string Name, bool Active, bool Nlgeom, string IncrementationType,
    double TimePeriod, double InitialTimeIncrement, double MinTimeIncrement, double MaxTimeIncrement,
    int MaxIncrements, int Boundaries, int Loads, int FieldOutputs, int HistoryOutputs);

/// <summary>Original static-step operations on the supported editable model, saved as a new project.</summary>
public static class ProjectSteps
{
    public static StaticStepSummary[] List(string path) => Read(path).Steps;

    public static StepEditorData Read(string path)
    {
        var project = StaticProjectArchive.Open(path);
        var steps = project.Model.StepCollection.StepsList.OfType<StaticStep>()
            .Select(step => new StaticStepSummary(step.Name, step.Active, step.Nlgeom,
                step.IncrementationType.ToString(), step.TimePeriod, step.InitialTimeIncrement, step.MinTimeIncrement,
                step.MaxTimeIncrement, step.MaxIncrements, step.BoundaryConditions.Count, step.Loads.Count,
                step.FieldOutputs.Count, step.HistoryOutputs.Count)).ToArray();
        return new(steps, EditingError(project.Model));
    }

    private static string? EditingError(FeModel model) => model.CalculixUserKeywords?.Count > 0
        ? "Step editing with positional CalculiX user keywords is not supported yet; their insertion paths require remapping."
        : null;

    public static void AddCopy(string source, string destination, StaticStepSettings settings, bool copyPrevious = true)
    {
        Validate(settings);
        var project = Editable(source);
        Unique(project, settings.Name);
        var step = new StaticStep(settings.Name);
        Configure(step, settings);
        if (copyPrevious && project.Model.StepCollection.StepsList.LastOrDefault() is StaticStep previous)
        {
            // The original AddStep copies BCs, loads and defined fields, but not output requests.
            // Defined fields are outside the current editable archive contract.
            var independent = StaticModelMetadata.CopyStaticStep(previous);
            foreach (var boundary in independent.BoundaryConditions.Values) step.AddBoundaryCondition(boundary);
            foreach (var load in independent.Loads.Values) step.AddLoad(load);
        }
        project.Model.StepCollection.AddStep(step);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveCopy(string source, string destination, string stepName, StaticStepSettings settings)
    {
        Validate(settings);
        var project = Editable(source);
        var step = Find(project, stepName);
        Unique(project, settings.Name, step);
        Configure(step, settings);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void DuplicateCopy(string source, string destination, string stepName, string newName)
    {
        ValidateName(newName);
        var project = Editable(source);
        Unique(project, newName);
        var duplicate = StaticModelMetadata.CopyStaticStep(Find(project, stepName));
        duplicate.Name = newName;
        project.Model.StepCollection.AddStep(duplicate);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SetActiveCopy(string source, string destination, string stepName, bool active)
    {
        var project = Editable(source);
        Find(project, stepName).Active = active;
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string stepName)
    {
        var project = Editable(source);
        var step = Find(project, stepName);
        project.Model.StepCollection.RemoveStep(step.Name);
        // Named mesh regions may be shared with other steps; do not remove them with this step.
        ProjectEdits.SaveNew(destination, project);
    }

    private static ReopenedStaticProject Editable(string path)
    {
        var project = StaticProjectArchive.Open(path);
        if (EditingError(project.Model) is { } error) throw new NotSupportedException(error);
        return project;
    }

    private static StaticStep Find(ReopenedStaticProject project, string name) =>
        project.Model.StepCollection.StepsList.OfType<StaticStep>().SingleOrDefault(step => step.Name == name)
        ?? throw new ArgumentException("Select an existing static step.");

    private static void Unique(ReopenedStaticProject project, string name, StaticStep? except = null)
    {
        if (except == null && project.Model.StepCollection.StepsList.Count >= 1000)
            throw new NotSupportedException("The step editor supports at most 1,000 steps.");
        if (project.Model.StepCollection.StepsList.Any(step => !ReferenceEquals(step, except) &&
            step.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Step names must be unique, ignoring case.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 128 || name.Any(char.IsControl))
            throw new ArgumentException("Enter a step name of 1–128 characters without surrounding whitespace or control characters.");
    }

    private static void Validate(StaticStepSettings settings)
    {
        ValidateName(settings.Name);
        if (!Enum.IsDefined(settings.IncrementationType) || settings.MaxIncrements is < 1 or > 1000000 ||
            !new[] { settings.TimePeriod, settings.InitialTimeIncrement, settings.MinTimeIncrement, settings.MaxTimeIncrement }
                .All(value => double.IsFinite(value) && value > 0) ||
            settings.MinTimeIncrement > settings.InitialTimeIncrement ||
            settings.InitialTimeIncrement > settings.MaxTimeIncrement || settings.InitialTimeIncrement > settings.TimePeriod)
            throw new ArgumentException("Use finite positive times, min ≤ initial ≤ max, initial ≤ period, and 1–1,000,000 increments.");
    }

    private static void Configure(StaticStep step, StaticStepSettings settings)
    {
        step.Name = settings.Name;
        step.Nlgeom = settings.Nlgeom;
        step.IncrementationType = settings.IncrementationType;
        step.Direct = settings.IncrementationType == IncrementationTypeEnum.Direct;
        step.MaxIncrements = settings.MaxIncrements;
        step.TimePeriod = settings.TimePeriod;
        step.MaxTimeIncrement = settings.MaxTimeIncrement;
        step.MinTimeIncrement = settings.MinTimeIncrement;
        step.InitialTimeIncrement = settings.InitialTimeIncrement;
    }
}
