using System.Text;
using System.Text.Json;
using CaeModel;
using PrePoMax.Application;

// This probe runs before project loading, private IPC or renderer startup.
if (args.Length == 1 && args[0] == "--check-runtime")
{
    Console.WriteLine($"Viewer companion runtime ready: .NET {Environment.Version}");
    return 0;
}

// Private companion protocol. stdout carries credentials, never diagnostics.
if (args.Length != 3 || !Console.IsInputRedirected || !Console.IsOutputRedirected)
{
    Console.Error.WriteLine("Launch this companion through scripts/start-linux-viewer.mjs.");
    return 2;
}
try
{
    await using var host = await RendererHost.StartAsync(args[1], args[2]);
    await using var jobs = new ViewerJobs();
    await using var meshing = new ViewerMeshingJobs();
    bool NativeBusy() => jobs.Active || meshing.Active;
    var meshSelection = new ProjectMeshSelectionSession();
    string? currentProject = args[0] == "-" ? null : Path.GetFullPath(args[0]);
    ProjectSummary? projectSummary = currentProject == null ? null : ProjectService.Inspect(currentProject);
    ProjectGeometrySummary? geometry = projectSummary?.GeometrySource;
    ProjectGmshSetupSummary? meshingSetup = projectSummary?.MeshingSetup;
    ProjectCadTopologySummary? cadTopology = projectSummary?.CadTopology;
    RendererSceneSummary? scene = currentProject == null ? null : await host.PublishAsync(currentProject);
    Console.WriteLine(JsonSerializer.Serialize(new { address = host.BrowserAddress.AbsoluteUri, scene, geometry, meshingSetup, cadTopology, canSolve = projectSummary?.CanSolve == true }));
    var line = new StringBuilder();
    char[] buffer = new char[1];
    while (await Console.In.ReadAsync(buffer) != 0)
    {
        if (buffer[0] != '\n')
        {
            if (line.Length >= 4 * 1024 * 1024) throw new InvalidDataException("Viewer message exceeds limit.");
            line.Append(buffer[0]);
            continue;
        }
        using var request = JsonDocument.Parse(line.ToString());
        line.Clear();
        var commandName = request.RootElement.TryGetProperty("command", out var command) ? command.GetString() : null;
        if (currentProject == null && commandName is not ("open" or "import-mesh" or "job-status" or "cancel" or
            "mesh-geometry" or "mesh-job-status" or "cancel-mesh"))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Open or import a project first." }));
            continue;
        }
        if (commandName is "solve" or "job-status" or "cancel")
        {
            bool ok = true;
            if (commandName == "solve")
            {
                if (meshing.Active) ok = false;
                else
                {
                    host.ClearResultSession();
                    string root = request.RootElement.GetProperty("jobRoot").GetString()!;
                    string? solver = request.RootElement.TryGetProperty("solver", out var configured) ? configured.GetString() : null;
                    ok = jobs.Start(currentProject!, root, solver);
                }
            }
            else if (commandName == "cancel") jobs.Cancel();
            Console.WriteLine(JsonSerializer.Serialize(new { ok, job = jobs.Snapshot() }));
            continue;
        }
        if (commandName is "mesh-geometry" or "remesh-geometry" or "mesh-job-status" or "cancel-mesh")
        {
            bool ok = true;
            try
            {
                if (commandName is "mesh-geometry" or "remesh-geometry")
                {
                    if (jobs.Active) throw new InvalidOperationException("An analysis is running.");
                    string? gmsh = request.RootElement.TryGetProperty("gmsh", out var configured) ? configured.GetString() : null;
                    var options = ReadGmshOptions(request.RootElement);
                    ok = commandName == "remesh-geometry"
                        ? meshing.StartRemesh(currentProject!, request.RootElement.GetProperty("path").GetString()!, options, gmsh)
                        : meshing.Start(request.RootElement.GetProperty("source").GetString()!,
                            request.RootElement.GetProperty("path").GetString()!,
                            request.RootElement.GetProperty("youngsModulus").GetDouble(),
                            request.RootElement.GetProperty("poissonsRatio").GetDouble(), options, gmsh);
                }
                else if (commandName == "cancel-mesh") meshing.Cancel();
                Console.WriteLine(JsonSerializer.Serialize(new { ok, job = meshing.Snapshot() }));
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, job = meshing.Snapshot(),
                    error = "Unable to start meshing: " + ex.Message }));
            }
            continue;
        }
        if (commandName is "model-tree" or "tree-selection" or "tree-highlight")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "model-tree")
                {
                    string tree = JsonSerializer.Serialize(new { ok = true, nodes = ProjectModelTree.Read(currentProject!) });
                    if (tree.Length >= 1024 * 1024)
                        throw new NotSupportedException("This model exceeds the current tree inventory limit.");
                    Console.WriteLine(tree);
                }
                else if (commandName == "tree-highlight")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, nodes = ProjectModelTree.NodeSetHighlight(
                        meshSelection.GetMesh(currentProject!), request.RootElement.GetProperty("key").GetString()!) }));
                else
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, selection = ProjectModelTree.Select(currentProject!,
                        request.RootElement.GetProperty("key").GetString()!) }));
            }
            catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
            continue;
        }
        if (commandName == "cad-sizing-faces")
        {
            try { Console.WriteLine(JsonSerializer.Serialize(new { ok = true, faces = ProjectService.CadSizingFaces(currentProject!) })); }
            catch (Exception ex) when (ex is NotSupportedException or InvalidDataException or IOException)
            { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
            continue;
        }
        if (commandName == "cad-topology")
        {
            try { Console.WriteLine(JsonSerializer.Serialize(new { ok = true, topology = ProjectService.CadTopology(currentProject!) })); }
            catch (Exception ex) when (ex is NotSupportedException or InvalidDataException or IOException)
            { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
            continue;
        }
        if (commandName is "fields" or "field")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "fields")
                {
                    var fields = host.ListResultFields(currentProject!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, fields, resultAccess = host.ResultAccess }));
                }
                else
                {
                    var field = request.RootElement.GetProperty("field").Deserialize<ResultField>();
                    double scale = request.RootElement.TryGetProperty("deformationScale", out var scaleValue) ? scaleValue.GetDouble() : 0;
                    scene = await host.PublishAsync(currentProject!, field: field, deformationScale: scale);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, scene, resultAccess = host.ResultAccess, publication = host.LastPublication }));
                }
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to display the selected results." })); }
            continue;
        }
        if (commandName is "history-result-data" or "history-result-series" or "export-history-results")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "history-result-data")
                {
                    var catalog = ProjectHistoryResults.List(currentProject!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, series = catalog.Series, diagnostics = catalog.Diagnostics }));
                }
                else if (commandName == "history-result-series")
                {
                    var table = ProjectHistoryResults.Read(currentProject!,
                        request.RootElement.GetProperty("set").GetString()!,
                        request.RootElement.GetProperty("field").GetString()!,
                        request.RootElement.GetProperty("component").GetString()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, table }));
                }
                else
                {
                    string delimiter = request.RootElement.TryGetProperty("delimiter", out var delimiterValue)
                        ? delimiterValue.GetString()! : ",";
                    string[]? sets = request.RootElement.TryGetProperty("sets", out var setValues)
                        ? setValues.Deserialize<string[]>() : null;
                    ProjectHistoryResults.Export(currentProject!, request.RootElement.GetProperty("path").GetString()!, sets, delimiter);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to read or export history results: " + ex.Message }));
            }
            continue;
        }
        if (commandName == "extract-geometry")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                ProjectService.ExportGeometry(currentProject!, request.RootElement.GetProperty("path").GetString()!);
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true, geometry }));
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to export retained CAD geometry. Choose a new destination." })); }
            continue;
        }
        if (commandName is "field-output-data" or "add-field-output" or "edit-field-output" or
            "duplicate-field-output" or "field-output-active" or "propagate-field-output" or "remove-field-output")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "field-output-data")
                {
                    var data = ProjectFieldOutputs.Read(currentProject!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, steps = data.Steps, outputs = data.Outputs,
                        variableChoices = data.VariableChoices, editingError = data.EditingError }));
                }
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    string step = request.RootElement.GetProperty("step").GetString()!;
                    if (commandName == "add-field-output")
                        ProjectFieldOutputs.AddCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("settings").Deserialize<FieldOutputSettings>()!);
                    else if (commandName == "edit-field-output")
                        ProjectFieldOutputs.SaveCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("output").GetString()!,
                            request.RootElement.GetProperty("settings").Deserialize<FieldOutputSettings>()!);
                    else if (commandName == "duplicate-field-output")
                        ProjectFieldOutputs.DuplicateCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("output").GetString()!, request.RootElement.GetProperty("name").GetString()!);
                    else if (commandName == "field-output-active")
                        ProjectFieldOutputs.SetActiveCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("output").GetString()!, request.RootElement.GetProperty("active").GetBoolean());
                    else if (commandName == "propagate-field-output")
                        ProjectFieldOutputs.PropagateCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("output").GetString()!);
                    else ProjectFieldOutputs.RemoveCopy(currentProject!, path, step,
                        request.RootElement.GetProperty("output").GetString()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save field output: " + ex.Message }));
            }
            continue;
        }
        if (commandName is "history-output-data" or "add-history-output" or "edit-history-output" or
            "duplicate-history-output" or "history-output-active" or "propagate-history-output" or "remove-history-output")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "history-output-data")
                {
                    var data = ProjectHistoryOutputs.Read(currentProject!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, steps = data.Steps, outputs = data.Outputs,
                        regions = data.Regions, variableChoices = data.VariableChoices, editingError = data.EditingError }));
                }
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    string step = request.RootElement.GetProperty("step").GetString()!;
                    if (commandName == "add-history-output")
                        ProjectHistoryOutputs.AddCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("settings").Deserialize<HistoryOutputSettings>()!);
                    else if (commandName == "edit-history-output")
                        ProjectHistoryOutputs.SaveCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("output").GetString()!,
                            request.RootElement.GetProperty("settings").Deserialize<HistoryOutputSettings>()!);
                    else if (commandName == "duplicate-history-output")
                        ProjectHistoryOutputs.DuplicateCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("output").GetString()!, request.RootElement.GetProperty("name").GetString()!);
                    else if (commandName == "history-output-active")
                        ProjectHistoryOutputs.SetActiveCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("output").GetString()!, request.RootElement.GetProperty("active").GetBoolean());
                    else if (commandName == "propagate-history-output")
                        ProjectHistoryOutputs.PropagateCopy(currentProject!, path, step,
                            request.RootElement.GetProperty("output").GetString()!);
                    else ProjectHistoryOutputs.RemoveCopy(currentProject!, path, step,
                        request.RootElement.GetProperty("output").GetString()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save history output: " + ex.Message }));
            }
            continue;
        }
        if (commandName is "section-data" or "add-section" or "edit-section" or "duplicate-section" or "section-active" or "remove-section")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "section-data")
                {
                    var data = ProjectSections.Read(currentProject!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, sections = data.Sections, regions = data.Regions,
                        materials = data.Materials, coverage = data.Coverage, editingError = data.EditingError }));
                }
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    if (commandName == "add-section")
                        ProjectSections.AddCopy(currentProject!, path, request.RootElement.GetProperty("settings").Deserialize<SolidSectionSettings>()!);
                    else if (commandName == "edit-section")
                        ProjectSections.SaveCopy(currentProject!, path, request.RootElement.GetProperty("section").GetString()!,
                            request.RootElement.GetProperty("settings").Deserialize<SolidSectionSettings>()!);
                    else if (commandName == "duplicate-section")
                        ProjectSections.DuplicateCopy(currentProject!, path, request.RootElement.GetProperty("section").GetString()!,
                            request.RootElement.GetProperty("name").GetString()!);
                    else if (commandName == "section-active")
                        ProjectSections.SetActiveCopy(currentProject!, path, request.RootElement.GetProperty("section").GetString()!,
                            request.RootElement.GetProperty("active").GetBoolean());
                    else ProjectSections.RemoveCopy(currentProject!, path, request.RootElement.GetProperty("section").GetString()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save section: " + ex.Message })); }
            continue;
        }
        if (commandName is "step-data" or "add-step" or "edit-step" or "duplicate-step" or "step-active" or "remove-step")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "step-data")
                {
                    var data = ProjectSteps.Read(currentProject!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, steps = data.Steps, editingError = data.EditingError }));
                }
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    if (commandName == "add-step")
                        ProjectSteps.AddCopy(currentProject!, path, request.RootElement.GetProperty("settings").Deserialize<StaticStepSettings>()!,
                            request.RootElement.GetProperty("copyPrevious").GetBoolean());
                    else if (commandName == "edit-step")
                        ProjectSteps.SaveCopy(currentProject!, path, request.RootElement.GetProperty("step").GetString()!,
                            request.RootElement.GetProperty("settings").Deserialize<StaticStepSettings>()!);
                    else if (commandName == "duplicate-step")
                        ProjectSteps.DuplicateCopy(currentProject!, path, request.RootElement.GetProperty("step").GetString()!,
                            request.RootElement.GetProperty("name").GetString()!);
                    else if (commandName == "step-active")
                        ProjectSteps.SetActiveCopy(currentProject!, path, request.RootElement.GetProperty("step").GetString()!,
                            request.RootElement.GetProperty("active").GetBoolean());
                    else ProjectSteps.RemoveCopy(currentProject!, path, request.RootElement.GetProperty("step").GetString()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save step: " + ex.Message })); }
            continue;
        }
        if (commandName is "materials" or "material-data" or "edit-material" or "add-material" or "assign-material" or "remove-material")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "materials")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, materials = ProjectMaterials.List(currentProject!) }));
                else if (commandName == "material-data")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, materials = ProjectMaterials.List(currentProject!), sections = ProjectMaterials.ListSolidSections(currentProject!) }));
                else if (commandName == "add-material")
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    string name = request.RootElement.GetProperty("name").GetString()!;
                    double modulus = request.RootElement.GetProperty("youngsModulus").GetDouble();
                    double ratio = request.RootElement.GetProperty("poissonsRatio").GetDouble();
                    if (request.RootElement.TryGetProperty("section", out var section) && section.ValueKind != JsonValueKind.Null)
                        ProjectMaterials.AddElasticAndAssignCopy(currentProject!, path, section.GetString()!, name, modulus, ratio);
                    else ProjectMaterials.AddElasticCopy(currentProject!, path, name, modulus, ratio);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
                else if (commandName == "assign-material")
                {
                    ProjectMaterials.AssignSolidSectionCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("section").GetString()!,
                        request.RootElement.GetProperty("name").GetString()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
                else if (commandName == "remove-material")
                {
                    ProjectMaterials.RemoveUnusedCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("name").GetString()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    string name = request.RootElement.GetProperty("name").GetString()!;
                    double youngsModulus = request.RootElement.GetProperty("youngsModulus").GetDouble();
                    double poissonsRatio = request.RootElement.GetProperty("poissonsRatio").GetDouble();
                    if (request.RootElement.TryGetProperty("density", out var density))
                        ProjectMaterials.SaveElasticDensityCopy(currentProject!, path, name, youngsModulus, poissonsRatio,
                            density.ValueKind == JsonValueKind.Null ? null : density.GetDouble());
                    else ProjectMaterials.SaveElasticCopy(currentProject!, path, name, youngsModulus, poissonsRatio);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save material. Check the name, section, values and destination." })); }
            continue;
        }
        if (commandName is "plasticity-data" or "set-plasticity" or "remove-plasticity")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "plasticity-data")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, materials = ProjectMaterials.ListPlasticity(currentProject!) }));
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    string material = request.RootElement.GetProperty("material").GetString()!;
                    if (commandName == "set-plasticity")
                        ProjectMaterials.SavePlasticityCopy(currentProject!, path, material,
                            request.RootElement.GetProperty("hardening").Deserialize<PlasticHardening>(),
                            request.RootElement.GetProperty("points").Deserialize<PlasticPoint[]>()!);
                    else ProjectMaterials.RemovePlasticityCopy(currentProject!, path, material);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save plasticity: " + ex.Message }));
            }
            continue;
        }
        if (commandName is "gravity-data" or "add-gravity" or "edit-gravity" or "remove-gravity")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "gravity-data")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, loads = ProjectGravity.List(currentProject!), targets = ProjectGravity.CreationTargets(currentProject!) }));
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    string step = request.RootElement.GetProperty("step").GetString()!;
                    string name = request.RootElement.GetProperty("name").GetString()!;
                    if (commandName == "remove-gravity") ProjectGravity.RemoveCopy(currentProject!, path, step, name);
                    else if (commandName == "edit-gravity")
                        ProjectGravity.SaveCopy(currentProject!, path, step, name,
                            request.RootElement.GetProperty("a1").GetDouble(), request.RootElement.GetProperty("a2").GetDouble(),
                            request.RootElement.GetProperty("a3").GetDouble());
                    else ProjectGravity.AddCopy(currentProject!, path, step, name,
                        request.RootElement.GetProperty("region").GetString()!, request.RootElement.GetProperty("a1").GetDouble(),
                        request.RootElement.GetProperty("a2").GetDouble(), request.RootElement.GetProperty("a3").GetDouble());
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save gravity. Check density, region, acceleration and destination." })); }
            continue;
        }
        if (commandName is "centrifugal-data" or "add-centrifugal" or "edit-centrifugal" or "remove-centrifugal")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "centrifugal-data")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, loads = ProjectCentrifugal.List(currentProject!), targets = ProjectCentrifugal.CreationTargets(currentProject!) }));
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    string step = request.RootElement.GetProperty("step").GetString()!;
                    string name = request.RootElement.GetProperty("name").GetString()!;
                    if (commandName == "remove-centrifugal") ProjectCentrifugal.RemoveCopy(currentProject!, path, step, name);
                    else
                    {
                        double x = request.RootElement.GetProperty("x").GetDouble();
                        double y = request.RootElement.GetProperty("y").GetDouble();
                        double z = request.RootElement.GetProperty("z").GetDouble();
                        double n1 = request.RootElement.GetProperty("n1").GetDouble();
                        double n2 = request.RootElement.GetProperty("n2").GetDouble();
                        double n3 = request.RootElement.GetProperty("n3").GetDouble();
                        double speed = request.RootElement.GetProperty("rotationalSpeed").GetDouble();
                        if (commandName == "edit-centrifugal")
                            ProjectCentrifugal.SaveCopy(currentProject!, path, step, name, x, y, z, n1, n2, n3, speed);
                        else ProjectCentrifugal.AddCopy(currentProject!, path, step, name,
                            request.RootElement.GetProperty("region").GetString()!, x, y, z, n1, n2, n3, speed);
                    }
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save centrifugal load. Check density, region, axis, speed and destination." })); }
            continue;
        }
        if (commandName is "loads" or "edit-load" or "load-targets" or "add-load")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "load-targets")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, loads = ProjectLoads.CreationTargets(currentProject!) }));
                else if (commandName == "add-load")
                {
                    ProjectLoads.AddCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("step").GetString()!, request.RootElement.GetProperty("name").GetString()!,
                        request.RootElement.GetProperty("region").GetString()!,
                        request.RootElement.TryGetProperty("surfaceTraction", out var surfaceTraction) && surfaceTraction.GetBoolean()
                            ? ForceLoadCreationKind.SurfaceTraction : ForceLoadCreationKind.ConcentratedForce,
                        request.RootElement.GetProperty("f1").GetDouble(),
                        request.RootElement.GetProperty("f2").GetDouble(), request.RootElement.GetProperty("f3").GetDouble());
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
                else if (commandName == "loads")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, loads = ProjectLoads.List(currentProject!) }));
                else
                {
                    ProjectLoads.SaveForceCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("step").GetString()!, request.RootElement.GetProperty("name").GetString()!,
                        request.RootElement.GetProperty("f1").GetDouble(), request.RootElement.GetProperty("f2").GetDouble(),
                        request.RootElement.GetProperty("f3").GetDouble());
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save force edit. Check values and choose a new file." })); }
            continue;
        }
        if (commandName is "pressures" or "pressure-targets" or "pressure-data" or "add-pressure" or "edit-pressure" or "remove-pressure")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "pressure-data")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, pressures = ProjectPressures.List(currentProject!), targets = ProjectPressures.CreationTargets(currentProject!) }));
                else if (commandName == "pressures")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, pressures = ProjectPressures.List(currentProject!) }));
                else if (commandName == "pressure-targets")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, pressures = ProjectPressures.CreationTargets(currentProject!) }));
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    string step = request.RootElement.GetProperty("step").GetString()!;
                    string name = request.RootElement.GetProperty("name").GetString()!;
                    if (commandName == "add-pressure")
                        ProjectPressures.AddCopy(currentProject!, path, step, name,
                            request.RootElement.GetProperty("region").GetString()!, request.RootElement.GetProperty("magnitude").GetDouble());
                    else if (commandName == "edit-pressure")
                        ProjectPressures.SaveMagnitudeCopy(currentProject!, path, step, name,
                            request.RootElement.GetProperty("magnitude").GetDouble());
                    else ProjectPressures.RemoveCopy(currentProject!, path, step, name);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save pressure. Check the surface, value and destination." })); }
            continue;
        }
        if (commandName is "boundaries" or "edit-boundary" or "boundary-directions" or "edit-boundary-direction" or "boundary-targets" or "add-boundary" or
            "fixed-support-data" or "add-fixed-support")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "fixed-support-data")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, supports = ProjectBoundaries.ListFixed(currentProject!),
                        targets = ProjectBoundaries.FixedCreationTargets(currentProject!) }));
                else if (commandName == "add-fixed-support")
                {
                    ProjectBoundaries.AddFixedCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("step").GetString()!, request.RootElement.GetProperty("name").GetString()!,
                        request.RootElement.GetProperty("region").GetString()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
                else if (commandName == "boundary-targets")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, boundaries = ProjectBoundaries.CreationTargets(currentProject!) }));
                else if (commandName == "add-boundary")
                {
                    ProjectBoundaries.AddCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("step").GetString()!, request.RootElement.GetProperty("name").GetString()!,
                        request.RootElement.GetProperty("region").GetString()!, request.RootElement.GetProperty("direction").GetInt32(),
                        request.RootElement.GetProperty("value").GetDouble());
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
                else if (commandName == "boundary-directions")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, boundaries = ProjectBoundaries.ListDirections(currentProject!) }));
                else if (commandName == "edit-boundary-direction")
                {
                    var value = request.RootElement.GetProperty("value");
                    ProjectBoundaries.SaveDirectionCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("step").GetString()!, request.RootElement.GetProperty("name").GetString()!,
                        request.RootElement.GetProperty("direction").GetInt32(), value.ValueKind == JsonValueKind.Null ? null : value.GetDouble());
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
                else if (commandName == "boundaries")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, boundaries = ProjectBoundaries.List(currentProject!) }));
                else
                {
                    ProjectBoundaries.SaveDisplacementCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("step").GetString()!, request.RootElement.GetProperty("name").GetString()!,
                        request.RootElement.GetProperty("direction").GetInt32(), request.RootElement.GetProperty("value").GetDouble());
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to save displacement edit. Check values and choose a new file." })); }
            continue;
        }
        if (commandName == "node-selection-end")
        {
            meshSelection.Clear();
            Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
            continue;
        }
        if (commandName is "node-selection-initial" or "node-selection-all" or "node-selection-ids" or "node-selection-pick" or "node-selection-area" or "node-selection-frustum")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                var root = request.RootElement;
                var selectionMesh = meshSelection.GetMesh(currentProject!, restart: commandName == "node-selection-initial");
                int[] nodes = commandName switch
                {
                    "node-selection-initial" => ProjectMeshNodeSelection.Initial(selectionMesh, root.TryGetProperty("region", out var region) ? region.GetString() : null),
                    "node-selection-all" => ProjectMeshNodeSelection.Universe(selectionMesh),
                    "node-selection-ids" => ProjectMeshNodeSelection.Ids(selectionMesh, root.GetProperty("ids").GetString()!),
                    "node-selection-pick" => ProjectMeshNodeSelection.Pick(selectionMesh, root.GetProperty("pick").Deserialize<MeshNodePick>()!),
                    "node-selection-frustum" => ProjectMeshNodeFrustum.Select(selectionMesh, root.GetProperty("frustum").Deserialize<MeshNodeFrustum>()!),
                    _ => ProjectMeshNodeSelection.Area(selectionMesh, root.GetProperty("area").Deserialize<MeshNodeArea>()!)
                };
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true, nodes }));
            }
            catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
            continue;
        }
        if (commandName is "node-set-data" or "surface-editor-data" or "preview-node-set" or "preview-surface" or
            "create-node-set" or "edit-node-set" or "duplicate-node-set" or "remove-node-set" or
            "create-surface" or "edit-surface" or "duplicate-surface" or "remove-surface")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                string? name = request.RootElement.TryGetProperty("region", out var region) ? region.GetString() : null;
                if (commandName is "node-set-data" or "surface-editor-data")
                {
                    string data = commandName == "node-set-data"
                        ? JsonSerializer.Serialize(new { ok = true, data = ProjectNodeSets.Read(currentProject!, name) })
                        : JsonSerializer.Serialize(new { ok = true, data = ProjectSurfaces.Read(currentProject!, name) });
                    if (data.Length >= 1024 * 1024) throw new NotSupportedException("This model exceeds the region inventory response limit.");
                    Console.WriteLine(data);
                }
                else if (commandName == "preview-node-set")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, selection = new ModelTreeSelection(ProjectNodeSets.Preview(currentProject!,
                        request.RootElement.GetProperty("settings").Deserialize<NodeSetSettings>()!, name), []) }));
                else if (commandName == "preview-surface")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, selection = ProjectSurfaces.Preview(currentProject!,
                        request.RootElement.GetProperty("settings").Deserialize<SurfaceSettings>()!, name) }));
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    if (commandName == "create-node-set") ProjectNodeSets.AddCopy(currentProject!, path,
                        request.RootElement.GetProperty("settings").Deserialize<NodeSetSettings>()!);
                    else if (commandName == "edit-node-set") ProjectNodeSets.SaveCopy(currentProject!, path, name!,
                        request.RootElement.GetProperty("settings").Deserialize<NodeSetSettings>()!);
                    else if (commandName == "duplicate-node-set") ProjectNodeSets.DuplicateCopy(currentProject!, path, name!, request.RootElement.GetProperty("name").GetString()!);
                    else if (commandName == "remove-node-set") ProjectNodeSets.RemoveCopy(currentProject!, path, name!);
                    else if (commandName == "create-surface")
                    {
                        var settings = request.RootElement.GetProperty("settings").Deserialize<SurfaceSettings>()!;
                        ProjectSurfaces.AddCopy(currentProject!, path, settings.Name, settings.Faces!);
                    }
                    else if (commandName == "edit-surface") ProjectSurfaces.SaveCopy(currentProject!, path, name!,
                        request.RootElement.GetProperty("settings").Deserialize<SurfaceSettings>()!);
                    else if (commandName == "duplicate-surface") ProjectSurfaces.DuplicateCopy(currentProject!, path, name!, request.RootElement.GetProperty("name").GetString()!);
                    else ProjectSurfaces.RemoveCopy(currentProject!, path, name!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
            continue;
        }
        if (commandName is "element-set-data" or "preview-element-set" or "add-element-set" or
            "edit-element-set" or "duplicate-element-set" or "remove-element-set")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                string? name = request.RootElement.TryGetProperty("set", out var setName) ? setName.GetString() : null;
                if (commandName == "element-set-data")
                {
                    string data = JsonSerializer.Serialize(new { ok = true, data = ProjectElementSets.Read(currentProject!, name) });
                    if (data.Length >= 1024 * 1024) throw new NotSupportedException("This model exceeds the current element-set inventory limit.");
                    Console.WriteLine(data);
                }
                else if (commandName == "preview-element-set")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, selection = ProjectElementSets.Preview(currentProject!,
                        request.RootElement.GetProperty("settings").Deserialize<ElementSetSettings>()!, name) }));
                else
                {
                    string path = request.RootElement.GetProperty("path").GetString()!;
                    if (commandName == "add-element-set")
                        ProjectElementSets.AddCopy(currentProject!, path, request.RootElement.GetProperty("settings").Deserialize<ElementSetSettings>()!);
                    else if (commandName == "edit-element-set")
                        ProjectElementSets.SaveCopy(currentProject!, path, name!, request.RootElement.GetProperty("settings").Deserialize<ElementSetSettings>()!);
                    else if (commandName == "duplicate-element-set")
                        ProjectElementSets.DuplicateCopy(currentProject!, path, name!, request.RootElement.GetProperty("name").GetString()!);
                    else ProjectElementSets.RemoveCopy(currentProject!, path, name!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
            continue;
        }
        if (commandName is "node-sets" or "add-node-set" or "select-node-box")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "select-node-box")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, nodes = ProjectNodeSets.SelectBox(currentProject!,
                        request.RootElement.GetProperty("box").Deserialize<NodeSelectionBox>()!) }));
                else if (commandName == "node-sets")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, nodes = ProjectNodeSets.Nodes(currentProject!), sets = ProjectNodeSets.List(currentProject!), bounds = ProjectNodeSets.Bounds(currentProject!) }));
                else
                {
                    ProjectNodeSets.AddCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("name").GetString()!, request.RootElement.GetProperty("nodes").Deserialize<int[]>()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
            continue;
        }
        if (commandName is "surface-data" or "surface-face-nodes" or "resolve-picked-face" or "resolve-picked-faces" or "resolve-picked-face-angle" or "add-surface")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                if (commandName == "surface-data")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, surfaces = ProjectSurfaces.List(currentProject!), inventory = ProjectSurfaces.Inventory(currentProject!) }));
                else if (commandName == "surface-face-nodes")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, nodes = ProjectSurfaces.FaceNodes(currentProject!,
                        request.RootElement.GetProperty("faces").Deserialize<ProjectElementFace[]>()!) }));
                else if (commandName == "resolve-picked-face")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, face = ProjectSurfaces.ResolveFace(currentProject!,
                        request.RootElement.GetProperty("element").GetInt32(),
                        request.RootElement.GetProperty("nodes").Deserialize<int[]>()!) }));
                else if (commandName == "resolve-picked-faces")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, faces = ProjectSurfaces.ResolveFaces(currentProject!,
                        request.RootElement.GetProperty("triangles").Deserialize<ProjectPickedTriangle[]>()!) }));
                else if (commandName == "resolve-picked-face-angle")
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true, faces = ProjectSurfaces.GrowFace(currentProject!,
                        request.RootElement.GetProperty("element").GetInt32(),
                        request.RootElement.GetProperty("nodes").Deserialize<int[]>()!,
                        request.RootElement.GetProperty("angle").GetDouble()) }));
                else
                {
                    ProjectSurfaces.AddCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                        request.RootElement.GetProperty("name").GetString()!,
                        request.RootElement.GetProperty("faces").Deserialize<ProjectElementFace[]>()!);
                    Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
                }
            }
            catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
            continue;
        }
        if (commandName == "preview-nodes")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                scene = await host.PublishAsync(currentProject!,
                    selectedNodes: request.RootElement.GetProperty("nodes").Deserialize<int[]>()!,
                    selectedFaces: request.RootElement.TryGetProperty("faces", out var faces)
                        ? faces.Deserialize<ProjectElementFace[]>()! : null);
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true, scene }));
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to preview selected nodes." })); }
            continue;
        }
        if (commandName == "preview-faces")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                scene = await host.PublishAsync(currentProject!,
                    selectedFaces: request.RootElement.GetProperty("faces").Deserialize<ProjectElementFace[]>()!);
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true, scene }));
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to preview selected faces." })); }
            continue;
        }
        if (commandName is "remove-load" or "remove-boundary")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                ProjectDefinitions.RemoveCopy(currentProject!, request.RootElement.GetProperty("path").GetString()!,
                    request.RootElement.GetProperty("step").GetString()!, request.RootElement.GetProperty("name").GetString()!,
                    commandName == "remove-boundary");
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to remove the selected definition. Choose an existing definition and a new file." })); }
            continue;
        }
        if (commandName == "import-mesh")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                ProjectMeshImport.Import(request.RootElement.GetProperty("source").GetString()!,
                    request.RootElement.GetProperty("path").GetString()!,
                    request.RootElement.GetProperty("youngsModulus").GetDouble(), request.RootElement.GetProperty("poissonsRatio").GetDouble());
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true }));
            }
            catch { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Mesh import failed. Check the supported format, material values and new output path." })); }
            continue;
        }
        if (commandName == "open")
        {
            try
            {
                if (NativeBusy()) throw new InvalidOperationException("A native job is running.");
                string path = request.RootElement.GetProperty("path").GetString()!;
                // Validate before changing the current renderer/bootstrap state.
                var summary = ProjectService.Inspect(path);
                scene = await host.PublishAsync(path);
                currentProject = Path.GetFullPath(path);
                meshSelection.Clear();
                geometry = summary.GeometrySource;
                meshingSetup = summary.MeshingSetup;
                cadTopology = summary.CadTopology;
                Console.WriteLine(JsonSerializer.Serialize(new { ok = true, scene, geometry, meshingSetup, cadTopology, canSolve = summary.CanSolve }));
            }
            catch
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = "Unable to open the selected project." }));
            }
            continue;
        }
        string? response = null;
        host.TryBootstrapBrowser(new Uri(request.RootElement.GetProperty("source").GetString()!),
            request.RootElement.GetProperty("topLevel").GetBoolean(),
            request.RootElement.GetProperty("message").GetString()!, json => response = json);
        Console.WriteLine(JsonSerializer.Serialize(new { bootstrap = response }));
    }
    return 0;
}
catch
{
    // Do not echo malformed credential-bearing IPC or arbitrary renderer output.
    Console.Error.WriteLine("Viewer host failed to start or process a request.");
    return 1;
}

