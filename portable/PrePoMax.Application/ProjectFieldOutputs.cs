using CaeModel;
using PrePoMax.Persistence;
using System.Text.Json.Serialization;

namespace PrePoMax.Application;

[JsonConverter(typeof(JsonStringEnumConverter<FieldOutputKind>))]
public enum FieldOutputKind { Nodal, Element, Contact }

public sealed record FieldOutputSettings(string Name, FieldOutputKind Kind, string[] Variables,
    bool LastIterations = false, bool ContactElements = false, bool Global = true,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ElementFieldOutputOutputEnum>))]
    ElementFieldOutputOutputEnum ElementOutput = ElementFieldOutputOutputEnum.Default);
public sealed record FieldOutputSummary(string Step, string Name, bool Active, string Kind, string[] Variables,
    bool LastIterations, bool ContactElements, bool Global, string ElementOutput);
public sealed record FieldOutputEditorData(string[] Steps, FieldOutputSummary[] Outputs,
    Dictionary<string, string[]> VariableChoices, string? EditingError);

/// <summary>Original-style global field-output operations on supported static steps.</summary>
public static class ProjectFieldOutputs
{
    private const int MaxPerStep = 1000;

    public static FieldOutputEditorData Read(string path)
    {
        var project = StaticProjectArchive.Open(path);
        var steps = project.Model.StepCollection.StepsList.OfType<StaticStep>().ToArray();
        return new(steps.Select(step => step.Name).ToArray(),
            steps.SelectMany(step => step.FieldOutputs.Values.Select(output => Summary(step.Name, output))).ToArray(),
            new()
            {
                [FieldOutputKind.Nodal.ToString()] = Names<NodalFieldVariable>(),
                [FieldOutputKind.Element.ToString()] = Names<ElementFieldVariable>(),
                [FieldOutputKind.Contact.ToString()] = Names<ContactFieldVariable>()
            }, EditingError(project.Model));
    }

