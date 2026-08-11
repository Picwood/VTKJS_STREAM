$ErrorActionPreference = 'Stop'
$prototypeRoot = Split-Path -Parent $PSScriptRoot
$serverJob = Start-Job -ScriptBlock {
    param($root)
    Set-Location $root
    $env:PREPOMAX_RENDER_PORT = '5174'
    dotnet run --project .\src\PrePoMax.Render.Server
} -ArgumentList $prototypeRoot

try {
    Set-Location (Join-Path $prototypeRoot 'src/client')
    npm run dev
}
finally {
    Stop-Job -Job $serverJob -ErrorAction SilentlyContinue
    Receive-Job -Job $serverJob -ErrorAction SilentlyContinue
    Remove-Job -Job $serverJob -Force -ErrorAction SilentlyContinue
}
