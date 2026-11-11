using System.Text.Json;
using PrePoMax.Application;
using CaeModel;
using CaeGlobals;

if (args.Length == 1 && args[0] == "--help")
{
    Usage();
    return 0;
}
if (args.Length < 2 || !((args[0] == "render-check" && args.Length == 4) || (args[0] is "inspect" or "fields" or "cad-topology" or "steps" or "sections" or "field-outputs" or "history-outputs" or "history-results" or "plasticity" && args.Length == 2) ||
    (args[0] == "history-series" && args.Length == 5) ||
    (args[0] == "export-history" && args.Length is 3 or 5) ||
    (args[0] == "result-fingerprint" && args.Length == 8) ||
    (args[0] == "result-scene" && args.Length == 9) ||
    (args[0] is "export" or "scene" or "extract-geometry" && args.Length == 3) || (args[0] == "solve" && args.Length is 3 or 4) ||
    (args[0] == "import-mesh" && args.Length == 5) || (args[0] == "mesh-cad" && args.Length >= 6) ||
    (args[0] == "remesh-cad" && args.Length >= 3) ||
    (args[0] == "add-step" && args.Length >= 4) || (args[0] == "edit-step" && args.Length >= 5) ||
    (args[0] is "duplicate-step" or "step-active" && args.Length == 5) ||
    (args[0] == "remove-step" && args.Length == 4) ||
    (args[0] == "add-section" && args.Length == 7) || (args[0] == "edit-section" && args.Length == 8) ||
    (args[0] is "duplicate-section" or "section-active" && args.Length == 5) ||
    (args[0] == "remove-section" && args.Length == 4) || (args[0] == "add-material" && args.Length == 6) ||
    (args[0] == "set-plasticity" && args.Length == 6) || (args[0] == "remove-plasticity" && args.Length == 4) ||
    (args[0] == "add-field-output" && args.Length >= 7) || (args[0] == "edit-field-output" && args.Length >= 8) ||
    (args[0] is "duplicate-field-output" or "field-output-active" && args.Length == 6) ||
    (args[0] is "propagate-field-output" or "remove-field-output" && args.Length == 5) ||
    (args[0] == "add-history-output" && args.Length >= 9) || (args[0] == "edit-history-output" && args.Length >= 10) ||
    (args[0] is "duplicate-history-output" or "history-output-active" && args.Length == 6) ||
    (args[0] is "propagate-history-output" or "remove-history-output" && args.Length == 5) ||
    (args[0] == "element-sets" && args.Length is 2 or 3) ||
    (args[0] == "add-element-set" && args.Length == 6) || (args[0] == "edit-element-set" && args.Length == 7) ||
    (args[0] == "duplicate-element-set" && args.Length == 5) || (args[0] == "remove-element-set" && args.Length == 4) ||
    (args[0] is "node-sets" or "surfaces" && args.Length is 2 or 3) ||
    (args[0] == "add-node-set" && args.Length is 6 or 11) || (args[0] == "edit-node-set" && args.Length is 7 or 12) ||
    (args[0] is "duplicate-node-set" or "duplicate-surface" && args.Length == 5) ||
    (args[0] is "remove-node-set" or "remove-surface" && args.Length == 4) ||
    (args[0] == "add-surface" && args.Length == 5) || (args[0] == "edit-surface" && args.Length == 6)))
{
    Usage();
    return 2;
}
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += cancel;
try
{
    if (args[0] is "node-sets" or "surfaces")
    {
        string? name = args.Length == 3 ? args[2] : null;
        string json = args[0] == "node-sets" ? JsonSerializer.Serialize(ProjectNodeSets.Read(args[1], name), new JsonSerializerOptions { WriteIndented = true })
            : JsonSerializer.Serialize(ProjectSurfaces.Read(args[1], name), new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        return 0;
    }
    if (args[0] is "add-node-set" or "edit-node-set" or "duplicate-node-set" or "remove-node-set")
    {
        NodeSetSettings Settings(int index)
        {
            string mode = args[index + 1].ToLowerInvariant();
            string[] values = args.Skip(index + 2).ToArray();
            if (mode == "ids" && values.Length == 1) return new(args[index], values[0]);
            if (mode == "keep" && values.SequenceEqual(new[] { "-" })) return new(args[index]);
            if (mode == "box" && values.Length == 6)
            {
                double[] bounds = values.Select(value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                return new(args[index], Box: new(bounds[0], bounds[1], bounds[2], bounds[3], bounds[4], bounds[5]));
            }
            throw new ArgumentException("Choose ids EXPRESSION, box XMIN XMAX YMIN YMAX ZMIN ZMAX or keep -.");
        }
        if (args[0] == "add-node-set") ProjectNodeSets.AddCopy(args[1], args[2], Settings(3));
        else if (args[0] == "edit-node-set") ProjectNodeSets.SaveCopy(args[1], args[2], args[3], Settings(4));
        else if (args[0] == "duplicate-node-set") ProjectNodeSets.DuplicateCopy(args[1], args[2], args[3], args[4]);
        else ProjectNodeSets.RemoveCopy(args[1], args[2], args[3]);
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] is "add-surface" or "edit-surface" or "duplicate-surface" or "remove-surface")
    {
        SurfaceSettings Settings(int index)
        {
            string text = args[index + 1];
            if (text == "-") return new(args[index]);
            var faces = text.Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(token =>
            {
                string[] pair = token.Split(':');
                if (pair.Length != 2) throw new ArgumentException("Use element:face pairs, for example 1:S4.");
                return new ProjectElementFace(int.Parse(pair[0], System.Globalization.CultureInfo.InvariantCulture), pair[1]);
            }).ToArray();
            return new(args[index], faces);
        }
        if (args[0] == "add-surface")
        {
            var settings = Settings(3);
            ProjectSurfaces.AddCopy(args[1], args[2], settings.Name, settings.Faces!);
        }
        else if (args[0] == "edit-surface") ProjectSurfaces.SaveCopy(args[1], args[2], args[3], Settings(4));
        else if (args[0] == "duplicate-surface") ProjectSurfaces.DuplicateCopy(args[1], args[2], args[3], args[4]);
        else ProjectSurfaces.RemoveCopy(args[1], args[2], args[3]);
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] == "element-sets")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectElementSets.Read(args[1], args.Length == 3 ? args[2] : null),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] is "add-element-set" or "edit-element-set" or "duplicate-element-set" or "remove-element-set")
    {
        ElementSetSettings Settings(int index) => args[index + 1].ToLowerInvariant() switch
        {
            "ids" => new(args[index], args[index + 2]),
            "parts" => new(args[index], Parts: args[index + 2].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)),
            "keep" when args[index + 2] == "-" => new(args[index]),
            _ => throw new ArgumentException("Choose ids EXPRESSION, parts NAME[,NAME...] or keep - for an existing set.")
        };
        if (args[0] == "add-element-set") ProjectElementSets.AddCopy(args[1], args[2], Settings(3));
        else if (args[0] == "edit-element-set") ProjectElementSets.SaveCopy(args[1], args[2], args[3], Settings(4));
        else if (args[0] == "duplicate-element-set") ProjectElementSets.DuplicateCopy(args[1], args[2], args[3], args[4]);
        else ProjectElementSets.RemoveCopy(args[1], args[2], args[3]);
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] == "history-results")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectHistoryResults.List(args[1]), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] == "history-series")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectHistoryResults.Read(args[1], args[2], args[3], args[4]),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] == "export-history")
    {
        string delimiter = args.Length == 3 ? "," : args[3] == "--delimiter" ? args[4].ToLowerInvariant() switch
        {
            "comma" => ",", "semicolon" => ";", "tab" => "\t",
            _ => throw new ArgumentException("History delimiter must be comma, semicolon or tab.")
        } : throw new ArgumentException("Unknown history export option: " + args[3]);
        ProjectHistoryResults.Export(args[1], args[2], delimiter: delimiter);
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] == "history-outputs")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectHistoryOutputs.Read(args[1]), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] is "add-history-output" or "edit-history-output" or "duplicate-history-output" or
        "history-output-active" or "propagate-history-output" or "remove-history-output")
    {
        if (args[0] == "add-history-output")
            ProjectHistoryOutputs.AddCopy(args[1], args[2], args[3], ParseHistoryOutput(args, 4, 5, 6, 7, 8, 9));
        else if (args[0] == "edit-history-output")
            ProjectHistoryOutputs.SaveCopy(args[1], args[2], args[3], args[4], ParseHistoryOutput(args, 5, 6, 7, 8, 9, 10));
        else if (args[0] == "duplicate-history-output")
            ProjectHistoryOutputs.DuplicateCopy(args[1], args[2], args[3], args[4], args[5]);
        else if (args[0] == "history-output-active")
            ProjectHistoryOutputs.SetActiveCopy(args[1], args[2], args[3], args[4], bool.Parse(args[5]));
        else if (args[0] == "propagate-history-output")
            ProjectHistoryOutputs.PropagateCopy(args[1], args[2], args[3], args[4]);
        else ProjectHistoryOutputs.RemoveCopy(args[1], args[2], args[3], args[4]);
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] == "field-outputs")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectFieldOutputs.Read(args[1]), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] is "add-field-output" or "edit-field-output" or "duplicate-field-output" or
        "field-output-active" or "propagate-field-output" or "remove-field-output")
    {
        if (args[0] == "add-field-output")
            ProjectFieldOutputs.AddCopy(args[1], args[2], args[3], ParseFieldOutput(args, 4, 5, 6, 7));
        else if (args[0] == "edit-field-output")
            ProjectFieldOutputs.SaveCopy(args[1], args[2], args[3], args[4], ParseFieldOutput(args, 5, 6, 7, 8));
        else if (args[0] == "duplicate-field-output")
            ProjectFieldOutputs.DuplicateCopy(args[1], args[2], args[3], args[4], args[5]);
        else if (args[0] == "field-output-active")
            ProjectFieldOutputs.SetActiveCopy(args[1], args[2], args[3], args[4], bool.Parse(args[5]));
        else if (args[0] == "propagate-field-output")
            ProjectFieldOutputs.PropagateCopy(args[1], args[2], args[3], args[4]);
        else ProjectFieldOutputs.RemoveCopy(args[1], args[2], args[3], args[4]);
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] == "sections")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectSections.Read(args[1]), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] == "add-material")
    {
        ProjectMaterials.AddElasticCopy(args[1], args[2], args[3],
            double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture));
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] == "plasticity")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectMaterials.ListPlasticity(args[1]),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] is "set-plasticity" or "remove-plasticity")
    {
        if (args[0] == "set-plasticity")
            ProjectMaterials.SavePlasticityCopy(args[1], args[2], args[3], ParsePlasticHardening(args[4]), ParsePlasticPoints(args[5]));
        else ProjectMaterials.RemovePlasticityCopy(args[1], args[2], args[3]);
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] is "add-section" or "edit-section" or "duplicate-section" or "section-active" or "remove-section")
    {
        if (args[0] == "add-section") ProjectSections.AddCopy(args[1], args[2], new(args[3], args[4], ParseSectionRegion(args[5]), args[6]));
        else if (args[0] == "edit-section") ProjectSections.SaveCopy(args[1], args[2], args[3], new(args[4], args[5], ParseSectionRegion(args[6]), args[7]));
        else if (args[0] == "duplicate-section") ProjectSections.DuplicateCopy(args[1], args[2], args[3], args[4]);
        else if (args[0] == "section-active") ProjectSections.SetActiveCopy(args[1], args[2], args[3], bool.Parse(args[4]));
        else ProjectSections.RemoveCopy(args[1], args[2], args[3]);
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] == "steps")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectSteps.List(args[1]), new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] is "add-step" or "edit-step" or "duplicate-step" or "step-active" or "remove-step")
    {
        if (args[0] == "duplicate-step") ProjectSteps.DuplicateCopy(args[1], args[2], args[3], args[4]);
        else if (args[0] == "step-active") ProjectSteps.SetActiveCopy(args[1], args[2], args[3], bool.Parse(args[4]));
        else if (args[0] == "remove-step") ProjectSteps.RemoveCopy(args[1], args[2], args[3]);
        else if (args[0] == "add-step")
        {
            var parsed = ParseStepOptions(args, 4, new(args[3]), true);
            ProjectSteps.AddCopy(args[1], args[2], parsed.Settings, parsed.CopyPrevious);
        }
        else
        {
            var previous = ProjectSteps.List(args[1]).SingleOrDefault(step => step.Name == args[3])
                ?? throw new ArgumentException("Select an existing static step.");
            var defaults = new StaticStepSettings(args[4], previous.Nlgeom,
                Enum.Parse<IncrementationTypeEnum>(previous.IncrementationType), previous.TimePeriod,
                previous.InitialTimeIncrement, previous.MinTimeIncrement, previous.MaxTimeIncrement, previous.MaxIncrements);
            ProjectSteps.SaveCopy(args[1], args[2], args[3], ParseStepOptions(args, 5, defaults, false).Settings);
        }
        Console.WriteLine(Path.GetFullPath(args[2]));
        return 0;
    }
    if (args[0] == "import-mesh")
    {
        ProjectMeshImport.Import(args[1], args[2],
            double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture));
        Console.WriteLine("Created editable mesh project. Add supports and loads before solving.");
        return 0;
    }
    if (args[0] == "mesh-cad")
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var settings = ParseGmshOptions(args, 6, double.Parse(args[5], culture));
        var gmshJob = ProjectGmshImport.Prepare(args[1], args[2], double.Parse(args[3], culture),
            double.Parse(args[4], culture), settings.Options, settings.Executable);
        var gmshResult = await gmshJob.RunAsync(cancellation.Token);
        Console.Error.WriteLine($"Gmsh project: {gmshResult.Status}; mesher elapsed {gmshJob.MesherElapsed}");
        if (gmshResult.Status != ProjectGmshStatus.Succeeded)
        {
            Console.Error.WriteLine(gmshResult.Error?.Message ?? gmshResult.Mesher?.Process?.StandardError);
            return gmshResult.Status == ProjectGmshStatus.Cancelled ? 130 : 1;
        }
        Console.WriteLine(gmshResult.ProjectPath);
        return 0;
    }
    if (args[0] == "remesh-cad")
    {
        ProjectGmshJob gmshJob;
        if (args.Length == 3) gmshJob = ProjectGmshRemesh.Prepare(args[1], args[2]);
        else
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var settings = ParseGmshOptions(args, 4, double.Parse(args[3], culture));
            gmshJob = ProjectGmshRemesh.Prepare(args[1], args[2], settings.Options, settings.Executable);
        }
        var gmshResult = await gmshJob.RunAsync(cancellation.Token);
        Console.Error.WriteLine($"Gmsh project: {gmshResult.Status}; mesher elapsed {gmshJob.MesherElapsed}");
        if (gmshResult.Status != ProjectGmshStatus.Succeeded)
        {
            Console.Error.WriteLine(gmshResult.Error?.Message ?? gmshResult.Mesher?.Process?.StandardError);
            return gmshResult.Status == ProjectGmshStatus.Cancelled ? 130 : 1;
        }
        Console.WriteLine(gmshResult.ProjectPath);
        return 0;
    }
    if (args[0] == "render-check")
    {
        await using var renderer = await RendererHost.StartAsync(args[2], args[3], cancellation.Token);
        Console.WriteLine(JsonSerializer.Serialize(await renderer.PublishAsync(args[1], cancellation.Token)));
        return 0;
    }
    if (args[0] == "cad-topology")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectService.CadTopology(args[1]),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] == "inspect")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectService.Inspect(args[1]),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] == "fields")
    {
        Console.WriteLine(JsonSerializer.Serialize(ProjectResults.List(args[1]),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] == "result-scene")
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var field = new ResultField(args[3], args[4], int.Parse(args[5], culture), int.Parse(args[6], culture),
            float.Parse(args[7], culture));
        ProjectScene.Export(args[1], args[2], field, double.Parse(args[8], culture));
        return 0;
    }
    if (args[0] == "result-fingerprint")
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var field = new ResultField(args[2], args[3], int.Parse(args[4], culture), int.Parse(args[5], culture),
            float.Parse(args[6], culture));
        if (args[7] != "--json") throw new ArgumentException("Result fingerprint output must be --json.");
        Console.WriteLine(JsonSerializer.Serialize(ProjectResults.Fingerprint(args[1], field),
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    if (args[0] == "scene")
    {
        ProjectScene.Export(args[1], args[2]);
        return 0;
    }
    if (args[0] == "export")
    {
        ProjectService.Export(args[1], args[2]);
        return 0;
    }
    if (args[0] == "extract-geometry")
    {
        ProjectService.ExportGeometry(args[1], args[2]);
        return 0;
    }
    var job = ProjectService.PrepareSolve(args[1], args[2], args.Length == 4 ? args[3] : null);
    Console.Error.WriteLine($"Job files: {job.JobDirectory}");
    var result = await job.RunAsync(cancellation.Token);
    Console.Error.WriteLine($"Project: {result.Status}; solver elapsed {job.SolverElapsed}");
    if (result.Status != ProjectSolveStatus.Succeeded)
    {
        Console.Error.WriteLine(result.Error?.Message ?? result.Solver?.Process?.StandardError);
        return result.Status == ProjectSolveStatus.Cancelled ? 130 : 1;
    }
    Console.WriteLine(result.ArchivePath);
    return 0;
}
catch (OperationCanceledException)
{
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
finally
{
    Console.CancelKeyPress -= cancel;
}

static void Usage() => Console.Error.WriteLine("""
Usage:
  PrePoMax.Cli import-mesh SOURCE.inp_OR_mesh_OR_msh NEW_PROJECT.pma YOUNGS_MODULUS POISSONS_RATIO
  PrePoMax.Cli mesh-cad SOURCE.step NEW_PROJECT.pma YOUNGS_MODULUS POISSONS_RATIO MAX_ELEMENT_SIZE [GMSH_EXECUTABLE] [GMSH_OPTIONS]
  PrePoMax.Cli remesh-cad PROJECT.pma NEW_PROJECT.pma [MAX_ELEMENT_SIZE [GMSH_EXECUTABLE] [GMSH_OPTIONS]]
  PrePoMax.Cli render-check PROJECT.pma DOTNET_EXECUTABLE RENDER_SERVER.dll
  PrePoMax.Cli inspect PROJECT.pma
  PrePoMax.Cli fields PROJECT.pma
  PrePoMax.Cli result-fingerprint PROJECT.pma FIELD COMPONENT STEP INCREMENT TIME --json
  PrePoMax.Cli result-scene PROJECT.pma NEW_SCENE.bin FIELD COMPONENT STEP INCREMENT TIME DEFORMATION_SCALE
  PrePoMax.Cli sections PROJECT.pma
  PrePoMax.Cli element-sets PROJECT.pma [NAME]
  PrePoMax.Cli node-sets PROJECT.pma [NAME]
  PrePoMax.Cli add-node-set PROJECT.pma NEW_PROJECT.pma NAME ids EXPRESSION
  PrePoMax.Cli add-node-set PROJECT.pma NEW_PROJECT.pma NAME box XMIN XMAX YMIN YMAX ZMIN ZMAX
  PrePoMax.Cli edit-node-set PROJECT.pma NEW_PROJECT.pma OLD_NAME NEW_NAME ids|box|keep MEMBERS
  PrePoMax.Cli duplicate-node-set PROJECT.pma NEW_PROJECT.pma NAME NEW_NAME
  PrePoMax.Cli remove-node-set PROJECT.pma NEW_PROJECT.pma NAME
  PrePoMax.Cli surfaces PROJECT.pma [NAME]
  PrePoMax.Cli add-surface PROJECT.pma NEW_PROJECT.pma NAME ELEMENT:FACE[,ELEMENT:FACE...]
  PrePoMax.Cli edit-surface PROJECT.pma NEW_PROJECT.pma OLD_NAME NEW_NAME FACES_OR_-
  PrePoMax.Cli duplicate-surface PROJECT.pma NEW_PROJECT.pma NAME NEW_NAME
  PrePoMax.Cli remove-surface PROJECT.pma NEW_PROJECT.pma NAME
  PrePoMax.Cli add-element-set PROJECT.pma NEW_PROJECT.pma NAME ids|parts MEMBERS
  PrePoMax.Cli edit-element-set PROJECT.pma NEW_PROJECT.pma OLD_NAME NEW_NAME ids|parts|keep MEMBERS
  PrePoMax.Cli duplicate-element-set PROJECT.pma NEW_PROJECT.pma NAME NEW_NAME
  PrePoMax.Cli remove-element-set PROJECT.pma NEW_PROJECT.pma NAME
  PrePoMax.Cli add-material PROJECT.pma NEW_PROJECT.pma NAME YOUNGS_MODULUS POISSONS_RATIO
  PrePoMax.Cli plasticity PROJECT.pma
  PrePoMax.Cli set-plasticity PROJECT.pma NEW_PROJECT.pma MATERIAL isotropic|kinematic|combined STRESS:STRAIN[,STRESS:STRAIN...]
  PrePoMax.Cli remove-plasticity PROJECT.pma NEW_PROJECT.pma MATERIAL
  PrePoMax.Cli add-section PROJECT.pma NEW_PROJECT.pma NAME MATERIAL part|set REGION
  PrePoMax.Cli edit-section PROJECT.pma NEW_PROJECT.pma OLD_NAME NEW_NAME MATERIAL part|set REGION
  PrePoMax.Cli duplicate-section PROJECT.pma NEW_PROJECT.pma NAME NEW_NAME
  PrePoMax.Cli section-active PROJECT.pma NEW_PROJECT.pma NAME true|false
  PrePoMax.Cli remove-section PROJECT.pma NEW_PROJECT.pma NAME
  PrePoMax.Cli steps PROJECT.pma
  PrePoMax.Cli add-step PROJECT.pma NEW_PROJECT.pma NAME [STEP_OPTIONS]
  PrePoMax.Cli edit-step PROJECT.pma NEW_PROJECT.pma OLD_NAME NEW_NAME [STEP_OPTIONS]
  PrePoMax.Cli duplicate-step PROJECT.pma NEW_PROJECT.pma NAME NEW_NAME
  PrePoMax.Cli step-active PROJECT.pma NEW_PROJECT.pma NAME true|false
  PrePoMax.Cli remove-step PROJECT.pma NEW_PROJECT.pma NAME
  PrePoMax.Cli field-outputs PROJECT.pma
  PrePoMax.Cli add-field-output PROJECT.pma NEW_PROJECT.pma STEP NAME nodal|element|contact VARIABLES [FIELD_OPTIONS]
  PrePoMax.Cli edit-field-output PROJECT.pma NEW_PROJECT.pma STEP OLD_NAME NEW_NAME nodal|element|contact VARIABLES [FIELD_OPTIONS]
  PrePoMax.Cli duplicate-field-output PROJECT.pma NEW_PROJECT.pma STEP NAME NEW_NAME
  PrePoMax.Cli field-output-active PROJECT.pma NEW_PROJECT.pma STEP NAME true|false
  PrePoMax.Cli propagate-field-output PROJECT.pma NEW_PROJECT.pma STEP NAME
  PrePoMax.Cli remove-field-output PROJECT.pma NEW_PROJECT.pma STEP NAME
  PrePoMax.Cli history-outputs PROJECT.pma
  PrePoMax.Cli add-history-output PROJECT.pma NEW_PROJECT.pma STEP NAME nodal|element|contact|section VARIABLES REGION_TYPE REGION [HISTORY_OPTIONS]
  PrePoMax.Cli edit-history-output PROJECT.pma NEW_PROJECT.pma STEP OLD_NAME NEW_NAME nodal|element|contact|section VARIABLES REGION_TYPE REGION [HISTORY_OPTIONS]
  PrePoMax.Cli duplicate-history-output PROJECT.pma NEW_PROJECT.pma STEP NAME NEW_NAME
  PrePoMax.Cli history-output-active PROJECT.pma NEW_PROJECT.pma STEP NAME true|false
  PrePoMax.Cli propagate-history-output PROJECT.pma NEW_PROJECT.pma STEP NAME
  PrePoMax.Cli remove-history-output PROJECT.pma NEW_PROJECT.pma STEP NAME
  PrePoMax.Cli history-results PROJECT.pma
  PrePoMax.Cli history-series PROJECT.pma SET FIELD COMPONENT
  PrePoMax.Cli export-history PROJECT.pma NEW_RESULTS.csv [--delimiter comma|semicolon|tab]
  PrePoMax.Cli cad-topology PROJECT.pma
  PrePoMax.Cli export PROJECT.pma NEW_INPUT.inp
  PrePoMax.Cli extract-geometry PROJECT.pma OUTPUT.step
  PrePoMax.Cli scene PROJECT.pma NEW_SCENE.bin
  PrePoMax.Cli solve PROJECT.pma JOB_ROOT [CCX_EXECUTABLE]

Solve retains input, logs and result.pma in a unique directory under JOB_ROOT.
CalculiX must produce nonempty FRD; DAT is required when active history requests exist. Ctrl+C cancels the solver.
GMSH_OPTIONS: --min SIZE --algorithm auto|delaunay|frontal|hxt --optimize --second-order --gmsh EXECUTABLE
Local sizing: --size-box SIZE XMIN YMIN ZMIN XMAX YMAX ZMAX
Retained CAD face remeshing: --size-face TAG SIZE TRANSITION_DISTANCE
STEP_OPTIONS: --mode default|automatic|direct --nlgeom true|false --period TIME
              --initial TIME --min-increment TIME --max-increment TIME --max-increments COUNT
              --copy-previous true|false (add-step only; default true)
VARIABLES: comma-separated original CalculiX field variables, for example U,RF or S,E,ENER
FIELD_OPTIONS: --last-iterations true|false --contact-elements true|false --global true|false
               --element-output default|2d|3d
HISTORY REGION_TYPE: node-set|element-set|surface|reference-point|contact-pair
HISTORY_OPTIONS: --totals yes|only|no --global true|false
ELEMENT MEMBERS: ids "10,20,30:100:10"; parts "part1,part2"; keep - retains existing membership.
Omit MAX_ELEMENT_SIZE when remeshing to reuse the Gmsh setup stored in the project.
""");

static PlasticHardening ParsePlasticHardening(string value) => value.ToLowerInvariant() switch
{
    "isotropic" => PlasticHardening.Isotropic,
    "kinematic" => PlasticHardening.Kinematic,
    "combined" => PlasticHardening.Combined,
    _ => throw new ArgumentException("Plastic hardening must be isotropic, kinematic or combined.")
};

static PlasticPoint[] ParsePlasticPoints(string value)
{
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Enter plasticity points as STRESS:STRAIN pairs.");
    return value.Split(',', StringSplitOptions.TrimEntries).Select(item =>
    {
        string[] pair = item.Split(':', StringSplitOptions.TrimEntries);
        if (pair.Length != 2) throw new ArgumentException("Enter plasticity points as STRESS:STRAIN pairs.");
        return new PlasticPoint(double.Parse(pair[0], System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(pair[1], System.Globalization.CultureInfo.InvariantCulture));
    }).ToArray();
}

static (ProjectGmshOptions Options, string? Executable) ParseGmshOptions(string[] values, int start, double maximum)
{
    double? minimum = null;
    ProjectGmshAlgorithm algorithm = ProjectGmshAlgorithm.Automatic;
    bool optimize = false, secondOrder = false;
    string? executable = null;
    ProjectGmshSizeBox? sizeBox = null;
    ProjectGmshFaceSize? faceSize = null;
    var seen = new HashSet<string>(StringComparer.Ordinal);
    for (int index = start; index < values.Length; index++)
    {
        string value = values[index];
        if (!value.StartsWith("--", StringComparison.Ordinal))
        {
            if (executable != null) throw new ArgumentException("Only one Gmsh executable may be specified.");
            executable = value;
            continue;
        }
        if (!seen.Add(value)) throw new ArgumentException("Repeated Gmsh option: " + value);
        if (value == "--optimize") optimize = true;
        else if (value == "--second-order") secondOrder = true;
        else if (value == "--size-face")
        {
            if (values.Length - index - 1 < 3) throw new ArgumentException("--size-face requires a face tag, size and transition distance.");
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            faceSize = new(int.Parse(values[++index], culture), double.Parse(values[++index], culture), double.Parse(values[++index], culture));
        }
        else if (value == "--size-box")
        {
            if (values.Length - index - 1 < 7) throw new ArgumentException("--size-box requires size and six bounds.");
            double[] box = values.Skip(index + 1).Take(7)
                .Select(number => double.Parse(number, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            sizeBox = new(box[0], box[1], box[2], box[3], box[4], box[5], box[6]);
            index += 7;
        }
        else if (value is "--min" or "--gmsh")
        {
            if (++index >= values.Length) throw new ArgumentException(value + " requires a value.");
            if (value == "--gmsh")
            {
                if (executable != null) throw new ArgumentException("Only one Gmsh executable may be specified.");
                executable = values[index];
            }
            else minimum = double.Parse(values[index], System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (value == "--algorithm")
        {
            if (++index >= values.Length) throw new ArgumentException("--algorithm requires a value.");
            algorithm = values[index].ToLowerInvariant() switch
            {
                "auto" => ProjectGmshAlgorithm.Automatic,
                "delaunay" => ProjectGmshAlgorithm.Delaunay,
                "frontal" => ProjectGmshAlgorithm.Frontal,
                "hxt" => ProjectGmshAlgorithm.Hxt,
                _ => throw new ArgumentException("Algorithm must be auto, delaunay, frontal or hxt.")
            };
        }
        else throw new ArgumentException("Unknown Gmsh option: " + value);
    }
    return (new ProjectGmshOptions(maximum, minimum, algorithm, optimize, secondOrder, sizeBox, faceSize), executable);
}

static (StaticStepSettings Settings, bool CopyPrevious) ParseStepOptions(string[] values, int start,
    StaticStepSettings settings, bool adding)
{
    var culture = System.Globalization.CultureInfo.InvariantCulture;
    var seen = new HashSet<string>(StringComparer.Ordinal);
    bool copyPrevious = true;
    for (int index = start; index < values.Length; index += 2)
    {
        string option = values[index];
        if (!seen.Add(option) || index + 1 >= values.Length) throw new ArgumentException("Repeated or incomplete step option: " + option);
        string value = values[index + 1];
        if (option == "--copy-previous" && adding) copyPrevious = bool.Parse(value);
        else settings = option switch
        {
            "--mode" => settings with { IncrementationType = value.ToLowerInvariant() switch
            {
                "default" => IncrementationTypeEnum.Default, "automatic" => IncrementationTypeEnum.Automatic,
                "direct" => IncrementationTypeEnum.Direct, _ => throw new ArgumentException("Select default, automatic or direct incrementation.")
            } },
            "--nlgeom" => settings with { Nlgeom = bool.Parse(value) },
            "--period" => settings with { TimePeriod = double.Parse(value, culture) },
            "--initial" => settings with { InitialTimeIncrement = double.Parse(value, culture) },
            "--min-increment" => settings with { MinTimeIncrement = double.Parse(value, culture) },
            "--max-increment" => settings with { MaxTimeIncrement = double.Parse(value, culture) },
            "--max-increments" => settings with { MaxIncrements = int.Parse(value, culture) },
            _ => throw new ArgumentException("Unknown step option: " + option)
        };
    }
    return (settings, copyPrevious);
}

static RegionTypeEnum ParseSectionRegion(string value) => value.ToLowerInvariant() switch
{
    "part" or "partname" => RegionTypeEnum.PartName,
    "set" or "elementsetname" => RegionTypeEnum.ElementSetName,
    _ => throw new ArgumentException("Solid sections require a named part or element set.")
};

static FieldOutputSettings ParseFieldOutput(string[] values, int nameIndex, int kindIndex, int variablesIndex, int optionsStart)
{
    FieldOutputKind kind = values[kindIndex].ToLowerInvariant() switch
    {
        "nodal" or "node" => FieldOutputKind.Nodal,
        "element" => FieldOutputKind.Element,
        "contact" => FieldOutputKind.Contact,
        _ => throw new ArgumentException("Field-output type must be nodal, element or contact.")
    };
    var settings = new FieldOutputSettings(values[nameIndex], kind,
        values[variablesIndex].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    var seen = new HashSet<string>(StringComparer.Ordinal);
    for (int index = optionsStart; index < values.Length; index += 2)
    {
        string option = values[index];
        if (!seen.Add(option) || index + 1 >= values.Length)
            throw new ArgumentException("Repeated or incomplete field-output option: " + option);
        string value = values[index + 1];
        settings = option switch
        {
            "--last-iterations" => settings with { LastIterations = bool.Parse(value) },
            "--contact-elements" => settings with { ContactElements = bool.Parse(value) },
            "--global" => settings with { Global = bool.Parse(value) },
            "--element-output" => settings with { ElementOutput = value.ToLowerInvariant() switch
            {
                "default" => ElementFieldOutputOutputEnum.Default,
                "2d" => ElementFieldOutputOutputEnum.TwoD,
                "3d" => ElementFieldOutputOutputEnum.ThreeD,
                _ => throw new ArgumentException("Element output mode must be default, 2d or 3d.")
            } },
            _ => throw new ArgumentException("Unknown field-output option: " + option)
        };
    }
    return settings;
}

static HistoryOutputSettings ParseHistoryOutput(string[] values, int nameIndex, int kindIndex, int variablesIndex,
    int regionTypeIndex, int regionIndex, int optionsStart)
{
    HistoryOutputKind kind = values[kindIndex].ToLowerInvariant() switch
    {
        "nodal" or "node" => HistoryOutputKind.Nodal,
        "element" => HistoryOutputKind.Element,
        "contact" => HistoryOutputKind.Contact,
        "section" => HistoryOutputKind.Section,
        _ => throw new ArgumentException("History-output type must be nodal, element, contact or section.")
    };
    RegionTypeEnum regionType = values[regionTypeIndex].ToLowerInvariant() switch
    {
        "node-set" or "nodeset" => RegionTypeEnum.NodeSetName,
        "element-set" or "elementset" => RegionTypeEnum.ElementSetName,
        "surface" => RegionTypeEnum.SurfaceName,
        "reference-point" or "referencepoint" => RegionTypeEnum.ReferencePointName,
        "contact-pair" or "contactpair" => RegionTypeEnum.ContactPair,
        _ => throw new ArgumentException("Unsupported history-output region type.")
    };
    var settings = new HistoryOutputSettings(values[nameIndex], kind,
        values[variablesIndex].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        regionType, values[regionIndex]);
    var seen = new HashSet<string>(StringComparer.Ordinal);
    for (int index = optionsStart; index < values.Length; index += 2)
    {
        string option = values[index];
        if (!seen.Add(option) || index + 1 >= values.Length)
            throw new ArgumentException("Repeated or incomplete history-output option: " + option);
        string value = values[index + 1];
        settings = option switch
        {
            "--global" => settings with { Global = bool.Parse(value) },
            "--totals" => settings with { Totals = value.ToLowerInvariant() switch
            {
                "yes" => TotalsTypeEnum.Yes, "only" => TotalsTypeEnum.Only, "no" => TotalsTypeEnum.No,
                _ => throw new ArgumentException("Totals mode must be yes, only or no.")
            } },
            _ => throw new ArgumentException("Unknown history-output option: " + option)
        };
    }
    return settings;
}
