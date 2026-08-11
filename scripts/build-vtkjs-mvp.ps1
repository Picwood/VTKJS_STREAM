param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$prototypeRoot = Join-Path $repositoryRoot 'prototypes\VtkJsStreaming'
$clientRoot = Join-Path $prototypeRoot 'src\client'
$prePoMaxOutput = Join-Path $repositoryRoot "PrePoMax\bin\x64\$Configuration"
$rendererOutput = Join-Path $prePoMaxOutput 'VtkJsRenderer'
$serverOutput = Join-Path $rendererOutput 'server'
$clientOutput = Join-Path $rendererOutput 'client'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer discovery tool was not found.'
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw 'A Visual Studio installation containing MSBuild was not found.'
}

Push-Location $clientRoot
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'The VTK.js client build failed.' }
}
finally {
    Pop-Location
}

dotnet publish (Join-Path $prototypeRoot 'src\PrePoMax.Render.Server\PrePoMax.Render.Server.csproj') `
    -c Release --no-restore -o $serverOutput
if ($LASTEXITCODE -ne 0) { throw 'The renderer server publish failed.' }

New-Item -ItemType Directory -Force -Path $clientOutput | Out-Null
Copy-Item -Path (Join-Path $clientRoot 'dist\*') -Destination $clientOutput -Recurse -Force

& $msbuild (Join-Path $repositoryRoot 'PrePoMax.sln') /t:Restore,Build `
    /p:Configuration=$Configuration /p:Platform=x64 /m /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'The PrePoMax solution build failed.' }

Write-Host "VTK.js MVP staged at $rendererOutput"
