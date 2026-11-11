using PrePoMax.Application;

internal sealed class ViewerJobs : IAsyncDisposable
{
    private CancellationTokenSource? _stop;
    private Task<ProjectSolveResult>? _task;
    private ProjectSolveJob? _job;
    public bool Active => _task is { IsCompleted: false };

    public bool Start(string project, string root, string? solver)
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
                var job = ProjectService.PrepareSolve(project, root, solver);
                Volatile.Write(ref _job, job);
                return await job.RunAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return new ProjectSolveResult(ProjectSolveStatus.Cancelled, "", null, null, null);
            }
            catch (Exception ex)
            {
                return new ProjectSolveResult(ProjectSolveStatus.Failed, "", null, null, ex);
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
            elapsedSeconds = job?.SolverElapsed.TotalSeconds ?? 0,
            outputCharacters = job?.Progress.StandardOutputCharacters ?? 0,
            jobDirectory = job?.JobDirectory,
            archivePath = result?.ArchivePath,
            error = result?.Status == ProjectSolveStatus.Failed ? "Analysis failed. Check the job files and solver configuration." : null
        };
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        if (_task != null) await _task;
        _stop?.Dispose();
    }
}
