#!/usr/bin/env bash
set -euo pipefail

prototype_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export PREPOMAX_RENDER_CHUNK_CELL_ROWS="${PREPOMAX_RENDER_CHUNK_CELL_ROWS:-50}"
export PREPOMAX_RENDER_RESOURCE_CAP_BYTES="${PREPOMAX_RENDER_RESOURCE_CAP_BYTES:-8388608}"
export PREPOMAX_BENCHMARK_HEADLESS="${PREPOMAX_BENCHMARK_HEADLESS:-0}"

cd "$prototype_root"
dotnet build -c Release
cd src/client
npm ci
npx playwright test --config ../../tests/benchmark/playwright.config.mts
