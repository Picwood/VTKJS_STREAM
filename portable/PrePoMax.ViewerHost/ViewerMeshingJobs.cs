using PrePoMax.Application;

internal sealed class ViewerMeshingJobs : IAsyncDisposable
{
    private CancellationTokenSource? _stop;
    private Task<ProjectGmshResult>? _task;
    private ProjectGmshJob? _job;
    public bool Active => _task is { IsCompleted: false };

    public bool Start(string source, string destination, double youngsModulus,
                      double poissonsRatio, ProjectGmshOptions options, string? gmsh)
        => Start(() => ProjectGmshImport.Prepare(source, destination, youngsModulus,
            poissonsRatio, options, gmsh));

    public bool StartRemesh(string source, string destination, ProjectGmshOptions options, string? gmsh)
        => Start(() => ProjectGmshRemesh.Prepare(source, destination, options, gmsh));

    private bool Start(Func<ProjectGmshJob> prepare)
    {
        if (Active) return false;
        _stop?.Dispose();
        _stop = new CancellationTokenSource();
        var token = _stop.Token;
        Volatile.Write(ref _job, null);
        _task = Task.Run(async () =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var job = prepare();
                Volatile.Write(ref _job, job);
                return await job.RunAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return new ProjectGmshResult(ProjectGmshStatus.Cancelled, null, null, null);
            }
            catch (Exception ex)
            {
                return new ProjectGmshResult(ProjectGmshStatus.Failed, null, null, ex);
            }
        });
        return true;
    }

    public void Cancel() => _stop?.Cancel();

    public object Snapshot()
    {
        var job = Volatile.Read(ref _job);
        var result = _task is { IsCompletedSuccessfully: true } ? _task.Result : null;
        return new
        {
            status = result?.Status.ToString() ?? (_task == null ? "Idle" : job?.Status.ToString() ?? "Preparing"),
            active = _task != null && result == null,
            elapsedSeconds = job?.MesherElapsed.TotalSeconds ?? 0,
            outputCharacters = job?.Progress.StandardOutputCharacters ?? 0,
            sourcePath = job?.SourcePath,
            projectPath = result?.ProjectPath,
            error = result?.Status == ProjectGmshStatus.Failed
                ? "Meshing failed. Check the CAD solid, element size and Gmsh installation." : null
        };
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        if (_task != null) await _task;
        _stop?.Dispose();
    }
}