    public static void AddCopy(string source, string destination, string stepName, FieldOutputSettings settings)
    {
        var project = Editable(source);
        var step = FindStep(project, stepName);
        Validate(step, settings);
        if (step.FieldOutputs.Count >= MaxPerStep) throw new NotSupportedException("A step supports at most 1,000 field outputs in this editor.");
        step.AddFieldOutput(Create(settings));
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveCopy(string source, string destination, string stepName, string outputName, FieldOutputSettings settings)
    {
        var project = Editable(source);
        var step = FindStep(project, stepName);
        var previous = Find(step, outputName);
        Validate(step, settings, previous);
        if (Kind(previous) != settings.Kind) throw new ArgumentException("Editing retains the field-output type.");
        var replacement = Create(settings);
        replacement.Active = previous.Active;
        step.FieldOutputs.Replace(previous.Name, replacement.Name, replacement);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void DuplicateCopy(string source, string destination, string stepName, string outputName, string newName)
    {
        var project = Editable(source);
        var step = FindStep(project, stepName);
        var original = Find(step, outputName);
        ValidateName(newName);
        Unique(step, newName);
        if (step.FieldOutputs.Count >= MaxPerStep) throw new NotSupportedException("A step supports at most 1,000 field outputs in this editor.");
        var duplicate = Clone(original);
        duplicate.Name = newName;
        step.AddFieldOutput(duplicate);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SetActiveCopy(string source, string destination, string stepName, string outputName, bool active)
    {
        var project = Editable(source);
        Find(FindStep(project, stepName), outputName).Active = active;
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string stepName, string outputName)
    {
        var project = Editable(source);
        var step = FindStep(project, stepName);
        step.FieldOutputs.Remove(Find(step, outputName).Name);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void PropagateCopy(string source, string destination, string stepName, string outputName)
    {
        var project = Editable(source);
        var steps = project.Model.StepCollection.StepsList.OfType<StaticStep>().ToArray();
        int index = Array.FindIndex(steps, step => step.Name == stepName);
        if (index < 0) throw new ArgumentException("Select an existing static step.");
        var original = Find(steps[index], outputName);
        for (int i = index + 1; i < steps.Length; i++)
        {
            var clone = Clone(original);
            if (steps[i].FieldOutputs.TryGetValue(outputName, out var existing))
                steps[i].FieldOutputs.Replace(existing.Name, clone.Name, clone);
            else
            {
                if (steps[i].FieldOutputs.Count >= MaxPerStep)
                    throw new NotSupportedException("A target step already has 1,000 field outputs.");
                steps[i].AddFieldOutput(clone);
            }
        }
        ProjectEdits.SaveNew(destination, project);
    }

    private static string? EditingError(FeModel model) => model.CalculixUserKeywords?.Count > 0
        ? "Field-output editing is unavailable for projects with positional CalculiX keywords until their placement can be remapped."
        : null;

    private static ReopenedStaticProject Editable(string path)
    {
        var project = StaticProjectArchive.Open(path);
        if (EditingError(project.Model) is { } error) throw new NotSupportedException(error);
        return project;
    }

    private static StaticStep FindStep(ReopenedStaticProject project, string name) =>
        project.Model.StepCollection.StepsList.OfType<StaticStep>().SingleOrDefault(step => step.Name == name)
        ?? throw new ArgumentException("Select an existing static step.");

    private static FieldOutput Find(StaticStep step, string name) =>
        step.FieldOutputs.TryGetValue(name, out var output) ? output
        : throw new ArgumentException("Select an existing field output.");

    private static void Validate(StaticStep step, FieldOutputSettings settings, FieldOutput? except = null)
    {
        ValidateName(settings.Name);
        Unique(step, settings.Name, except);
        if (!Enum.IsDefined(settings.Kind) || !Enum.IsDefined(settings.ElementOutput))
            throw new ArgumentException("Select a supported field-output type.");
        if (settings.Kind != FieldOutputKind.Element && settings.ElementOutput != ElementFieldOutputOutputEnum.Default)
            throw new ArgumentException("Only element output supports the 2D/3D output mode.");
        _ = ParseVariables(settings.Kind, settings.Variables);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 128 || name.Any(char.IsControl))
            throw new ArgumentException("Enter an output name of 1–128 characters without surrounding whitespace or control characters.");
    }

    private static void Unique(StaticStep step, string name, FieldOutput? except = null)
    {
        if (step.FieldOutputs.Values.Any(output => !ReferenceEquals(output, except) && output.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Field-output names must be unique within a step, ignoring case.");
    }

    private static FieldOutput Create(FieldOutputSettings settings)
    {
        long variables = ParseVariables(settings.Kind, settings.Variables);
        FieldOutput output = settings.Kind switch
        {
            FieldOutputKind.Nodal => new NodalFieldOutput(settings.Name, (NodalFieldVariable)variables),
            FieldOutputKind.Element => new ElementFieldOutput(settings.Name, (ElementFieldVariable)variables)
                { Output = settings.ElementOutput },
            FieldOutputKind.Contact => new ContactFieldOutput(settings.Name, (ContactFieldVariable)variables),
            _ => throw new ArgumentException("Select a supported field-output type.")
        };
        output.LastIterations = settings.LastIterations;
        output.ContactElements = settings.ContactElements;
        output.Global = settings.Global;
        return output;
    }

    private static FieldOutput Clone(FieldOutput output)
    {
        FieldOutput clone = output switch
        {
            NodalFieldOutput value => new NodalFieldOutput(value.Name, value.Variables),
            ElementFieldOutput value => new ElementFieldOutput(value.Name, value.Variables) { Output = value.Output },
            ContactFieldOutput value => new ContactFieldOutput(value.Name, value.Variables),
            _ => throw new NotSupportedException("Only nodal, element and contact field outputs are editable.")
        };
        clone.Active = output.Active;
        clone.LastIterations = output.LastIterations;
        clone.ContactElements = output.ContactElements;
        clone.Global = output.Global;
        return clone;
    }

    private static FieldOutputSummary Summary(string step, FieldOutput output)
    {
        string[] variables = output switch
        {
            NodalFieldOutput value => Selected(value.Variables),
            ElementFieldOutput value => Selected(value.Variables),
            ContactFieldOutput value => Selected(value.Variables),
            _ => []
        };
        string kind = output switch
        {
            NodalFieldOutput => FieldOutputKind.Nodal.ToString(),
            ElementFieldOutput => FieldOutputKind.Element.ToString(),
            ContactFieldOutput => FieldOutputKind.Contact.ToString(),
            _ => output.GetType().Name
        };
        return new(step, output.Name, output.Active, kind, variables, output.LastIterations,
            output.ContactElements, output.Global,
            output is ElementFieldOutput element ? element.Output.ToString() : ElementFieldOutputOutputEnum.Default.ToString());
    }

    private static long ParseVariables(FieldOutputKind kind, string[] variables)
    {
        if (variables is not { Length: > 0 } || variables.Length > 32 || variables.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Select at least one field variable.");
        long result = kind switch
        {
            FieldOutputKind.Nodal => Parse<NodalFieldVariable>(variables),
            FieldOutputKind.Element => Parse<ElementFieldVariable>(variables),
            FieldOutputKind.Contact => Parse<ContactFieldVariable>(variables),
            _ => 0
        };
        if (result == 0) throw new ArgumentException("Select at least one field variable.");
        return result;
    }

    private static long Parse<T>(string[] names) where T : struct, Enum
    {
        long result = 0;
        foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<T>(name.Trim(), true, out var value) || !Enum.IsDefined(value))
                throw new ArgumentException("Unsupported field variable: " + name);
            result |= Convert.ToInt64(value);
        }
        return result;
    }

    private static string[] Names<T>() where T : struct, Enum => Enum.GetNames<T>();
    private static string[] Selected<T>(T value) where T : struct, Enum
    {
        long bits = Convert.ToInt64(value);
        return Enum.GetValues<T>().Where(item => (bits & Convert.ToInt64(item)) != 0).Select(item => item.ToString()).ToArray();
    }

    private static FieldOutputKind Kind(FieldOutput output) => output switch
    {
        NodalFieldOutput => FieldOutputKind.Nodal,
        ElementFieldOutput => FieldOutputKind.Element,
        ContactFieldOutput => FieldOutputKind.Contact,
        _ => throw new NotSupportedException("Only nodal, element and contact field outputs are editable.")
    };
}
