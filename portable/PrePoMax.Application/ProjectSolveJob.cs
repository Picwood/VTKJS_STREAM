using CaeJob;
using CaeJob.Native;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public enum ProjectSolveStatus { Prepared, Running, SavingResults, Succeeded, Failed, Cancelled, TimedOut }
public sealed record ProjectSolveResult(ProjectSolveStatus Status, string JobDirectory,
                                       string? ArchivePath, NativeJobResult? Solver, Exception? Error);

/// <summary>One-shot job owning its reopened model. Pollable state is independent of any UI.
/// Artifacts and logs remain available after failure or cancellation.</summary>
public sealed class ProjectSolveJob
{
    private readonly ReopenedStaticProject _project;
    private readonly NativeJob _solver;
    private int _started;
    private int _status = (int)ProjectSolveStatus.Prepared;
    public string JobDirectory { get; }
    public ProjectSolveStatus Status => (ProjectSolveStatus)Volatile.Read(ref _status);
    public NativeProcessProgress Progress => _solver.Progress;
    public TimeSpan SolverElapsed => _solver.Elapsed;

    internal ProjectSolveJob(ReopenedStaticProject project, string directory, string solver, TimeSpan timeout)
    {
        _project = project;
        JobDirectory = directory;
        _solver = new NativeJob(solver, new[] { "-i", "analysis" }, directory, timeout,
            logs: new NativeLogFiles(Path.Combine(directory, "stdout.log"), Path.Combine(directory, "stderr.log")));
    }

    public async Task<ProjectSolveResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("A project job can only run once. Prepare a new job to retry.");
        NativeJobResult? solver = null;
        ProjectSolveResult Complete(ProjectSolveStatus status, string? archive, Exception? error)
        {
            Volatile.Write(ref _status, (int)status);
            return new(status, JobDirectory, archive, solver, error);
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _status, (int)ProjectSolveStatus.Running);
            solver = await _solver.RunAsync(cancellationToken).ConfigureAwait(false);
            if (solver.Status != JobStatus.OK)
                return Complete(solver.Status switch
                {
                    JobStatus.Killed => ProjectSolveStatus.Cancelled,
                    JobStatus.TimedOut => ProjectSolveStatus.TimedOut,
                    _ => ProjectSolveStatus.Failed
                }, null, solver.Error);
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _status, (int)ProjectSolveStatus.SavingResults);
            byte[] frd = ReadResult("frd");
            bool historyRequested = _project.Model.StepCollection.StepsList
                .Where(step => step.Active && step.RunAnalysis)
                .Any(step => step.HistoryOutputs.Values.Any(output => output.Active));
            string historyPath = Path.Combine(JobDirectory, "analysis.dat");
            byte[]? dat = !historyRequested && (!File.Exists(historyPath) || new FileInfo(historyPath).Length == 0)
                ? null : ReadResult("dat");
            cancellationToken.ThrowIfCancellationRequested();
            string output = Path.Combine(JobDirectory, "result.pma");
            // Atomic archive publication is the commit point; cancellation after this point
            // does not turn a successfully published result into a cancelled job.
            StaticProjectArchive.Save(output, _project.Model, frd, dat, geometrySource: _project.GeometrySource,
                materialIdentities: _project.MaterialIdentities);
            return Complete(ProjectSolveStatus.Succeeded, output, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Complete(ProjectSolveStatus.Cancelled, null, null);
        }
        catch (Exception ex)
        {
            return Complete(ProjectSolveStatus.Failed, null, ex);
        }
    }

    private byte[] ReadResult(string extension)
    {
        string path = Path.Combine(JobDirectory, "analysis." + extension);
        long length = new FileInfo(path).Length;
        if (length == 0 || length > 128 * 1024 * 1024)
            throw new InvalidDataException($"Empty or oversized {extension} results (limit 128 MiB).");
        return File.ReadAllBytes(path);
    }
}