static ProjectGmshOptions ReadGmshOptions(JsonElement request)
{
    double? minimum = request.TryGetProperty("minimumElementSize", out var minimumValue) &&
        minimumValue.ValueKind != JsonValueKind.Null ? minimumValue.GetDouble() : null;
    string algorithmName = request.TryGetProperty("algorithm", out var algorithmValue)
        ? algorithmValue.GetString() ?? "auto" : "auto";
    ProjectGmshAlgorithm algorithm = algorithmName.ToLowerInvariant() switch
    {
        "auto" or "automatic" => ProjectGmshAlgorithm.Automatic,
        "delaunay" => ProjectGmshAlgorithm.Delaunay,
        "frontal" => ProjectGmshAlgorithm.Frontal,
        "hxt" => ProjectGmshAlgorithm.Hxt,
        _ => throw new ArgumentException("Unsupported Gmsh algorithm.")
    };
    bool optimize = request.TryGetProperty("optimize", out var optimizeValue) && optimizeValue.GetBoolean();
    bool secondOrder = request.TryGetProperty("secondOrder", out var orderValue) && orderValue.GetBoolean();
    ProjectGmshSizeBox? sizeBox = null;
    if (request.TryGetProperty("sizeBox", out var box) && box.ValueKind != JsonValueKind.Null)
        sizeBox = new(box.GetProperty("MeshSize").GetDouble(), box.GetProperty("XMin").GetDouble(),
            box.GetProperty("YMin").GetDouble(), box.GetProperty("ZMin").GetDouble(),
            box.GetProperty("XMax").GetDouble(), box.GetProperty("YMax").GetDouble(), box.GetProperty("ZMax").GetDouble());
    ProjectGmshFaceSize? faceSize = null;
    if (request.TryGetProperty("faceSize", out var face) && face.ValueKind != JsonValueKind.Null)
        faceSize = new(face.GetProperty("FaceTag").GetInt32(), face.GetProperty("MeshSize").GetDouble(),
            face.GetProperty("TransitionDistance").GetDouble());
    return new(request.GetProperty("maximumElementSize").GetDouble(), minimum, algorithm, optimize, secondOrder, sizeBox, faceSize);
}
