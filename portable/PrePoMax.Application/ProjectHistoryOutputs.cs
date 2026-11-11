using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;
using System.Text.Json.Serialization;

namespace PrePoMax.Application;

[JsonConverter(typeof(JsonStringEnumConverter<HistoryOutputKind>))]
public enum HistoryOutputKind { Nodal, Element, Contact, Section }

public sealed record HistoryOutputSettings(string Name, HistoryOutputKind Kind, string[] Variables,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RegionTypeEnum>))] RegionTypeEnum RegionType,
    string Region,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TotalsTypeEnum>))] TotalsTypeEnum Totals = TotalsTypeEnum.No,
    bool Global = true);
public sealed record HistoryOutputRegion(string Name, string RegionType, int Entities);
public sealed record HistoryOutputSummary(string Step, string Name, bool Active, string Kind, string[] Variables,
    string RegionType, string Region, string Totals, bool Global);
public sealed record HistoryOutputEditorData(string[] Steps, HistoryOutputSummary[] Outputs,
    HistoryOutputRegion[] Regions, Dictionary<string, string[]> VariableChoices, string? EditingError);

/// <summary>Original-style region history-output operations on supported static steps.</summary>
public static class ProjectHistoryOutputs
{
    private const int MaxPerStep = 1000;

    public static HistoryOutputEditorData Read(string path)
    {
        var project = StaticProjectArchive.Open(path);
        var model = project.Model;
        var steps = model.StepCollection.StepsList.OfType<StaticStep>().ToArray();
        return new(steps.Select(step => step.Name).ToArray(),
            steps.SelectMany(step => step.HistoryOutputs.Values.Select(output => Summary(step.Name, output))).ToArray(),
            Regions(model), new()
            {
                [HistoryOutputKind.Nodal.ToString()] = Names<NodalHistoryVariable>(),
                [HistoryOutputKind.Element.ToString()] = Names<ElementHistoryVariable>(),
                [HistoryOutputKind.Contact.ToString()] = Names<ContactHistoryVariable>(),
                [HistoryOutputKind.Section.ToString()] = Names<SectionHistoryVariableEnum>()
            }, EditingError(model));
    }

