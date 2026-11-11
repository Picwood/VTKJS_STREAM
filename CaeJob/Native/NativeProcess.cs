// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System.Diagnostics;
using System.Text;

namespace CaeJob.Native;

public sealed record NativeProcessResult(int ExitCode, string StandardOutput, string StandardError,
                                         bool OutputTruncated, bool ErrorTruncated);

public static class NativeProcess
{
    public static async Task<NativeProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken = default,
        int captureLimit = 1024 * 1024, NativeProcessProgress? progress = null, NativeLogFiles? logs = null)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (captureLimit < 0) throw new ArgumentOutOfRangeException(nameof(captureLimit));
        cancellationToken.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo(Path.GetFullPath(executable))
        {
            WorkingDirectory = Path.GetFullPath(workingDirectory), UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        using var deadline = new CancellationTokenSource(timeout);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using var outputLog = OpenLog(logs?.StandardOutput);
        using var errorLog = OpenLog(logs?.StandardError);
        process.Start();
        var stdout = CaptureAsync(process.StandardOutput, captureLimit, stop.Token, progress, false, outputLog);
        var stderr = CaptureAsync(process.StandardError, captureLimit, stop.Token, progress, true, errorLog);
        try
        {
            await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            return new(process.ExitCode, stdout.Result.text, stderr.Result.text, stdout.Result.truncated, stderr.Result.truncated);
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            stop.Cancel();
            try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); } catch (OperationCanceledException) { }
            cancellationToken.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested) throw new TimeoutException($"Native process exceeded {timeout}.");
            throw;
        }
    }

    private static StreamWriter? OpenLog(string? path) => path == null ? null :
        new StreamWriter(new FileStream(Path.GetFullPath(path), FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                         new UTF8Encoding(false));

    private static async Task<(string text, bool truncated)> CaptureAsync(StreamReader reader, int limit, CancellationToken token, NativeProcessProgress? progress, bool isError, StreamWriter? log)
    {
        var output = new StringBuilder();
        var buffer = new char[4096];
        bool truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            if (log != null)
            {
                await log.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
                await log.FlushAsync().ConfigureAwait(false);
            }
            progress?.Add(count, isError);
            int keep = Math.Min(count, limit - output.Length);
            output.Append(buffer, 0, keep);
            truncated |= keep != count;
        }
        return (output.ToString(), truncated);
    }
}

/// <summary>New UTF-8 log files; existing files are never overwritten. Parent directories must exist.</summary>
public sealed record NativeLogFiles(string StandardOutput, string StandardError);
