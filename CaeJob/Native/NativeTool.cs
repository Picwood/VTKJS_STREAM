// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

namespace CaeJob.Native;

public static class NativeTool
{
    public static string Resolve(string name, string? configuredPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (configuredPath != null)
        {
            string path = Path.GetFullPath(configuredPath);
            if (!File.Exists(path)) throw new FileNotFoundException("Configured native tool was not found.", path);
            return path;
        }
        string fileName = OperatingSystem.IsWindows() && !Path.HasExtension(name) ? name + ".exe" : name;
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            string path = Path.GetFullPath(Path.Combine(directory, fileName));
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException($"Native tool '{name}' was not found on PATH.");
    }
}