    public static void AddCopy(string source, string destination, string stepName, HistoryOutputSettings settings)
    {
        var project = Editable(source);
        var step = FindStep(project, stepName);
        Validate(project.Model, step, settings);
        if (step.HistoryOutputs.Count >= MaxPerStep) throw new NotSupportedException("A step supports at most 1,000 history outputs in this editor.");
        step.AddHistoryOutput(Create(settings));
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveCopy(string source, string destination, string stepName, string outputName, HistoryOutputSettings settings)
    {
        var project = Editable(source);
        var step = FindStep(project, stepName);
        var previous = Find(step, outputName);
        Validate(project.Model, step, settings, previous);
        if (Kind(previous) != settings.Kind) throw new ArgumentException("Editing retains the history-output type.");
        var replacement = Create(settings);
        replacement.Active = previous.Active;
        step.HistoryOutputs.Replace(previous.Name, replacement.Name, replacement);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void DuplicateCopy(string source, string destination, string stepName, string outputName, string newName)
    {
        var project = Editable(source);
        var step = FindStep(project, stepName);
        var original = Find(step, outputName);
        ValidateName(newName);
        Unique(step, newName);
        if (step.HistoryOutputs.Count >= MaxPerStep) throw new NotSupportedException("A step supports at most 1,000 history outputs in this editor.");
        var duplicate = Clone(original);
        duplicate.Name = newName;
        step.AddHistoryOutput(duplicate);
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
        step.HistoryOutputs.Remove(Find(step, outputName).Name);
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
            if (steps[i].HistoryOutputs.TryGetValue(outputName, out var existing))
                steps[i].HistoryOutputs.Replace(existing.Name, clone.Name, clone);
            else
            {
                if (steps[i].HistoryOutputs.Count >= MaxPerStep)
                    throw new NotSupportedException("A target step already has 1,000 history outputs.");
                steps[i].AddHistoryOutput(clone);
            }
        }
        ProjectEdits.SaveNew(destination, project);
    }

    private static HistoryOutputRegion[] Regions(FeModel model)
    {
        var mesh = model.Mesh;
        return mesh.NodeSets.Values.Where(set => set.Labels.Length > 0)
            .Select(set => new HistoryOutputRegion(set.Name, RegionTypeEnum.NodeSetName.ToString(), set.Labels.Length))
            .Concat(mesh.ElementSets.Values.Where(set => set.Labels.Length > 0)
                .Select(set => new HistoryOutputRegion(set.Name, RegionTypeEnum.ElementSetName.ToString(), set.Labels.Length)))
            .Concat(mesh.Surfaces.Values.Select(surface => new HistoryOutputRegion(surface.Name,
                RegionTypeEnum.SurfaceName.ToString(), SurfaceEntities(mesh, surface))))
            .Concat(mesh.ReferencePoints.Values.Select(point => new HistoryOutputRegion(point.Name,
                RegionTypeEnum.ReferencePointName.ToString(), 1)))
            .Concat(model.ContactPairs.Values.Select(pair => new HistoryOutputRegion(pair.Name,
                RegionTypeEnum.ContactPair.ToString(), 1))).ToArray();
    }

    private static int SurfaceEntities(FeMesh mesh, FeSurface surface) => surface.Type == FeSurfaceType.Node
        ? (surface.NodeSetName != null && mesh.NodeSets.TryGetValue(surface.NodeSetName, out var nodes) ? nodes.Labels.Length : 0)
        : surface.ElementFaces?.Values.Where(mesh.ElementSets.ContainsKey).Sum(name => mesh.ElementSets[name].Labels.Length) ?? 0;

    private static string? EditingError(FeModel model) => model.CalculixUserKeywords?.Count > 0
        ? "History-output editing is unavailable for projects with positional CalculiX keywords until their placement can be remapped."
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

    private static HistoryOutput Find(StaticStep step, string name) =>
        step.HistoryOutputs.TryGetValue(name, out var output) ? output
        : throw new ArgumentException("Select an existing history output.");

    private static void Validate(FeModel model, StaticStep step, HistoryOutputSettings settings, HistoryOutput? except = null)
    {
        ValidateName(settings.Name);
        Unique(step, settings.Name, except);
        if (!Enum.IsDefined(settings.Kind) || !Enum.IsDefined(settings.RegionType) || !Enum.IsDefined(settings.Totals))
            throw new ArgumentException("Select a supported history-output type, region and totals mode.");
        if (!ValidRegion(model, settings)) throw new ArgumentException("Select a nonempty region supported by this history-output type.");
        if (settings.Kind == HistoryOutputKind.Section && (settings.Totals != TotalsTypeEnum.No || !settings.Global))
            throw new ArgumentException("Section history output does not support totals or the global flag.");
        _ = ParseVariables(settings.Kind, settings.Variables);
    }

    private static bool ValidRegion(FeModel model, HistoryOutputSettings settings)
    {
        var mesh = model.Mesh;
        return (settings.Kind, settings.RegionType) switch
        {
            (HistoryOutputKind.Nodal, RegionTypeEnum.NodeSetName) => mesh.NodeSets.TryGetValue(settings.Region, out var nodes) && nodes.Labels.Length > 0,
            (HistoryOutputKind.Nodal, RegionTypeEnum.SurfaceName) => mesh.Surfaces.TryGetValue(settings.Region, out var nodalSurface) && SurfaceEntities(mesh, nodalSurface) > 0,
            (HistoryOutputKind.Nodal, RegionTypeEnum.ReferencePointName) => mesh.ReferencePoints.ContainsKey(settings.Region),
            (HistoryOutputKind.Element, RegionTypeEnum.ElementSetName) => mesh.ElementSets.TryGetValue(settings.Region, out var elements) && elements.Labels.Length > 0,
            (HistoryOutputKind.Contact, RegionTypeEnum.ContactPair) => model.ContactPairs.ContainsKey(settings.Region),
            (HistoryOutputKind.Section, RegionTypeEnum.SurfaceName) => mesh.Surfaces.TryGetValue(settings.Region, out var sectionSurface) &&
                sectionSurface.Type == FeSurfaceType.Element && SurfaceEntities(mesh, sectionSurface) > 0,
            _ => false
        };
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Length > 128 || name.Any(char.IsControl))
            throw new ArgumentException("Enter an output name of 1–128 characters without surrounding whitespace or control characters.");
    }

