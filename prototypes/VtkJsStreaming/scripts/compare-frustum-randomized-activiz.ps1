param(
    [string]$ActiVizBin = 'C:\Program Files\ActiViz.NET 5.8.0 OpenSource Edition\bin',
    [int]$CasesPerType = 12,
    [int]$Seed = 58103,
    [string]$CorpusOutput = ''
)

$ErrorActionPreference = 'Stop'
if ($CasesPerType -lt 1 -or $CasesPerType -gt 100) { throw 'CasesPerType must be between 1 and 100.' }
$ActiVizBin = [IO.Path]::GetFullPath($ActiVizBin)
if (-not (Test-Path -LiteralPath (Join-Path $ActiVizBin 'Kitware.VTK.dll'))) {
    throw "ActiViz 5.8 was not found at '$ActiVizBin'."
}

$env:Path = "$ActiVizBin;$env:Path"
Add-Type -Path (Join-Path $ActiVizBin 'Kitware.mummy.Runtime.dll')
Add-Type -Path (Join-Path $ActiVizBin 'Kitware.VTK.dll')

function New-UnitBoxPlanes {
    $points = [Kitware.VTK.vtkPoints]::New()
    $normals = [Kitware.VTK.vtkDoubleArray]::New()
    [void]$normals.SetNumberOfComponents(3)
    foreach ($definition in @(
        @(-1, 0, 0, -1, 0, 0), @(1, 0, 0, 1, 0, 0),
        @(0, -1, 0, 0, -1, 0), @(0, 1, 0, 0, 1, 0),
        @(0, 0, -1, 0, 0, -1), @(0, 0, 1, 0, 0, 1))) {
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

function New-Rotation([Random]$random) {
    do {
        $qx = $random.NextDouble() * 2 - 1
        $qy = $random.NextDouble() * 2 - 1
        $qz = $random.NextDouble() * 2 - 1
        $qw = $random.NextDouble() * 2 - 1
        $length = [Math]::Sqrt($qx*$qx + $qy*$qy + $qz*$qz + $qw*$qw)
    } while ($length -lt 0.1)
    $qx /= $length; $qy /= $length; $qz /= $length; $qw /= $length
    return [double[]]@(
        (1-2*$qy*$qy-2*$qz*$qz), (2*$qx*$qy-2*$qz*$qw), (2*$qx*$qz+2*$qy*$qw),
        (2*$qx*$qy+2*$qz*$qw), (1-2*$qx*$qx-2*$qz*$qz), (2*$qy*$qz-2*$qx*$qw),
        (2*$qx*$qz-2*$qy*$qw), (2*$qy*$qz+2*$qx*$qw), (1-2*$qx*$qx-2*$qy*$qy))
}

function Convert-Coordinates($template, [Random]$random) {
    $rotation = if ($template.Type -eq 11) {
        [double[]]@(1, 0, 0, 0, 1, 0, 0, 0, 1)
    } else {
        New-Rotation $random
    }
    $scale = @(
        (0.2 + 2.8 * $random.NextDouble()),
        (0.2 + 2.8 * $random.NextDouble()),
        (0.2 + 2.8 * $random.NextDouble()))
    $translation = @(
        ($random.NextDouble() * 4 - 2),
        ($random.NextDouble() * 4 - 2),
        ($random.NextDouble() * 4 - 2))
    $result = [Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $template.Coordinates.Count; $index++) {
        $source = $template.Coordinates[$index]
        $jitter = if ($template.Type -eq 11) { 0 } elseif ($index -lt $template.Corners) { 0.015 } else { 0.045 }
        $scaled = @(
            ([double]$source[0] * $scale[0] + ($random.NextDouble()*2-1)*$jitter),
            ([double]$source[1] * $scale[1] + ($random.NextDouble()*2-1)*$jitter),
            ([double]$source[2] * $scale[2] + ($random.NextDouble()*2-1)*$jitter))
        $result.Add(@(
            ($rotation[0]*$scaled[0] + $rotation[1]*$scaled[1] + $rotation[2]*$scaled[2] + $translation[0]),
            ($rotation[3]*$scaled[0] + $rotation[4]*$scaled[1] + $rotation[5]*$scaled[2] + $translation[1]),
            ($rotation[6]*$scaled[0] + $rotation[7]*$scaled[1] + $rotation[8]*$scaled[2] + $translation[2])))
    }
    return $result.ToArray()
}

$templates = @(
    @{ Name='vertex'; Type=1; Corners=1; Coordinates=@(@(0,0,0)) },
    @{ Name='line'; Type=3; Corners=2; Coordinates=@(@(-0.5,0,0),@(0.5,0,0)) },
    @{ Name='triangle'; Type=5; Corners=3; Coordinates=@(@(0,0,0),@(1,0,0),@(0,1,0)) },
    @{ Name='quad'; Type=9; Corners=4; Coordinates=@(@(0,0,0),@(1,0,0),@(1,1,0),@(0,1,0)) },
    @{ Name='tetra'; Type=10; Corners=4; Coordinates=@(@(0,0,0),@(1,0,0),@(0,1,0),@(0,0,1)) },
    @{ Name='voxel'; Type=11; Corners=8; Coordinates=@(@(0,0,0),@(1,0,0),@(0,1,0),@(1,1,0),@(0,0,1),@(1,0,1),@(0,1,1),@(1,1,1)) },
    @{ Name='hexa'; Type=12; Corners=8; Coordinates=@(@(0,0,0),@(1,0,0),@(1,1,0),@(0,1,0),@(0,0,1),@(1,0,1),@(1,1,1),@(0,1,1)) },
    @{ Name='wedge'; Type=13; Corners=6; Coordinates=@(@(0,0,0),@(1,0,0),@(0,1,0),@(0,0,1),@(1,0,1),@(0,1,1)) },
    @{ Name='pyramid'; Type=14; Corners=5; Coordinates=@(@(0,0,0),@(1,0,0),@(1,1,0),@(0,1,0),@(0.5,0.5,1)) },
    @{ Name='quadratic-edge'; Type=21; Corners=2; Coordinates=@(@(-0.5,0,0),@(0.5,0,0),@(0,0,0)) },
    @{ Name='quadratic-triangle'; Type=22; Corners=3; Coordinates=@(@(0,0,0),@(1,0,0),@(0,1,0),@(0.5,0,0),@(0.5,0.5,0),@(0,0.5,0)) },
    @{ Name='quadratic-quad'; Type=23; Corners=4; Coordinates=@(@(0,0,0),@(1,0,0),@(1,1,0),@(0,1,0),@(0.5,0,0),@(1,0.5,0),@(0.5,1,0),@(0,0.5,0)) },
    @{ Name='quadratic-tetra'; Type=24; Corners=4; Coordinates=@(@(0,0,0),@(1,0,0),@(0,1,0),@(0,0,1),@(0.5,0,0),@(0.5,0.5,0),@(0,0.5,0),@(0,0,0.5),@(0.5,0,0.5),@(0,0.5,0.5)) },
    @{ Name='quadratic-hexa'; Type=25; Corners=8; Coordinates=@(@(0,0,0),@(1,0,0),@(1,1,0),@(0,1,0),@(0,0,1),@(1,0,1),@(1,1,1),@(0,1,1),@(0.5,0,0),@(1,0.5,0),@(0.5,1,0),@(0,0.5,0),@(0.5,0,1),@(1,0.5,1),@(0.5,1,1),@(0,0.5,1),@(0,0,0.5),@(1,0,0.5),@(1,1,0.5),@(0,1,0.5)) },
    @{ Name='quadratic-wedge'; Type=26; Corners=6; Coordinates=@(@(0,0,0),@(1,0,0),@(0,1,0),@(0,0,1),@(1,0,1),@(0,1,1),@(0.5,0,0),@(0.5,0.5,0),@(0,0.5,0),@(0.5,0,1),@(0.5,0.5,1),@(0,0.5,1),@(0,0,0.5),@(1,0,0.5),@(0,1,0.5)) },
    @{ Name='quadratic-pyramid'; Type=27; Corners=5; Coordinates=@(@(0,0,0),@(1,0,0),@(1,1,0),@(0,1,0),@(0.5,0.5,1),@(0.5,0,0),@(1,0.5,0),@(0.5,1,0),@(0,0.5,0),@(0.25,0.25,0.5),@(0.75,0.25,0.5),@(0.75,0.75,0.5),@(0.25,0.75,0.5)) },
    @{ Name='triquadratic-hexa'; Type=29; Corners=8; Coordinates=@(@(0,0,0),@(1,0,0),@(1,1,0),@(0,1,0),@(0,0,1),@(1,0,1),@(1,1,1),@(0,1,1),@(0.5,0,0),@(1,0.5,0),@(0.5,1,0),@(0,0.5,0),@(0.5,0,1),@(1,0.5,1),@(0.5,1,1),@(0,0.5,1),@(0,0,0.5),@(1,0,0.5),@(1,1,0.5),@(0,1,0.5),@(0,0.5,0.5),@(1,0.5,0.5),@(0.5,0,0.5),@(0.5,1,0.5),@(0.5,0.5,0),@(0.5,0.5,1),@(0.5,0.5,0.5)) }
)

$random = [Random]::new($Seed)
$planes = New-UnitBoxPlanes
$captured = [Collections.Generic.List[object]]::new()
foreach ($template in $templates) {
    for ($caseIndex = 0; $caseIndex -lt $CasesPerType; $caseIndex++) {
        $coordinates = Convert-Coordinates $template $random
        $points = [Kitware.VTK.vtkPoints]::New()
        $pointIds = [Kitware.VTK.vtkUnsignedLongArray]::New()
        [void]$pointIds.SetNumberOfValues($coordinates.Count)
        $cellPoints = [Kitware.VTK.vtkIdList]::New()
        $storedCoordinates = [Collections.Generic.List[object]]::new()
        for ($pointIndex = 0; $pointIndex -lt $coordinates.Count; $pointIndex++) {
            $point = $coordinates[$pointIndex]
            [void]$points.InsertNextPoint([double]$point[0], [double]$point[1], [double]$point[2])
            $storedPoint = $points.GetPoint($pointIndex)
            $storedCoordinates.Add(@([double]$storedPoint[0], [double]$storedPoint[1], [double]$storedPoint[2]))
            [void]$pointIds.SetValue($pointIndex, $pointIndex)
            [void]$cellPoints.InsertNextId($pointIndex)
        }
        $grid = [Kitware.VTK.vtkUnstructuredGrid]::New()
        [void]$grid.SetPoints($points)
        [void]$grid.GetPointData().SetGlobalIds($pointIds)
        [void]$grid.InsertNextCell([int]$template.Type, $cellPoints)
        $cellIds = [Kitware.VTK.vtkUnsignedLongArray]::New()
        [void]$cellIds.SetNumberOfValues(1)
        [void]$cellIds.SetValue(0, 501)
        [void]$grid.GetCellData().SetGlobalIds($cellIds)

        $pointOutput = Invoke-Extraction $grid $planes 1
        $selectedPoints = [Collections.Generic.List[int]]::new()
        $outputIds = $pointOutput.GetPointData().GetGlobalIds()
        if ($null -ne $outputIds) {
            for ($index = 0; $index -lt $outputIds.GetNumberOfTuples(); $index++) {
                $selectedPoints.Add([int]$outputIds.GetValue($index))
            }
        }
        $cellOutput = Invoke-Extraction $grid $planes 0
        $captured.Add([ordered]@{
            name = ('{0}-{1:D2}' -f $template.Name, $caseIndex)
            vtkCellType = [int]$template.Type
            points = $storedCoordinates.ToArray()
            legacyPointIndices = $selectedPoints.ToArray()
            legacyCellIntersects = ($cellOutput.GetNumberOfCells() -gt 0)
        })
    }
}

$temporaryCorpus = [string]::IsNullOrWhiteSpace($CorpusOutput)
$corpusPath = if ($temporaryCorpus) {
    [IO.Path]::ChangeExtension([IO.Path]::GetTempFileName(), '.json')
} else {
    [IO.Path]::GetFullPath($CorpusOutput)
}
$oldCorpusPath = $env:VTKJS_FRUSTUM_CORPUS_PATH
try {
    $captured | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $corpusPath -Encoding UTF8
    $env:VTKJS_FRUSTUM_CORPUS_PATH = $corpusPath
    $project = Join-Path $PSScriptRoot '..\tests\PrePoMax.Render.Server.Tests\PrePoMax.Render.Server.Tests.csproj'
    & dotnet test $project -c Release --no-restore --filter 'FullyQualifiedName~FrustumParityCorpusTests' --nologo
    if ($LASTEXITCODE -ne 0) { throw "The production frustum differential exited with code $LASTEXITCODE (seed $Seed)." }
    Write-Host "Randomized ActiViz frustum differential passed: $($captured.Count) cases, $CasesPerType per type, seed $Seed."
}
finally {
    if ($null -eq $oldCorpusPath) { Remove-Item Env:VTKJS_FRUSTUM_CORPUS_PATH -ErrorAction SilentlyContinue }
    else { $env:VTKJS_FRUSTUM_CORPUS_PATH = $oldCorpusPath }
    if ($temporaryCorpus -and (Test-Path -LiteralPath $corpusPath)) {
        Remove-Item -LiteralPath $corpusPath -Force
    }
}
