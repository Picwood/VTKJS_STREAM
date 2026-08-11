param(
    [string]$SourcePath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'examples\rotor.inp'),
    [string]$OutputDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) ('prepomax-rotor-harmonic-' + [Guid]::NewGuid().ToString('N'))),
    [string]$SolverPath
)

$ErrorActionPreference = 'Stop'
$source = Resolve-Path $SourcePath
$sourceText = [System.IO.File]::ReadAllText($source.Path)
if ($sourceText -notmatch '(?im)^\*frequency[^\r\n]*storage\s*=\s*yes') {
    throw 'The rotor source must retain a frequency step with STORAGE=YES.'
}
if ($sourceText -notmatch '(?im)^\*complex frequency[^\r\n]*coriolis') {
    throw 'The rotor source must retain its Coriolis complex-frequency step.'
}

if ([string]::IsNullOrWhiteSpace($SolverPath)) {
    $solver = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'PrePoMax*') -Recurse -Filter 'ccx_dynamic.exe' `
        -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if ($null -eq $solver) { throw 'An installed PrePoMax ccx_dynamic.exe was not found.' }
    $SolverPath = $solver.FullName
}
else { $SolverPath = (Resolve-Path $SolverPath).Path }

if (Test-Path -LiteralPath $OutputDirectory) {
    throw "The output directory already exists: $OutputDirectory"
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$jobName = 'rotor-harmonic'
$inputPath = Join-Path $OutputDirectory "$jobName.inp"
$acceptanceStep = @'

** VTK.js harmonic acceptance response derived from the rotor eigenbasis.
*step,perturbation
*steady state dynamics
100.,200.,3,1.
*cload
1,1,1.
*cload,load case=2
1,2,1.
*node file
U
*end step
'@
[System.IO.File]::WriteAllText($inputPath, $sourceText.TrimEnd() + $acceptanceStep)

Push-Location $OutputDirectory
try {
    & $SolverPath -i $jobName
    if ($LASTEXITCODE -ne 0) { throw "CalculiX failed with exit code $LASTEXITCODE." }
}
finally { Pop-Location }

$frdPath = Join-Path $OutputDirectory "$jobName.frd"
if (-not (Test-Path -LiteralPath $frdPath)) { throw 'CalculiX did not create the harmonic FRD fixture.' }
[pscustomobject]@{
    InputPath = $inputPath
    FrdPath = $frdPath
    SolverPath = $SolverPath
}