    private static void Unique(StaticStep step, string name, HistoryOutput? except = null)
    {
        if (step.HistoryOutputs.Values.Any(output => !ReferenceEquals(output, except) && output.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("History-output names must be unique within a step, ignoring case.");
    }

    private static HistoryOutput Create(HistoryOutputSettings settings)
    {
        long variables = ParseVariables(settings.Kind, settings.Variables);
        HistoryOutput output = settings.Kind switch
        {
            HistoryOutputKind.Nodal => new NodalHistoryOutput(settings.Name, (NodalHistoryVariable)variables, settings.Region, settings.RegionType),
            HistoryOutputKind.Element => new ElementHistoryOutput(settings.Name, (ElementHistoryVariable)variables, settings.Region, settings.RegionType),
            HistoryOutputKind.Contact => new ContactHistoryOutput(settings.Name, (ContactHistoryVariable)variables, settings.Region),
            HistoryOutputKind.Section => new SectionHistoryOutput(settings.Name, (SectionHistoryVariableEnum)variables, settings.Region, settings.RegionType),
            _ => throw new ArgumentException("Select a supported history-output type.")
        };
        output.TotalsType = settings.Totals;
        output.Global = settings.Global;
        return output;
    }

    private static HistoryOutput Clone(HistoryOutput output)
    {
        HistoryOutput clone = output switch
        {
            NodalHistoryOutput value => new NodalHistoryOutput(value.Name, value.Variables, value.RegionName, value.RegionType),
            ElementHistoryOutput value => new ElementHistoryOutput(value.Name, value.Variables, value.RegionName, value.RegionType),
            ContactHistoryOutput value => new ContactHistoryOutput(value.Name, value.Variables, value.RegionName),
            SectionHistoryOutput value => new SectionHistoryOutput(value.Name, value.Variables, value.RegionName, value.RegionType),
            _ => throw new NotSupportedException("Only nodal, element, contact and section history outputs are editable.")
        };
        clone.Active = output.Active;
        clone.TotalsType = output.TotalsType;
        clone.Global = output.Global;
        return clone;
    }

    private static HistoryOutputSummary Summary(string step, HistoryOutput output)
    {
        string[] variables = output switch
        {
            NodalHistoryOutput value => Selected(value.Variables),
            ElementHistoryOutput value => Selected(value.Variables),
            ContactHistoryOutput value => Selected(value.Variables),
            SectionHistoryOutput value => Selected(value.Variables),
            _ => []
        };
        string kind = output switch
        {
            NodalHistoryOutput => HistoryOutputKind.Nodal.ToString(),
            ElementHistoryOutput => HistoryOutputKind.Element.ToString(),
            ContactHistoryOutput => HistoryOutputKind.Contact.ToString(),
            SectionHistoryOutput => HistoryOutputKind.Section.ToString(),
            _ => output.GetType().Name
        };
        return new(step, output.Name, output.Active, kind, variables, output.RegionType.ToString(), output.RegionName,
            output.TotalsType.ToString(), output.Global);
    }

    private static long ParseVariables(HistoryOutputKind kind, string[] variables)
    {
        if (variables is not { Length: > 0 } || variables.Length > 32 || variables.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Select at least one history variable.");
        long result = kind switch
        {
            HistoryOutputKind.Nodal => Parse<NodalHistoryVariable>(variables),
            HistoryOutputKind.Element => Parse<ElementHistoryVariable>(variables),
            HistoryOutputKind.Contact => Parse<ContactHistoryVariable>(variables),
            HistoryOutputKind.Section => Parse<SectionHistoryVariableEnum>(variables),
            _ => 0
        };
        if (result == 0) throw new ArgumentException("Select at least one history variable.");
        return result;
    }

    private static long Parse<T>(string[] names) where T : struct, Enum
    {
        long result = 0;
        foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<T>(name.Trim(), true, out var value) || !Enum.IsDefined(value))
                throw new ArgumentException("Unsupported history variable: " + name);
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

    private static HistoryOutputKind Kind(HistoryOutput output) => output switch
    {
        NodalHistoryOutput => HistoryOutputKind.Nodal,
        ElementHistoryOutput => HistoryOutputKind.Element,
        ContactHistoryOutput => HistoryOutputKind.Contact,
        SectionHistoryOutput => HistoryOutputKind.Section,
        _ => throw new NotSupportedException("Only nodal, element, contact and section history outputs are editable.")
    };
}
