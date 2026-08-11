// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using CaeMesh.Meshing.BaramMesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PrePoMax
{
    public class BaramOpenFoamRunner : IBaramOpenFoamRunner
    {
        private static readonly string[] RequiredUtilities = new string[]
        {
            "blockMesh",
            "snappyHexMesh",
            "checkMesh",
            "topoSet",
            "splitMeshRegions",
            "createPatch",
            "extrudeMesh",
            "collapseEdges",
            "decomposePar",
            "reconstructParMesh"
        };

        private readonly object _lock;
        private Process _process;

        public BaramOpenFoamRunner()
        {
            _lock = new object();
        }

        public bool IsValidOpenFoamRoot(string openFoamRoot, out string message)
        {
            if (string.IsNullOrWhiteSpace(openFoamRoot))
            {
                message = "The OpenFOAM root directory is not set.";
                return false;
            }
            if (!Directory.Exists(openFoamRoot))
            {
                message = "The OpenFOAM root directory does not exist: " + openFoamRoot;
                return false;
            }

            List<string> missing = new List<string>();
            foreach (string utility in RequiredUtilities)
            {
                if (ResolveUtilityPath(openFoamRoot, utility) == null) missing.Add(utility);
            }

            if (missing.Count > 0)
            {
                message = "Missing OpenFOAM utilities: " + string.Join(", ", missing.ToArray()) + ".";
                return false;
            }

            message = "OpenFOAM root directory is valid.";
            return true;
        }

        public int Run(string utilityName, string arguments, string workingDirectory,
                       IDictionary<string, string> environment, Action<string> appendOutput)
        {
            if (string.IsNullOrWhiteSpace(utilityName)) throw new ArgumentException("Utility name is required.", "utilityName");
            if (string.IsNullOrWhiteSpace(workingDirectory)) throw new ArgumentException("Working directory is required.", "workingDirectory");
            if (!Directory.Exists(workingDirectory)) Directory.CreateDirectory(workingDirectory);

            string executable = File.Exists(utilityName) ? utilityName : ResolveUtilityPath(null, utilityName);
            if (executable == null) executable = utilityName;
            string runArguments = arguments ?? "";

            ProcessStartInfo psi = new ProcessStartInfo();
            if (IsBatchFile(executable))
            {
                psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                psi.Arguments = "/c \"" + executable + "\" " + runArguments;
            }
            else
            {
                psi.FileName = executable;
                psi.Arguments = runArguments;
            }
            psi.WorkingDirectory = workingDirectory;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;

            if (environment != null)
            {
                foreach (KeyValuePair<string, string> entry in environment)
                {
                    psi.EnvironmentVariables[entry.Key] = entry.Value ?? "";
                }
            }

            using (ManualResetEvent outputDone = new ManualResetEvent(false))
            using (ManualResetEvent errorDone = new ManualResetEvent(false))
            {
                Process process = new Process();
                process.StartInfo = psi;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null) outputDone.Set();
                    else if (appendOutput != null) appendOutput(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null) errorDone.Set();
                    else if (appendOutput != null) appendOutput(e.Data);
                };

                lock (_lock) _process = process;
                try
                {
                    if (appendOutput != null) appendOutput("Running command: " + executable + " " + runArguments);
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    outputDone.WaitOne();
                    errorDone.WaitOne();
                    return process.ExitCode;
                }
                finally
                {
                    lock (_lock)
                    {
                        if (_process == process) _process = null;
                    }
                    process.Close();
                }
            }
        }

        private static bool IsBatchFile(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            return string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase);
        }

        public void Cancel()
        {
            lock (_lock)
            {
                try
                {
                    if (_process != null && !_process.HasExited) _process.Kill();
                }
                catch
                {
                }
            }
        }

        public static string ResolveUtilityPath(string openFoamRoot, string utilityName)
        {
            if (string.IsNullOrWhiteSpace(utilityName)) return null;

            string[] extensions = new string[] { ".exe", ".bat", ".cmd", "" };
            List<string> directories = new List<string>();
            if (!string.IsNullOrWhiteSpace(openFoamRoot))
            {
                AddOpenFoamSearchDirectories(openFoamRoot, directories);
            }
            string path = Environment.GetEnvironmentVariable("PATH");
            if (path != null) directories.AddRange(path.Split(Path.PathSeparator));

            foreach (string directory in directories)
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                foreach (string extension in extensions)
                {
                    string candidate = Path.Combine(directory, utilityName + extension);
                    if (File.Exists(candidate)) return candidate;
                }
            }

            return null;
        }

        public static void AddOpenFoamSearchDirectories(string openFoamRoot, List<string> directories)
        {
            if (string.IsNullOrWhiteSpace(openFoamRoot) || directories == null) return;
            AddDirectory(directories, Path.Combine(openFoamRoot, "bin"));
            AddDirectory(directories, Path.Combine(openFoamRoot, "lib"));
            AddDirectory(directories, Path.Combine(openFoamRoot, "lib", "msmpi"));
            AddDirectory(directories, Path.Combine(openFoamRoot, "lib", "sys-openmpi"));
            AddDirectory(directories, Path.Combine(openFoamRoot, "lib", "dummy"));
            AddDirectory(directories, openFoamRoot);
            AddDirectory(directories, Path.Combine(openFoamRoot, "mingw64", "bin"));
            AddDirectory(directories, Path.Combine(openFoamRoot, "mingw64", "lib"));
            //
            string parent = Path.GetDirectoryName(openFoamRoot);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                AddDirectory(directories, Path.Combine(parent, "mingw64", "bin"));
                AddDirectory(directories, Path.Combine(parent, "mingw64", "lib"));
            }
            //
            string platforms = Path.Combine(openFoamRoot, "platforms");
            if (Directory.Exists(platforms))
            {
                foreach (string directory in Directory.GetDirectories(platforms, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(directory);
                    if (string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "lib", StringComparison.OrdinalIgnoreCase))
                        AddDirectory(directories, directory);
                }
            }
        }

        private static void AddDirectory(List<string> directories, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
            foreach (string existing in directories)
            {
                if (string.Equals(existing, directory, StringComparison.OrdinalIgnoreCase)) return;
            }
            directories.Add(directory);
        }
    }
}
