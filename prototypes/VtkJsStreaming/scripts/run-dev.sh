#!/usr/bin/env bash
set -euo pipefail

prototype_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PREPOMAX_RENDER_PORT=5174 dotnet run --project "$prototype_root/src/PrePoMax.Render.Server" &
server_pid=$!
trap 'kill "$server_pid" 2>/dev/null || true' EXIT INT TERM

cd "$prototype_root/src/client"
npm run dev
