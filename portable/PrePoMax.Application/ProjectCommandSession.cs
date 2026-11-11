using System.Security.Cryptography;
using System.Text.Json.Serialization;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public interface IProjectCommand { Guid OperationId { get; } int ExpectedRevision { get; } }

// Null elastic values mean preserve. Names remain accepted for the original pilot API.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaterialElasticCommand(Guid OperationId, int ExpectedRevision, string? MaterialName = null,
    double? YoungsModulus = null, double? PoissonsRatio = null, Guid? MaterialId = null) : IProjectCommand;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaterialRenameCommand(Guid OperationId, int ExpectedRevision, Guid MaterialId, string NewName) : IProjectCommand;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaterialCreateCommand(Guid OperationId, int ExpectedRevision, string Name, double YoungsModulus, double PoissonsRatio) : IProjectCommand;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaterialRemoveCommand(Guid OperationId, int ExpectedRevision, Guid MaterialId) : IProjectCommand;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaterialAssignCommand(Guid OperationId, int ExpectedRevision, string SectionName, Guid MaterialId) : IProjectCommand;

public sealed record MaterialCommandReceipt(string Kind, MaterialElasticCommand Command,
    int SourceRevision, int CommittedRevision, string SourceSha256, string ResultSha256, Guid MaterialId);

public sealed record MaterialRenameReceipt(string Kind, MaterialRenameCommand Command, string PreviousName,
    int SourceRevision, int CommittedRevision, string SourceSha256, string ResultSha256, Guid MaterialId);

public sealed record MaterialCreateReceipt(string Kind, MaterialCreateCommand Command,
    int SourceRevision, int CommittedRevision, string SourceSha256, string ResultSha256, Guid MaterialId);

public sealed record MaterialRemoveReceipt(string Kind, MaterialRemoveCommand Command, string MaterialName,
    int SourceRevision, int CommittedRevision, string SourceSha256, string ResultSha256, Guid MaterialId);

public sealed record MaterialAssignReceipt(string Kind, MaterialAssignCommand Command,
    int SourceRevision, int CommittedRevision, string SourceSha256, string ResultSha256, Guid MaterialId);

public sealed class ProjectCommandConflictException(string message) : Exception(message);

/// <summary>Host-independent single-operation publication and bounded snapshot history.
/// The owning host must serialize access. Receipts are session-local; this is not a durable journal.</summary>
public sealed class ProjectCommandSession(string directory)
{
    private readonly string _directory = Path.GetFullPath(directory);
    private sealed record Snapshot(string Path, Guid? CommandId);
    private readonly List<Snapshot> _history = [];
    private sealed record CompletedCommand(object Command, object Receipt);
    private readonly Dictionary<Guid, CompletedCommand> _completed = [];
    private int _position = -1;
    public string? ProjectPath => _position < 0 ? null : _history[_position].Path;
    public int Revision { get; private set; }
    public bool CanUndo => _position > 0;
    public bool CanRedo => _position < _history.Count - 1;
    public Guid? CurrentCommandId => _position < 0 ? null : _history[_position].CommandId;
    public object[] Commands => _completed.Values.Select(item => item.Receipt).ToArray();

    public MaterialCommandReceipt Execute(MaterialElasticCommand command)
        => Execute(command, (source, candidate) =>
        {
            var identity = Resolve(source, command.MaterialId, command.MaterialName);
            ProjectMaterials.PatchElasticCopy(source, candidate, identity.Name, command.YoungsModulus, command.PoissonsRatio);
            return identity;
        }, (identity, revision, sourceHash, resultHash) => new MaterialCommandReceipt("material.elastic.patch/1", command,
            revision, checked(revision + 1), sourceHash, resultHash, identity.Id));

    public MaterialRenameReceipt Rename(MaterialRenameCommand command)
        => Execute(command, (source, candidate) =>
        {
            var identity = Resolve(source, command.MaterialId, null);
            ProjectMaterials.RenameCopy(source, candidate, identity.Name, command.NewName);
            return identity;
        }, (identity, revision, sourceHash, resultHash) => new MaterialRenameReceipt("material.rename/1", command, identity.Name,
            revision, checked(revision + 1), sourceHash, resultHash, identity.Id));

    public MaterialCreateReceipt Create(MaterialCreateCommand command)
        => Execute(command, (source, candidate) =>
        {
            ProjectMaterials.AddElasticCopy(source, candidate, command.Name, command.YoungsModulus, command.PoissonsRatio);
            return StaticProjectArchive.Open(candidate).MaterialIdentities.Single(item => item.Name == command.Name);
        }, (identity, revision, sourceHash, resultHash) => new MaterialCreateReceipt("material.elastic.create/1", command,
            revision, checked(revision + 1), sourceHash, resultHash, identity.Id));

