using CaeModel;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record ElasticMaterial(string Name, double YoungsModulus, double PoissonsRatio, double? Density);
public sealed record SolidSectionMaterial(string Section, string Region, string Material);
public sealed record PlasticPoint(double YieldStress, double PlasticStrain);
public sealed record MaterialPlasticity(string Name, string? Hardening, PlasticPoint[] Points);

/// <summary>Edits and solid-section assignment of constant elastic materials in supported static archives.</summary>
public static class ProjectMaterials
{
    private const int MaxPlasticPoints = 1000;

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() ||
            !CaeGlobals.NamedClass.CheckName(name, new HashSet<char> { '@' }))
            throw new ArgumentException("Enter a valid material name without surrounding spaces.", nameof(name));
    }

    private static void RefuseOpaqueReferences(FeModel model, string name)
    {
        if (model.CalculixUserKeywords?.Values.Any(keyword => keyword.Data.Contains(name, StringComparison.OrdinalIgnoreCase)) == true)
            throw new NotSupportedException("A custom solver keyword mentions this material. Resolve that keyword first.");
    }

    public static void RenameCopy(string source, string destination, string name, string newName)
    {
        ValidateName(newName);
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        if (!model.Materials.TryGetValue(name, out var material))
            throw new NotSupportedException("Select an existing material.");
        string oldName = material.Name;
        if (newName == oldName || model.Materials.Values.Any(other => !ReferenceEquals(other, material) &&
            other.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Choose a different, unique material name.", nameof(newName));
        // Raw solver text has no typed reference graph. Refuse a possible reference rather
        // than renaming its target while preserving an opaque, potentially dangling keyword.
        RefuseOpaqueReferences(model, oldName);
        material.Name = newName;
        model.Materials.Replace(oldName, newName, material);
        foreach (var section in model.Sections.Values)
            if (section.MaterialName.Equals(oldName, StringComparison.OrdinalIgnoreCase)) section.MaterialName = newName;
        var identities = project.MaterialIdentities.Select(item => item.Name == oldName ? item with { Name = newName } : item).ToArray();
        ProjectEdits.SaveNew(destination, project with { MaterialIdentities = identities });
    }

    public static ElasticMaterial[] List(string path) => StaticProjectArchive.Open(path).Model.Materials.Values
        .Where(material => !material.TemperatureDependent && material.Properties.OfType<Elastic>().Count() == 1)
        .Where(material => material.Properties.OfType<Elastic>().Single().YoungsPoissonsTemp.Length == 1)
        .Where(material => material.Properties.OfType<Density>().Count() <= 1)
        .Where(material => material.Properties.OfType<Density>().SingleOrDefault()?.DensityTemp.Length is null or 1)
        .Select(material => {
            var row = material.Properties.OfType<Elastic>().Single().YoungsPoissonsTemp[0];
            var density = material.Properties.OfType<Density>().SingleOrDefault();
            return new ElasticMaterial(material.Name, row[0].Value, row[1].Value, density?.DensityTemp[0][0].Value);
        }).ToArray();

    public static SolidSectionMaterial[] ListSolidSections(string path) => StaticProjectArchive.Open(path).Model.Sections.Values
        .OfType<SolidSection>()
        .Select(section => new SolidSectionMaterial(section.Name, section.RegionName, section.MaterialName))
        .ToArray();

    public static MaterialPlasticity[] ListPlasticity(string path) => StaticProjectArchive.Open(path).Model.Materials.Values
        .Where(material => !material.TemperatureDependent && material.Properties.OfType<Elastic>().Count() == 1)
        .Where(material => material.Properties.OfType<Elastic>().Single().YoungsPoissonsTemp.Length == 1)
        .Select(material =>
        {
            var plastic = material.Properties.OfType<Plastic>().SingleOrDefault();
            return new MaterialPlasticity(material.Name, plastic?.Hardening.ToString(), plastic == null ? [] :
                plastic.StressStrainTemp.Select(row => new PlasticPoint(row[0].Value, row[1].Value)).ToArray());
        }).ToArray();

    public static void SavePlasticityCopy(string source, string destination, string materialName,
                                           PlasticHardening hardening, PlasticPoint[] points)
    {
        ValidatePlasticity(hardening, points);
        var project = StaticProjectArchive.Open(source);
        if (!project.Model.Materials.TryGetValue(materialName, out var material) || material.TemperatureDependent ||
            material.Properties.OfType<Elastic>().Count() != 1 ||
            material.Properties.OfType<Elastic>().Single().YoungsPoissonsTemp.Length != 1 ||
            material.Properties.OfType<Plastic>().Count() > 1)
            throw new NotSupportedException("Select a non-temperature-dependent elastic material.");
        var replacement = new Plastic(points.Select(point => new[] { point.YieldStress, point.PlasticStrain, 0d }).ToArray())
            { Hardening = hardening };
        var existing = material.Properties.OfType<Plastic>().SingleOrDefault();
        if (existing == null)
        {
            int elasticIndex = material.Properties.FindIndex(property => property is Elastic);
            material.Properties.Insert(elasticIndex + 1, replacement);
        }
        else material.Properties[material.Properties.IndexOf(existing)] = replacement;
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemovePlasticityCopy(string source, string destination, string materialName)
    {
        var project = StaticProjectArchive.Open(source);
        if (!project.Model.Materials.TryGetValue(materialName, out var material) || material.TemperatureDependent ||
            material.Properties.OfType<Plastic>().Count() != 1)
            throw new NotSupportedException("Select a non-temperature-dependent material with plasticity.");
        material.Properties.Remove(material.Properties.OfType<Plastic>().Single());
        ProjectEdits.SaveNew(destination, project);
    }

    public static void AddElasticCopy(string source, string destination, string materialName,
                                       double youngsModulus, double poissonsRatio)
        => AddElasticCore(source, destination, null, materialName, youngsModulus, poissonsRatio);

    public static void AddElasticAndAssignCopy(string source, string destination, string sectionName, string materialName,
                                                double youngsModulus, double poissonsRatio)
        => AddElasticCore(source, destination, sectionName, materialName, youngsModulus, poissonsRatio);

    private static void AddElasticCore(string source, string destination, string? sectionName, string materialName,
                                        double youngsModulus, double poissonsRatio)
    {
        ValidateElastic(youngsModulus, poissonsRatio);
        ValidateName(materialName);
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        if (model.Materials.Keys.Any(name => name.Equals(materialName, StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Enter a unique material name.");
        SolidSection? section = null;
        if (sectionName != null)
        {
            if (!model.Sections.TryGetValue(sectionName, out var selected) || selected is not SolidSection solid)
                throw new NotSupportedException("Choose an existing solid section.");
            section = solid;
        }
        var material = new Material(materialName);
        material.AddProperty(new Elastic(new[] { new[] { youngsModulus, poissonsRatio, 0d } }));
        model.Materials.Add(material.Name, material);
        if (section != null) section.MaterialName = material.Name;
        ProjectEdits.SaveNew(destination, project);
    }

    public static void AssignSolidSectionCopy(string source, string destination, string sectionName, string materialName)
    {
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        if (!model.Sections.TryGetValue(sectionName, out var selected) || selected is not SolidSection section ||
            !model.Materials.TryGetValue(materialName, out var material) || section.MaterialName == material.Name)
            throw new NotSupportedException("Choose a solid section and a different existing material.");
        section.MaterialName = material.Name;
        ProjectEdits.SaveNew(destination, project);
    }

    public static void RemoveUnusedCopy(string source, string destination, string materialName)
    {
        var project = StaticProjectArchive.Open(source);
        var model = project.Model;
        if (!model.Materials.TryGetValue(materialName, out var material) ||
            model.Sections.Values.Any(section => section.MaterialName.Equals(material.Name, StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Choose an existing material that is not assigned to a section.");
        RefuseOpaqueReferences(model, material.Name);
        model.Materials.Remove(material.Name);
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveElasticCopy(string source, string destination, string name, double youngsModulus, double poissonsRatio)
        => PatchElasticCopy(source, destination, name, youngsModulus, poissonsRatio);

    /// <summary>Omitted elastic values and every other material property retain their stored values.</summary>
    public static void PatchElasticCopy(string source, string destination, string name, double? youngsModulus, double? poissonsRatio)
    {
        if (youngsModulus == null && poissonsRatio == null)
            throw new ArgumentException("Supply at least one elastic value.");
        var project = StaticProjectArchive.Open(source);
        if (!project.Model.Materials.TryGetValue(name, out var material) || material.TemperatureDependent ||
            material.Properties.OfType<Elastic>().Count() != 1)
            throw new NotSupportedException("Select a constant elastic material.");
        var elastic = material.Properties.OfType<Elastic>().Single();
        if (elastic.YoungsPoissonsTemp.Length != 1) throw new NotSupportedException("Temperature tables cannot be edited here.");
        double e = youngsModulus ?? elastic.YoungsPoissonsTemp[0][0].Value;
        double nu = poissonsRatio ?? elastic.YoungsPoissonsTemp[0][1].Value;
        ValidateElastic(e, nu);
        material.Properties[material.Properties.IndexOf(elastic)] = new Elastic(new[] {
            new[] { e, nu, elastic.YoungsPoissonsTemp[0][2].Value } });
        ProjectEdits.SaveNew(destination, project);
    }

    public static void SaveElasticDensityCopy(string source, string destination, string name, double youngsModulus,
                                               double poissonsRatio, double? densityValue)
    {
        ValidateElastic(youngsModulus, poissonsRatio);
        if (densityValue.HasValue && (!double.IsFinite(densityValue.Value) || densityValue.Value <= 0))
            throw new ArgumentOutOfRangeException(nameof(densityValue), "Density must be finite and positive.");
        var project = StaticProjectArchive.Open(source);
        if (!project.Model.Materials.TryGetValue(name, out var material) || material.TemperatureDependent ||
            material.Properties.OfType<Elastic>().Count() != 1 || material.Properties.OfType<Density>().Count() > 1)
            throw new NotSupportedException("Select a constant elastic material with at most one constant density.");
        var elastic = material.Properties.OfType<Elastic>().Single();
        var density = material.Properties.OfType<Density>().SingleOrDefault();
        if (elastic.YoungsPoissonsTemp.Length != 1 || density?.DensityTemp.Length is > 1)
            throw new NotSupportedException("Temperature tables cannot be edited here.");
        material.Properties[material.Properties.IndexOf(elastic)] = new Elastic(new[] {
            new[] { youngsModulus, poissonsRatio, elastic.YoungsPoissonsTemp[0][2].Value } });
        if (densityValue.HasValue)
        {
            var replacement = new Density(new[] { new[] { densityValue.Value, density?.DensityTemp[0][1].Value ?? 0d } });
            if (density == null) material.AddProperty(replacement);
            else material.Properties[material.Properties.IndexOf(density)] = replacement;
        }
        else if (density != null) material.Properties.Remove(density);
        ProjectEdits.SaveNew(destination, project);
    }

    private static void ValidateElastic(double youngsModulus, double poissonsRatio)
    {
        if (!double.IsFinite(youngsModulus) || youngsModulus <= 0 || !double.IsFinite(poissonsRatio)
            || poissonsRatio <= -1 || poissonsRatio >= 0.5)
            throw new ArgumentOutOfRangeException(nameof(youngsModulus), "Require E > 0 and -1 < nu < 0.5, both finite.");
    }

    private static void ValidatePlasticity(PlasticHardening hardening, PlasticPoint[] points)
    {
        if (!Enum.IsDefined(hardening)) throw new ArgumentOutOfRangeException(nameof(hardening));
        if (points == null || points.Length is < 1 or > MaxPlasticPoints)
            throw new ArgumentException("Plasticity requires between 1 and 1,000 points.", nameof(points));
        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (!double.IsFinite(point.YieldStress) || point.YieldStress <= 0 ||
                !double.IsFinite(point.PlasticStrain) || point.PlasticStrain < 0)
                throw new ArgumentOutOfRangeException(nameof(points), "Yield stress must be positive and plastic strain nonnegative.");
            if (i == 0 && point.PlasticStrain != 0)
                throw new ArgumentException("The first plastic strain must be zero.", nameof(points));
            if (i > 0 && point.PlasticStrain <= points[i - 1].PlasticStrain)
                throw new ArgumentException("Plastic strain must increase strictly.", nameof(points));
        }
    }
}
