param(
    [string]$ActiVizBin = 'C:\Program Files\ActiViz.NET 5.8.0 OpenSource Edition\bin',
    [string]$CorpusPath = (Join-Path $PSScriptRoot '..\tests\PrePoMax.Render.Server.Tests\Fixtures\frustum-parity-corpus.json')
)

$ErrorActionPreference = 'Stop'
$ActiVizBin = [IO.Path]::GetFullPath($ActiVizBin)
$CorpusPath = [IO.Path]::GetFullPath($CorpusPath)
if (-not (Test-Path -LiteralPath (Join-Path $ActiVizBin 'Kitware.VTK.dll'))) {
    throw "ActiViz 5.8 was not found at '$ActiVizBin'."
}
if (-not (Test-Path -LiteralPath $CorpusPath)) { throw "Corpus not found at '$CorpusPath'." }

$env:Path = "$ActiVizBin;$env:Path"
Add-Type -Path (Join-Path $ActiVizBin 'Kitware.mummy.Runtime.dll')
Add-Type -Path (Join-Path $ActiVizBin 'Kitware.VTK.dll')

function New-UnitBoxPlanes {
    $points = [Kitware.VTK.vtkPoints]::New()
    $normals = [Kitware.VTK.vtkDoubleArray]::New()
    [void]$normals.SetNumberOfComponents(3)
    $definitions = @(
        @(-1, 0, 0, -1, 0, 0), @(1, 0, 0, 1, 0, 0),
        @(0, -1, 0, 0, -1, 0), @(0, 1, 0, 0, 1, 0),
        @(0, 0, -1, 0, 0, -1), @(0, 0, 1, 0, 0, 1)
    )
    foreach ($definition in $definitions) {
        [void]$points.InsertNextPoint([double]$definition[0], [double]$definition[1], [double]$definition[2])
        [void]$normals.InsertNextTuple3([double]$definition[3], [double]$definition[4], [double]$definition[5])
    }
    $planes = [Kitware.VTK.vtkPlanes]::New()
    [void]$planes.SetPoints($points)
    [void]$planes.SetNormals($normals)
    return $planes
}

function Invoke-Extraction($grid, $planes, [int]$fieldType) {
    $extractor = [Kitware.VTK.vtkExtractSelectedFrustum]::New()
    [void]$extractor.SetFieldType($fieldType)
    [void]$extractor.SetInput(0, $grid)
    [void]$extractor.SetFrustum($planes)
    [void]$extractor.Update()
    return $extractor.GetOutput()
}

$failures = 0
$cases = Get-Content -LiteralPath $CorpusPath -Raw | ConvertFrom-Json
foreach ($case in $cases) {
    $points = [Kitware.VTK.vtkPoints]::New()
    foreach ($point in @($case.points)) {
        [void]$points.InsertNextPoint([double]$point[0], [double]$point[1], [double]$point[2])
    }
    $grid = [Kitware.VTK.vtkUnstructuredGrid]::New()
    [void]$grid.SetPoints($points)
    $pointIds = [Kitware.VTK.vtkUnsignedLongArray]::New()
    [void]$pointIds.SetNumberOfValues($points.GetNumberOfPoints())
    $cellPoints = [Kitware.VTK.vtkIdList]::New()
    for ($index = 0; $index -lt $points.GetNumberOfPoints(); $index++) {
        [void]$pointIds.SetValue($index, $index)
        [void]$cellPoints.InsertNextId($index)
    }
    [void]$grid.GetPointData().SetGlobalIds($pointIds)
    [void]$grid.InsertNextCell([int]$case.vtkCellType, $cellPoints)
    $cellIds = [Kitware.VTK.vtkUnsignedLongArray]::New()
    [void]$cellIds.SetNumberOfValues(1)
    [void]$cellIds.SetValue(0, 501)
    [void]$grid.GetCellData().SetGlobalIds($cellIds)

    $planes = New-UnitBoxPlanes
    $pointOutput = Invoke-Extraction $grid $planes 1
    $actualPoints = @()
    $outputPointIds = $pointOutput.GetPointData().GetGlobalIds()
    if ($outputPointIds -ne $null) {
        for ($index = 0; $index -lt $outputPointIds.GetNumberOfTuples(); $index++) {
            $actualPoints += [int]$outputPointIds.GetValue($index)
        }
    }
    $cellOutput = Invoke-Extraction $grid $planes 0
    $actualCell = $cellOutput.GetNumberOfCells() -gt 0
    $expectedPoints = @($case.legacyPointIndices | ForEach-Object { [int]$_ })
    $pointsMatch = (($actualPoints -join ',') -eq ($expectedPoints -join ','))
    $cellMatch = $actualCell -eq [bool]$case.legacyCellIntersects
    if (-not ($pointsMatch -and $cellMatch)) {
        $failures++
        Write-Error -ErrorAction Continue ("{0}: expected points [{1}], cell={2}; ActiViz returned points [{3}], cell={4}" -f
            $case.name, ($expectedPoints -join ','), $case.legacyCellIntersects, ($actualPoints -join ','), $actualCell)
    }
    else { Write-Host "PASS $($case.name)" }
}

if ($failures -ne 0) { throw "$failures frustum parity corpus case(s) differ from ActiViz 5.8." }
Write-Host "ActiViz 5.8 frustum parity corpus passed: $($cases.Count) cases."
