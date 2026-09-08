# Linux x64 functional qualification

Date: 2026-09-01

Command, run from the repository root:

```powershell
.\scripts\smoke-vtkjs-linux-browser.ps1
```

## Environment

- Distribution: Ubuntu 22.04 under WSL2
- Kernel: `5.15.153.1-microsoft-standard-WSL2`
- Architecture: `x86_64`
- Server: self-contained .NET `linux-x64` publish
- Client runtime: Node.js `v24.20.0`
- Browser: Chromium Headless Shell `151.0.7922.34`
- Graphics: software WebGL2 (`--use-angle=swiftshader`)

The runner downloads the fixed Node archive and verifies it against Node's published SHA-256 list. It installs the lockfile-pinned Playwright package in a disposable directory. Because the WSL image lacks `libasound.so.2`, the runner downloads the Ubuntu `libasound2` package and extracts it into that same disposable directory. It does not use `sudo` or modify the distribution.

## Results

The server lifecycle gate passed:

- protocol `1.0` health response;
- production index HTTP 200;
- desktop-authorized Production session creation;
- manifest with 2 actors and 10 immutable resources;
- resource HTTP 200, exact SHA-256 match, and byte-range advertisement;
- invalid shutdown credential HTTP 401;
- valid shutdown HTTP 202 and clean process exit;
- captured Production logs contain neither the host credential nor publish/client filesystem roots.

The Linux browser gate passed:

- WebGL2 available;
- 2 renderer actors and 2 resident pipelines;
- 6 points and 2 polygons;
- exactly 152 resource bytes transferred;
- credentialed HTTP 202 shutdown and clean server exit.
- downloadable diagnostics contain no credential or filesystem path.

## Scope

This is a functional server/client qualification using software WebGL. It does not claim Linux physical-GPU performance, the 3M benchmark gates, or operation of the Windows-specific PrePoMax WinForms/WebView2 shell. Those remain separate acceptance activities.
