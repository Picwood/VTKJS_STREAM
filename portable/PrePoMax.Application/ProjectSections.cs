using CaeGlobals;
using CaeMesh;
using CaeModel;
using PrePoMax.Persistence;
using System.Text.Json.Serialization;

namespace PrePoMax.Application;

public sealed record SolidSectionSettings(string Name, string Material,
    [property: JsonConverter(typeof(JsonStringEnumConverter<RegionTypeEnum>))] RegionTypeEnum RegionType, string Region);
public sealed record SectionRegion(string Name, string RegionType, int Elements);
public sealed record SectionMaterial(string Name, bool Active);
public sealed record SolidSectionSummary(string Name, bool Active, string Material, string RegionType,
    string Region, int Elements, int EffectiveElements, string? Error);
public sealed record SectionCoverage(int ActiveElements, int UnassignedElements, int OverlappingElements,
    int InvalidRegions, int InvalidMaterials)
{
    public bool CanSolve => ActiveElements > 0 && UnassignedElements == 0 && InvalidRegions == 0 && InvalidMaterials == 0;
}
public sealed record SectionEditorData(SolidSectionSummary[] Sections, SectionRegion[] Regions,
    SectionMaterial[] Materials, SectionCoverage Coverage, string? EditingError);

/// <summary>Named-region solid section operations using production sections and export order.</summary>
public static class ProjectSections
{
    public static SectionEditorData Read(string path)
    {
        var project = StaticProjectArchive.Open(path);
        var model = project.Model;
        var analysis = Analyze(model);
        var regions = model.Mesh.Parts.Keys.Select(name => (name, kind: RegionTypeEnum.PartName))
            .Concat(model.Mesh.ElementSets.Values
                .Select(set => (name: set.Name, kind: RegionTypeEnum.ElementSetName)))
            .Select(item => new SectionRegion(item.name, item.kind.ToString(), Resolve(model, item.kind, item.name).Length))
            .ToArray();
        return new(analysis.Sections, regions, model.Materials.Values.Select(material => new SectionMaterial(material.Name, material.Active)).ToArray(),
            analysis.Coverage, EditingError(model));
    }

    public static SectionCoverage Coverage(FeModel model) => Analyze(model).Coverage;

