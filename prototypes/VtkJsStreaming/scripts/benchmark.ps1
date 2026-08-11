param(
    [switch]$Headless,
    [ValidateRange(1, 1499)][int]$ChunkCellRows = 50,
    [ValidateRange(1048576, 134217728)][long]$ResourceCapBytes = 8388608
)

$ErrorActionPreference = 'Stop'
$prototypeRoot = Split-Path -Parent $PSScriptRoot
$clientRoot = Join-Path $prototypeRoot 'src/client'
$env:PREPOMAX_RENDER_CHUNK_CELL_ROWS = $ChunkCellRows.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:PREPOMAX_RENDER_RESOURCE_CAP_BYTES = $ResourceCapBytes.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:PREPOMAX_BENCHMARK_HEADLESS = if ($Headless) { '1' } else { '0' }

Push-Location $prototypeRoot
try {
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    Push-Location $clientRoot
    try {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
        npx playwright test --config ../../tests/benchmark/playwright.config.mts
        if ($LASTEXITCODE -ne 0) { throw 'WP5 benchmark failed.' }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
