namespace CaeJob.Native;

/// <summary>A one-shot native invocation. Status is pollable; completion carries diagnostics.</summary>
public sealed class NativeJob
{
    private readonly string _executable;
    private readonly string[] _arguments;
    private readonly string _workingDirectory;
    private readonly TimeSpan _timeout;
    private readonly int _captureLimit;
    private readonly NativeLogFiles? _logs;
    private int _started;
    private int _status = (int)JobStatus.None;

    private readonly System.Diagnostics.Stopwatch _watch = new();
    private readonly object _clockLock = new();
    public NativeProcessProgress Progress { get; } = new();
    public TimeSpan Elapsed { get { lock (_clockLock) return _watch.Elapsed; } }

    public JobStatus Status => (JobStatus)Volatile.Read(ref _status);

    public NativeJob(string executable, IEnumerable<string> arguments, string workingDirectory,
                     TimeSpan timeout, int captureLimit = 1024 * 1024, NativeLogFiles? logs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (captureLimit < 0) throw new ArgumentOutOfRangeException(nameof(captureLimit));
        _executable = Path.GetFullPath(executable);
        _arguments = arguments.ToArray();
        _workingDirectory = Path.GetFullPath(workingDirectory);
        _timeout = timeout;
        _captureLimit = captureLimit;
        _logs = logs;
    }

    public async Task<NativeJobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("A native job can only be run once. Create a new job to retry.");
        lock (_clockLock) _watch.Start();
        NativeProcessResult? process = null;
        Exception? error = null;
        JobStatus terminal;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _status, (int)JobStatus.Running);
            process = await NativeProcess.RunAsync(_executable, _arguments, _workingDirectory, _timeout,
                                                   cancellationToken, _captureLimit, Progress, _logs).ConfigureAwait(false);
            terminal = process.ExitCode == 0 ? JobStatus.OK : JobStatus.Failed;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            terminal = JobStatus.Killed;
            error = ex;
        }
        catch (TimeoutException ex)
        {
            terminal = JobStatus.TimedOut;
            error = ex;
        }
        catch (Exception ex)
        {
            terminal = JobStatus.Failed;
            error = ex;
        }
        lock (_clockLock) _watch.Stop();
        Volatile.Write(ref _status, (int)terminal);
        return new NativeJobResult(terminal, process, error, Elapsed);
    }
}

public sealed record NativeJobResult(JobStatus Status, NativeProcessResult? Process, Exception? Error, TimeSpan Elapsed);