    public static void AddCopy(string source, string destination, SolidSectionSettings settings)
    {
        var project = Editable(source);
        Validate(project.Model, settings);
        settings = Canonical(project.Model, settings);
        Unique(project.Model, settings.Name);
        project.Model.Sections.Add(settings.Name,
            new SolidSection(settings.Name, settings.Material, settings.Region, settings.RegionType, 1, false));
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveCopy(string source, string destination, string sectionName, SolidSectionSettings settings)
    {
        var project = Editable(source);
        var section = Find(project.Model, sectionName);
        Validate(project.Model, settings);
        settings = Canonical(project.Model, settings);
        Unique(project.Model, settings.Name, section);
        var replacement = new SolidSection(settings.Name, settings.Material, settings.Region, settings.RegionType, section.Thickness.Value, false)
            { Active = section.Active };
        project.Model.Sections.Replace(sectionName, settings.Name, replacement);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void DuplicateCopy(string source, string destination, string sectionName, string newName)
    {
        var project = Editable(source);
        var section = Find(project.Model, sectionName);
        var settings = new SolidSectionSettings(newName, section.MaterialName, section.RegionType, section.RegionName);
        Validate(project.Model, settings);
        Unique(project.Model, newName);
        project.Model.Sections.Add(newName, new SolidSection(newName, section.MaterialName, section.RegionName,
            section.RegionType, section.Thickness.Value, false) { Active = section.Active });
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SetActiveCopy(string source, string destination, string sectionName, bool active)
    {
        var project = Editable(source);
        Find(project.Model, sectionName).Active = active;
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveCopy(string source, string destination, string sectionName)
    {
        var project = Editable(source);
        var section = Find(project.Model, sectionName);
        project.Model.Sections.Remove(section.Name);
        // Named regions and materials can be shared by other definitions.
        ProjectEdits.SaveNew(destination, project);
    }

    private static string? EditingError(FeModel model) => model.CalculixUserKeywords?.Count > 0
        ? "Section editing is unavailable for projects with custom CalculiX keywords until their placement can be preserved."
        : null;

    private static ReopenedStaticProject Editable(string source)
    {
        var project = StaticProjectArchive.Open(source);
        if (EditingError(project.Model) is { } error) throw new NotSupportedException(error);
        return project;
    }

    private static SolidSection Find(FeModel model, string name) =>
        model.Sections.TryGetValue(name, out var section) && section is SolidSection solid && !solid.TwoD
            ? solid : throw new ArgumentException("Select an existing 3D solid section.");

    private static void Unique(FeModel model, string name, Section? except = null)
    {
        if (model.Sections.Values.Any(section => !ReferenceEquals(section, except) && section.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Section names must be unique, ignoring case.");
    }

    private static void Validate(FeModel model, SolidSectionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Name) || settings.Name != settings.Name.Trim() || settings.Name.Length > 128 || settings.Name.Any(char.IsControl))
            throw new ArgumentException("Enter a section name of 1–128 characters without surrounding whitespace or control characters.");
        if (!model.Materials.ContainsKey(settings.Material)) throw new ArgumentException("Select an existing material.");
        Resolve(model, settings.RegionType, settings.Region);
    }

    private static SolidSectionSettings Canonical(FeModel model, SolidSectionSettings settings) => settings with
    {
        Material = model.Materials[settings.Material].Name,
        Region = settings.RegionType == RegionTypeEnum.PartName
            ? model.Mesh.Parts[settings.Region].Name : model.Mesh.ElementSets[settings.Region].Name
    };

    private static int[] Resolve(FeModel model, RegionTypeEnum kind, string name)
    {
        int[] labels;
        if (kind == RegionTypeEnum.PartName && model.Mesh.Parts.TryGetValue(name, out var part)) labels = part.Labels;
        else if (kind == RegionTypeEnum.ElementSetName && model.Mesh.ElementSets.TryGetValue(name, out var set))
        {
            if (set.CreatedFromParts)
            {
                var parts = model.Mesh.Parts.Values.ToDictionary(part => part.PartId);
                if (set.Labels.Any(id => !parts.ContainsKey(id))) throw new ArgumentException("The region references a missing mesh part.");
                labels = set.Labels.SelectMany(id => parts[id].Labels).Distinct().ToArray();
            }
            else labels = set.Labels;
        }
        else throw new ArgumentException("Select an existing part or element set.");
        if (labels.Length == 0 || labels.Any(id => !model.Mesh.Elements.TryGetValue(id, out var element) || element is not FeElement3D))
            throw new ArgumentException("A solid section requires a nonempty region of 3D solid elements.");
        return labels;
    }

    private static (SolidSectionSummary[] Sections, SectionCoverage Coverage) Analyze(FeModel model)
    {
        var active = model.Mesh.Parts.Values.Where(part => part.Active).SelectMany(part => part.Labels).ToHashSet();
        var assignments = new Dictionary<int, string>();
        var overlaps = new HashSet<int>();
        var sections = new List<SolidSectionSummary>();
        int invalidRegions = 0, invalidMaterials = 0;
        foreach (var section in model.Sections.Values)
        {
            int[] labels = [];
            string? error = null;
            try { labels = Resolve(model, section.RegionType, section.RegionName); }
            catch (ArgumentException ex) { error = ex.Message; if (section.Active) invalidRegions++; }
            if (!model.Materials.TryGetValue(section.MaterialName, out var material) || !material.Active)
            {
                error = "The assigned material is missing or inactive.";
                if (section.Active) invalidMaterials++;
            }
            if (section.Active)
                foreach (int id in labels)
                    if (active.Contains(id))
                    {
                        if (!assignments.TryAdd(id, section.Name)) { overlaps.Add(id); assignments[id] = section.Name; }
                    }
            sections.Add(new(section.Name, section.Active, section.MaterialName, section.RegionType.ToString(),
                section.RegionName, labels.Length, 0, error));
        }
        var effective = assignments.Values.GroupBy(name => name).ToDictionary(group => group.Key, group => group.Count());
        return (sections.Select(section => section with { EffectiveElements = effective.GetValueOrDefault(section.Name) }).ToArray(),
            new(active.Count, active.Count - assignments.Count, overlaps.Count, invalidRegions, invalidMaterials));
    }
}
