using PrePoMax.Application;

namespace PrePoMax.Web;

public sealed class Workspace : IAsyncDisposable
{
    public readonly SemaphoreSlim Gate = new(1, 1);
    public readonly string DirectoryPath = Directory.CreateTempSubdirectory("prepomax-web-").FullName;
    public DateTimeOffset Expires { get; } = DateTimeOffset.UtcNow.AddHours(8);
    private readonly ProjectCommandSession _commands;
    public string? ProjectPath => _commands.ProjectPath;
    public int Revision => _commands.Revision;
    public RendererHost? Renderer { get; private set; }
    public RendererSceneSummary? Scene { get; private set; }
    public Workspace() { _commands = new ProjectCommandSession(DirectoryPath); }

    public string RequireProject() => ProjectPath ?? throw new WorkspaceException(409, "Open or import a project first.");
    public string NewPath(string extension = ".pma") => Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + extension);

    public object Describe()
    {
        if (ProjectPath == null) return new { revision = Revision, hasProject = false };
        var materialIdentities = _commands.Materials;
        var identities = materialIdentities.ToDictionary(item => item.Name, item => item.Id, StringComparer.OrdinalIgnoreCase);
        return new
        {
            revision = Revision, hasProject = true,
            summary = ProjectService.Inspect(ProjectPath),
            tree = ProjectModelTree.Read(ProjectPath),
            materials = ProjectMaterials.List(ProjectPath).Select(item => new {
                id = identities[item.Name], item.Name, item.YoungsModulus, item.PoissonsRatio, item.Density
            }).ToArray(),
            materialIdentities,
            sections = ProjectMaterials.ListSolidSections(ProjectPath),
            steps = ProjectSteps.List(ProjectPath),
            forces = ProjectLoads.List(ProjectPath),
            fields = ProjectResults.List(ProjectPath),
            canUndo = _commands.CanUndo, canRedo = _commands.CanRedo,
            currentCommandId = _commands.CurrentCommandId,
        };
    }

    public object[] CommandHistory() => _commands.Commands;

    private TReceipt Apply<TReceipt>(Func<ProjectCommandSession, TReceipt> operation)
    {
        int before = Revision;
        var receipt = operation(_commands);
        if (Revision != before) InvalidateScene();
        return receipt;
    }

    public MaterialCommandReceipt Execute(MaterialElasticCommand command) => Apply(session => session.Execute(command));
    public MaterialRenameReceipt Rename(MaterialRenameCommand command) => Apply(session => session.Rename(command));
    public MaterialCreateReceipt Create(MaterialCreateCommand command) => Apply(session => session.Create(command));
    public MaterialRemoveReceipt Remove(MaterialRemoveCommand command) => Apply(session => session.Remove(command));
    public MaterialAssignReceipt Assign(MaterialAssignCommand command) => Apply(session => session.Assign(command));

    public Guid MaterialId(string? name)
    {
        RequireProject();
        return _commands.Materials.SingleOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new ArgumentException("Select an existing material.");
    }

    public void Commit(string candidate)
    {
        _commands.Commit(candidate);
        InvalidateScene();
    }

    public void MoveHistory(bool redo)
    {
        _commands.MoveHistory(redo);
        InvalidateScene();
    }

    private void InvalidateScene()
    {
        Scene = null;
        Renderer?.ClearResultSession();
    }

    public async Task<RendererSceneSummary> PublishAsync(string dotnet, string server, SceneRequest request, CancellationToken stop)
    {
        string path = RequireProject();
        if (request.Revision != Revision) throw new WorkspaceException(409, "The project changed. Refresh before displaying results.");
        ResultField? field = null;
        if (request.Field is int index)
        {
            var fields = ProjectResults.List(path);
            if (index < 0 || index >= fields.Length) throw new WorkspaceException(400, "Select an existing result field.");
            field = fields[index];
        }
        if (!double.IsFinite(request.DeformationScale) || Math.Abs(request.DeformationScale) > 1_000_000 ||
            field == null && request.DeformationScale != 0)
            throw new WorkspaceException(400, "Choose a result field and a finite deformation scale within ±1000000.");
        Renderer ??= await RendererHost.StartAsync(dotnet, server, stop);
        Scene = await Renderer.PublishAsync(path, stop, field, request.DeformationScale);
        return Scene;
    }

    public void Edit(EditRequest edit, string candidate)
    {
        string source = RequireProject();
        string Required(string? value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Complete all required fields.");
        double Number(double? value) => value ?? throw new ArgumentException("Enter the required numeric value.");
        switch (edit.Action)
        {
            case "nodeset-add":
                if (edit.Nodes is not { Length: > 0 and <= 100000 }) throw new ArgumentException("Enter 1–100000 node IDs.");
                ProjectNodeSets.AddCopy(source, candidate, Required(edit.Name), edit.Nodes); break;
            case "support-add": ProjectBoundaries.AddFixedCopy(source, candidate, Required(edit.Step), Required(edit.Name), Required(edit.Region)); break;
            case "force-add": ProjectLoads.AddCopy(source, candidate, Required(edit.Step), Required(edit.Name), Required(edit.Region), Number(edit.F1), Number(edit.F2), Number(edit.F3)); break;
            case "force-save": ProjectLoads.SaveForceCopy(source, candidate, Required(edit.Step), Required(edit.Name), Number(edit.F1), Number(edit.F2), Number(edit.F3)); break;
            case "step-add": ProjectSteps.AddCopy(source, candidate, new StaticStepSettings(Required(edit.Name)), copyPrevious: false); break;
            default: throw new ArgumentException("This editor is not available in the browser yet.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Renderer != null) await Renderer.DisposeAsync();
        if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
    }
}

public sealed record EditRequest(string Action, string? Name, string? Step = null, string? Region = null,
    int[]? Nodes = null, double? E = null, double? Nu = null, double? F1 = null, double? F2 = null, double? F3 = null);
public sealed record SceneRequest(int Revision, int? Field = null, double DeformationScale = 0);
public sealed class WorkspaceException(int status, string message) : Exception(message) { public int Status { get; } = status; }
