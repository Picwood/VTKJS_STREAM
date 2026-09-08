param(
    [string]$PrototypeRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'prototypes\VtkJsStreaming')
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = (Resolve-Path -LiteralPath $PrototypeRoot).Path
$projectFiles = @(Get-ChildItem -LiteralPath $resolvedRoot -Recurse -Filter '*.csproj' -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
if ($projectFiles.Count -eq 0) {
    throw "No prototype projects were found below '$resolvedRoot'."
}

$forbiddenProjectReference = '(?i)(Kitware\.VTK|ActiViz|System\.Windows\.Forms|System\.Drawing)'
$projectViolations = foreach ($project in $projectFiles) {
    $matches = Select-String -LiteralPath $project.FullName -Pattern $forbiddenProjectReference
    foreach ($match in $matches) {
        '{0}:{1}: {2}' -f $project.FullName, $match.LineNumber, $match.Line.Trim()
    }
}

$sourceFiles = @(Get-ChildItem -LiteralPath $resolvedRoot -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
$forbiddenNamespace = '(?i)\b(Kitware\.VTK|System\.Windows\.Forms|System\.Drawing)\b'
$sourceViolations = foreach ($source in $sourceFiles) {
    $matches = Select-String -LiteralPath $source.FullName -Pattern $forbiddenNamespace
    foreach ($match in $matches) {
        '{0}:{1}: {2}' -f $source.FullName, $match.LineNumber, $match.Line.Trim()
    }
}

$violations = @($projectViolations) + @($sourceViolations)
if ($violations.Count -gt 0) {
    throw "Forbidden renderer/UI dependencies were found:`n$($violations -join [Environment]::NewLine)"
}

[pscustomobject]@{
    PrototypeRoot = $resolvedRoot
    ProjectsAudited = $projectFiles.Count
    CSharpFilesAudited = $sourceFiles.Count
    ForbiddenReferences = 0
    Result = 'Pass'
}
