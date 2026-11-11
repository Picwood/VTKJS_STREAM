using CaeGlobals;
using CaeModel;
using System.Text.Json;
using PrePoMax.Application;
using PrePoMax.Persistence;

if (args.Length != 3 || args[0] is not ("prepare" or "prepare-material" or "prepare-identity" or "read-material" or "read-identity"))
    throw new ArgumentException("Usage: Parity.Probe prepare|prepare-material MESH.inp NEW.pma; read-material PROJECT.pma NEW.json");
string destination = Path.GetFullPath(args[2]);
if (File.Exists(destination)) throw new IOException("Refusing to overwrite probe output.");
if (args[0] is "read-material" or "read-identity")
{
    var opened = StaticProjectArchive.Open(args[1]);
    object Property(MaterialProperty property)
    {
        double[][] Table(EquationContainer[][] rows) => rows.Select(row => row.Select(cell => cell.Value).ToArray()).ToArray();
        return property switch
        {
            Elastic item => new { kind = "Elastic", data = Table(item.YoungsPoissonsTemp) },
            Density item => new { kind = "Density", data = Table(item.DensityTemp) },
            Plastic item => new { kind = "Plastic", data = Table(item.StressStrainTemp), hardening = item.Hardening.ToString() },
            ThermalExpansion item => new { kind = "ThermalExpansion", data = Table(item.ThermalExpansionTemp), zeroTemperature = item.ZeroTemperature.Value },
            _ => throw new NotSupportedException("Unexpected fixture property.")
        };
    }
    object Material(Material value) => new {
        name = value.Name, description = value.Description, active = value.Active,
        temperatureDependent = value.TemperatureDependent, properties = value.Properties.Select(Property).ToArray()
    };
    object snapshot = args[0] == "read-material" ? Material(opened.Model.Materials["ELASTIC"]) : new {
        materials = opened.Model.Materials.Values.Select(Material).ToArray(),
        sections = opened.Model.Sections.Values.Select(section => new { name = section.Name, material = section.MaterialName }).ToArray()
    };
    File.WriteAllText(destination, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
string temporary = Path.Combine(Path.GetDirectoryName(destination)!, Guid.NewGuid().ToString("N") + ".pma");
try
{
    ProjectMeshImport.Import(Path.GetFullPath(args[1]), temporary, 1000, 0);
    var project = StaticProjectArchive.Open(temporary);
    // A controlled fixture, not an assertion that the current mesh-import UI offers units.
    // The reference CLI requires a defined unit system; both adapters receive this same state.
    project.Model.UnitSystem = new UnitSystem(UnitSystemType.M_KG_S_C);
    if (args[0] is "prepare-material" or "prepare-identity")
    {
        var material = project.Model.Materials["ELASTIC"];
        material.Description = "keep this description";
        material.Properties.Insert(0, new Density([[2.5, 0]]));
        material.AddProperty(new Plastic([[100, 0, 0], [120, .1, 0]]) { Hardening = PlasticHardening.Combined });
        material.AddProperty(new ThermalExpansion([[1.2e-5, 0]]) { ZeroTemperature = new EquationContainer(typeof(StringTemperatureConverter), -40) });
    }
    if (args[0] == "prepare-identity")
    {
        var other = new Material("OTHER");
        other.AddProperty(new Elastic([[500, .1, 0]]));
        project.Model.Materials.Add(other.Name, other);
    }
    StaticProjectArchive.Save(destination, project.Model, materialIdentities: ProjectMaterialIdentities.Reconcile(project.Model, project.MaterialIdentities));
}
finally { if (File.Exists(temporary)) File.Delete(temporary); }
