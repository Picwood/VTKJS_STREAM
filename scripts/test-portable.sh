#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root/portable"
dotnet_command="${PREPOMAX_DOTNET:-dotnet}"
if [[ -z "${PREPOMAX_DOTNET:-}" ]] && ! command -v "$dotnet_command" >/dev/null 2>&1; then
    local_sdk="${XDG_DATA_HOME:-$HOME/.local/share}/prepomax/dotnet/dotnet"
    if [[ -x "$local_sdk" ]]; then dotnet_command="$local_sdk"; fi
fi
if ! command -v "$dotnet_command" >/dev/null 2>&1; then
    echo "Missing .NET SDK. Install .NET 10 or set PREPOMAX_DOTNET to its dotnet executable." >&2
    exit 1
fi
"$dotnet_command" build PrePoMax.Core.slnx --configuration Release
"$dotnet_command" run --project tests/STL.Characterization/STL.Characterization.csproj --configuration Release
"$dotnet_command" run --project tests/NCalc.LegacyOracle/NCalc.LegacyOracle.csproj --configuration Release --framework net10.0
"$dotnet_command" run --project tests/CaeGlobals.Characterization/CaeGlobals.Characterization.csproj --configuration Release
"$dotnet_command" run --project tests/CaeMesh.Characterization/CaeMesh.Characterization.csproj --configuration Release
"$dotnet_command" run --project tests/MeshIO.Characterization/MeshIO.Characterization.csproj --configuration Release
"$dotnet_command" run --project tests/CaeJob.Characterization/CaeJob.Characterization.csproj --configuration Release
"$dotnet_command" run --project tests/CaeResults.Characterization/CaeResults.Characterization.csproj --configuration Release
if [[ "${OS:-}" == "Windows_NT" ]]; then
    "$dotnet_command" run --project tests/NCalc.LegacyOracle/NCalc.LegacyOracle.csproj --configuration Release --framework net48
fi
