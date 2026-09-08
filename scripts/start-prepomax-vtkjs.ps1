param(
    [string]$ModelPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$executable = Resolve-Path (Join-Path $repositoryRoot 'PrePoMax\bin\x64\Release\PrePoMax.exe')
$argumentList = $null
if (-not [string]::IsNullOrWhiteSpace($ModelPath)) {
    $resolvedModel = Resolve-Path -LiteralPath $ModelPath
    $argumentList = '"' + $resolvedModel.Path.Replace('"', '\"') + '"'
}

$previousRenderer = [Environment]::GetEnvironmentVariable('PREPOMAX_RENDERER', 'Process')
try {
    $env:PREPOMAX_RENDERER = 'vtkjs'
    $parameters = @{
        FilePath = $executable.Path
        WorkingDirectory = Split-Path -Parent $executable.Path
        PassThru = $true
    }
    if ($null -ne $argumentList) { $parameters.ArgumentList = $argumentList }
    $process = Start-Process @parameters
    [pscustomobject]@{
        ProcessId = $process.Id
        Renderer = 'vtkjs'
        VisibleViewportUsesActiViz = $false
        ActiVizBinariesStillRequired = $true
    }
}
finally {
    if ($null -eq $previousRenderer) {
        Remove-Item Env:PREPOMAX_RENDERER -ErrorAction SilentlyContinue
    }
    else {
        $env:PREPOMAX_RENDERER = $previousRenderer
    }
}
