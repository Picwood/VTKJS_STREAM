using CaeJob.Native;
using System.Text.Json;

if (args.Length > 0)
{
    if (args[0] == "echo") { Console.WriteLine(JsonSerializer.Serialize(args.Skip(1))); Console.Error.Write(Environment.CurrentDirectory); return; }
    if (args[0] == "flood") { for (int i = 0; i < 10000; i++) { Console.Write("abcdefghij"); Console.Error.Write("0123456789"); } return; }
    if (args[0] == "sleep") { if (args.Length > 1) { File.WriteAllText(args[1] + ".tmp", Environment.ProcessId.ToString()); File.Move(args[1] + ".tmp", args[1]); } await Task.Delay(60000); return; }
    if (args[0] == "progress") { Console.Write("working"); Console.Out.Flush(); Console.Error.Write("diagnostic"); Console.Error.Flush(); await Task.Delay(60000); return; }
    if (args[0] == "fail") { Environment.ExitCode = 17; return; }
}
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
string executable = Environment.ProcessPath!;
string directory = Path.Combine(Path.GetTempPath(), "prepomax process é " + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    Check(NativeTool.Resolve("unused", executable) == executable, "configured tool path");
    try { NativeTool.Resolve("unused", Path.Combine(directory, "missing")); throw new Exception("accepted missing tool"); }
    catch (FileNotFoundException) { checks++; }
    string[] values = { "two words", "é", "", "\"quoted\"", "$(literal); &" };
    var echo = await NativeProcess.RunAsync(executable, new[] { "echo" }.Concat(values), directory, TimeSpan.FromSeconds(10));
    Check(echo.ExitCode == 0 && JsonSerializer.Deserialize<string[]>(echo.StandardOutput)!.SequenceEqual(values), "literal arguments");
    Check(echo.StandardError == directory, "working directory and stderr");
    var flood = await NativeProcess.RunAsync(executable, new[] { "flood" }, directory, TimeSpan.FromSeconds(20), captureLimit: 1024);
    Check(flood.ExitCode == 0 && flood.StandardOutput.Length == 1024 && flood.StandardError.Length == 1024 && flood.OutputTruncated && flood.ErrorTruncated, "concurrent bounded capture drains streams");
    var failed = await NativeProcess.RunAsync(executable, new[] { "fail" }, directory, TimeSpan.FromSeconds(10));
    Check(failed.ExitCode == 17, "nonzero exit preserved");
    try { await NativeProcess.RunAsync(executable, new[] { "sleep" }, directory, TimeSpan.FromMilliseconds(200)); throw new Exception("timeout missing"); }
    catch (TimeoutException) { checks++; }
    using var cancel = new CancellationTokenSource();
    string pidFile = Path.Combine(directory, "started.pid");
    var running = NativeProcess.RunAsync(executable, new[] { "sleep", pidFile }, directory, TimeSpan.FromSeconds(15), cancel.Token);
    var watch = System.Diagnostics.Stopwatch.StartNew();
    while (!File.Exists(pidFile) && !running.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20);
    Check(File.Exists(pidFile), "cancellable process started");
    int pid = int.Parse(File.ReadAllText(pidFile));
    cancel.Cancel();
    try { await running; throw new Exception("cancellation missing"); }
    catch (OperationCanceledException) { checks++; }
    bool exited;
    try { using var child = System.Diagnostics.Process.GetProcessById(pid); exited = child.HasExited; }
    catch (ArgumentException) { exited = true; }
    Check(exited, "cancellation reaps direct child");
    string oldPath = Environment.GetEnvironmentVariable("PATH");
    try
    {
        Environment.SetEnvironmentVariable("PATH", Path.GetDirectoryName(executable));
        Check(NativeTool.Resolve(Path.GetFileName(executable)) == executable, "PATH discovery");
    }
    finally { Environment.SetEnvironmentVariable("PATH", oldPath); }
    var jobArgs = new[] { "echo", "original" };
    var job = new NativeJob(executable, jobArgs, directory, TimeSpan.FromSeconds(10));
    jobArgs[1] = "changed";
    Check(job.Status == CaeJob.JobStatus.None, "job starts idle");
    var success = await job.RunAsync();
    Check(success.Status == CaeJob.JobStatus.OK && job.Status == success.Status && success.Error == null && success.Process.ExitCode == 0, "successful job status");
    Check(JsonSerializer.Deserialize<string[]>(success.Process.StandardOutput)!.Single() == "original", "job snapshots arguments");
    try { await job.RunAsync(); throw new Exception("job ran twice"); }
    catch (InvalidOperationException) { checks++; }
    var badExit = await new NativeJob(executable, new[] { "fail" }, directory, TimeSpan.FromSeconds(10)).RunAsync();
    Check(badExit.Status == CaeJob.JobStatus.Failed && badExit.Process.ExitCode == 17 && badExit.Error == null, "job preserves failing exit result");
    var launchFailure = await new NativeJob(Path.Combine(directory, "missing executable"), Array.Empty<string>(), directory, TimeSpan.FromSeconds(10)).RunAsync();
    Check(launchFailure.Status == CaeJob.JobStatus.Failed && launchFailure.Process == null && launchFailure.Error != null, "job launch error diagnostic");
    var timed = await new NativeJob(executable, new[] { "sleep" }, directory, TimeSpan.FromMilliseconds(200)).RunAsync();
    Check(timed.Status == CaeJob.JobStatus.TimedOut && timed.Error is TimeoutException, "job timeout classification");
    using var alreadyCancelled = new CancellationTokenSource(); alreadyCancelled.Cancel();
    var killed = await new NativeJob(executable, new[] { "sleep" }, directory, TimeSpan.FromSeconds(10)).RunAsync(alreadyCancelled.Token);
    Check(killed.Status == CaeJob.JobStatus.Killed && killed.Error is OperationCanceledException, "job prelaunch cancellation");
    using var jobCancellation = new CancellationTokenSource();
    string jobPidFile = Path.Combine(directory, "job.pid");
    var cancellableJob = new NativeJob(executable, new[] { "sleep", jobPidFile }, directory, TimeSpan.FromSeconds(15));
    var jobTask = cancellableJob.RunAsync(jobCancellation.Token);
    watch.Restart();
    while (!File.Exists(jobPidFile) && !jobTask.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20);
    Check(File.Exists(jobPidFile) && cancellableJob.Status == CaeJob.JobStatus.Running, "running job status");
    try { await cancellableJob.RunAsync(); throw new Exception("concurrent job run accepted"); }
    catch (InvalidOperationException) { checks++; }
    jobCancellation.Cancel();
    var cancelledJob = await jobTask;
    Check(cancelledJob.Status == CaeJob.JobStatus.Killed && cancellableJob.Status == CaeJob.JobStatus.Killed && cancelledJob.Elapsed > TimeSpan.Zero, "running job cancellation transition");
    using var progressCancellation = new CancellationTokenSource();
    var progressJob = new NativeJob(executable, new[] { "progress" }, directory, TimeSpan.FromSeconds(15));
    var progressTask = progressJob.RunAsync(progressCancellation.Token);
    watch.Restart();
    while ((progressJob.Progress.StandardErrorCharacters < 10 || progressJob.Progress.StandardOutputCharacters < 7) && !progressTask.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20);
    Check(progressJob.Status == CaeJob.JobStatus.Running && progressJob.Progress.StandardOutputCharacters == 7 &&
          progressJob.Progress.StandardErrorCharacters == 10 && progressJob.Elapsed > TimeSpan.Zero, "live progress before process exit");
    progressCancellation.Cancel();
    var progressResult = await progressTask;
    Check(progressResult.Status == CaeJob.JobStatus.Killed && progressResult.Elapsed == progressJob.Elapsed &&
          progressJob.Progress.StandardErrorCharacters == 10, "terminal progress retained");
    TimeSpan stoppedElapsed = progressJob.Elapsed;
    await Task.Delay(30);
    Check(stoppedElapsed == progressJob.Elapsed, "elapsed time stops at completion");
    var floodJob = new NativeJob(executable, new[] { "flood" }, directory, TimeSpan.FromSeconds(20), 32);
    var floodResult = await floodJob.RunAsync();
    Check(floodResult.Status == CaeJob.JobStatus.OK && floodResult.Process.OutputTruncated &&
          floodJob.Progress.StandardOutputCharacters == 100000 && floodJob.Progress.StandardErrorCharacters == 100000,
          "progress counts include truncated output");
    var logs = new NativeLogFiles(Path.Combine(directory, "stdout.log"), Path.Combine(directory, "stderr.log"));
    var logged = await new NativeJob(executable, new[] { "flood" }, directory, TimeSpan.FromSeconds(20), 32, logs).RunAsync();
    Check(logged.Status == CaeJob.JobStatus.OK && logged.Process.StandardOutput.Length == 32 &&
          File.ReadAllText(logs.StandardOutput).Length == 100000 && File.ReadAllText(logs.StandardError).Length == 100000, "persistent logs exceed capture limit");
    var reused = await new NativeJob(executable, new[] { "echo" }, directory, TimeSpan.FromSeconds(10), logs: logs).RunAsync();
    Check(reused.Status == CaeJob.JobStatus.Failed && reused.Error is IOException && File.ReadAllText(logs.StandardOutput).Length == 100000, "existing logs are preserved");
    var cancelLogs = new NativeLogFiles(Path.Combine(directory, "cancel.out"), Path.Combine(directory, "cancel.err"));
    using var loggedCancel = new CancellationTokenSource();
    var loggedJob = new NativeJob(executable, new[] { "progress" }, directory, TimeSpan.FromSeconds(15), logs: cancelLogs);
    var loggedTask = loggedJob.RunAsync(loggedCancel.Token);
    watch.Restart();
    while (loggedJob.Progress.StandardErrorCharacters < 10 && !loggedTask.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20);
    loggedCancel.Cancel();
    var loggedResult = await loggedTask;
    Check(loggedResult.Status == CaeJob.JobStatus.Killed && File.ReadAllText(cancelLogs.StandardError) == "diagnostic", "logs survive cancellation");
    Console.WriteLine($"PASS: {checks} native process characterization checks.");
}
finally { Directory.Delete(directory, true); }
