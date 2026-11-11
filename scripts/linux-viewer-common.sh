#!/usr/bin/env bash
# Shared by the supported-workflow GTK build and launch entry points.
linux_viewer_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
linux_viewer_fail() { printf '%s\n' "$*" >&2; exit 1; }
linux_viewer_dotnet() {
    if [[ -z "${PREPOMAX_DOTNET:-}" && -x "$linux_viewer_root/runtime/dotnet" ]]; then
        linux_dotnet="$linux_viewer_root/runtime/dotnet"
    else
        linux_dotnet="${PREPOMAX_DOTNET:-dotnet}"
        if [[ -z "${PREPOMAX_DOTNET:-}" ]] && ! command -v "$linux_dotnet" >/dev/null 2>&1; then
            linux_dotnet="${XDG_DATA_HOME:-$HOME/.local/share}/prepomax/dotnet/dotnet"
        fi
    fi
    linux_dotnet="$(command -v "$linux_dotnet")" || linux_viewer_fail 'Missing .NET. Install the .NET 10 SDK or set PREPOMAX_DOTNET.'
    linux_dotnet="$(realpath -- "$linux_dotnet")"
    linux_dotnet_root="$(dirname -- "$linux_dotnet")"
    export DOTNET_ROOT="$linux_dotnet_root"
}