    public MaterialRemoveReceipt Remove(MaterialRemoveCommand command)
        => Execute(command, (source, candidate) =>
        {
            var identity = Resolve(source, command.MaterialId, null);
            ProjectMaterials.RemoveUnusedCopy(source, candidate, identity.Name);
            return identity;
        }, (identity, revision, sourceHash, resultHash) => new MaterialRemoveReceipt("material.remove-unused/1", command, identity.Name,
            revision, checked(revision + 1), sourceHash, resultHash, identity.Id));

    public MaterialAssignReceipt Assign(MaterialAssignCommand command)
        => Execute(command, (source, candidate) =>
        {
            var identity = Resolve(source, command.MaterialId, null);
            ProjectMaterials.AssignSolidSectionCopy(source, candidate, command.SectionName, identity.Name);
            return identity;
        }, (identity, revision, sourceHash, resultHash) => new MaterialAssignReceipt("material.assign-solid-section/1", command,
            revision, checked(revision + 1), sourceHash, resultHash, identity.Id));

    private TReceipt Execute<TCommand, TReceipt>(TCommand command,
        Func<string, string, ProjectMaterialIdentity> apply,
        Func<ProjectMaterialIdentity, int, string, string, TReceipt> createReceipt) where TCommand : IProjectCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.OperationId == Guid.Empty || command.ExpectedRevision < 0)
            throw new ArgumentException("Supply an operation ID and source revision.");
        // Check receipts before the revision: retrying the identical request never edits twice,
        // even after undo, another command or eviction of its old snapshot.
        if (_completed.TryGetValue(command.OperationId, out var completed))
        {
            if (!completed.Command.Equals(command))
                throw new ProjectCommandConflictException("This operation ID already identifies a different command.");
            return (TReceipt)completed.Receipt;
        }
        if (command.ExpectedRevision != Revision)
            throw new ProjectCommandConflictException("The project changed. Refresh before editing.");
        string source = ProjectPath ?? throw new ProjectCommandConflictException("Open or import a project first.");
        if (_completed.Count >= 4096)
            throw new ProjectCommandConflictException("This pilot session has reached its command limit. Download the project and open a new workspace.");
        string candidate = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".pma");
        try
        {
            string sourceHash = Hash(source);
            var identity = apply(source, candidate);
            var receipt = createReceipt(identity, Revision, sourceHash, Hash(candidate));
            Commit(candidate, command.OperationId);
            _completed.Add(command.OperationId, new CompletedCommand(command, receipt!));
            return receipt;
        }
        finally { if (candidate != ProjectPath && File.Exists(candidate)) File.Delete(candidate); }
    }

    public ProjectMaterialIdentity[] Materials => ProjectPath == null ? [] : StaticProjectArchive.Open(ProjectPath).MaterialIdentities;

    private static ProjectMaterialIdentity Resolve(string source, Guid? id, string? name)
    {
        if (id == Guid.Empty || id == null && string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Select a material ID or name.");
        var identities = StaticProjectArchive.Open(source).MaterialIdentities;
        if (id != null)
        {
            var identity = identities.SingleOrDefault(item => item.Id == id)
                ?? throw new ProjectCommandConflictException("This material no longer exists in the current project.");
            if (name != null && !identity.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                throw new ProjectCommandConflictException("The material name no longer matches its ID. Refresh before editing.");
            return identity;
        }
        return identities.SingleOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException("Select an existing material.");
    }

    /// <summary>Publish an import or an edit not yet migrated to a typed command.</summary>
    public void Commit(string candidate) => Commit(candidate, null);

    private void Commit(string candidate, Guid? commandId)
    {
        candidate = Path.GetFullPath(candidate);
        if (_history.Any(item => Path.GetFullPath(item.Path) == candidate))
            throw new ArgumentException("Publish a new snapshot, not an existing history file.");
        ProjectService.Inspect(candidate);
        ProjectResults.List(candidate);
        int nextRevision = checked(Revision + 1);
        var discarded = _history.Skip(_position + 1).Select(item => item.Path).ToList();
        _history.RemoveRange(_position + 1, _history.Count - _position - 1);
        _history.Add(new Snapshot(candidate, commandId));
        if (_history.Count > 21) { discarded.Add(_history[0].Path); _history.RemoveAt(0); }
        _position = _history.Count - 1;
        Revision = nextRevision;
        // Cleanup must not turn an accepted publication into a reported rejection. The host
        // owns this directory and removes any remaining files when the session expires.
        foreach (string path in discarded)
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
    }

    public void MoveHistory(bool redo)
    {
        int next = _position + (redo ? 1 : -1);
        if (next < 0 || next >= _history.Count)
            throw new ProjectCommandConflictException("No further project history.");
        int nextRevision = checked(Revision + 1);
        _position = next;
        Revision = nextRevision;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
