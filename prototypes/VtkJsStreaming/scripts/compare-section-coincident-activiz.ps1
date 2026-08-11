param(
    [string]$ActiVizBin = 'C:\Program Files\ActiViz.NET 5.8.0 OpenSource Edition\bin'
)

$ErrorActionPreference = 'Stop'
$ActiVizBin = [IO.Path]::GetFullPath($ActiVizBin)
if (-not (Test-Path -LiteralPath (Join-Path $ActiVizBin 'Kitware.VTK.dll'))) {
    throw "ActiViz 5.8 was not found at '$ActiVizBin'."
}
$env:Path = "$ActiVizBin;$env:Path"
Add-Type -Path (Join-Path $ActiVizBin 'Kitware.mummy.Runtime.dll')
Add-Type -Path (Join-Path $ActiVizBin 'Kitware.VTK.dll')

function Add-CellPoints {
    param(
        [Collections.Generic.List[object]]$Points,
        [Collections.Generic.Dictionary[string,int]]$PointIndices,
        [object[]]$CellPoints,
        [switch]$KeepDuplicates
    )
    $indices = [Collections.Generic.List[int]]::new()
    foreach ($point in $CellPoints) {
        $key = [string]::Join(',', @($point | ForEach-Object {
            ([double]$_).ToString('R', [Globalization.CultureInfo]::InvariantCulture)
        }))
        $existing = 0
        if (-not $KeepDuplicates -and $PointIndices.TryGetValue($key, [ref]$existing)) {
            $indices.Add($existing)
            continue
        }
        $index = $Points.Count
        $Points.Add(@([double]$point[0], [double]$point[1], [double]$point[2]))
        if (-not $KeepDuplicates) { $PointIndices[$key] = $index }
        $indices.Add($index)
    }
    return $indices.ToArray()
}

function New-AdjacentCase {
    param(
        [string]$Name,
        [int]$VtkCellType,
        [object[]]$FirstPoints,
        [object[]]$SecondPoints,
        [double[]]$PlanePoint,
        [double[]]$PlaneNormal,
        [switch]$KeepDuplicates
    )
    $points = [Collections.Generic.List[object]]::new()
    $pointIndices = [Collections.Generic.Dictionary[string,int]]::new([StringComparer]::Ordinal)
    $first = Add-CellPoints $points $pointIndices $FirstPoints -KeepDuplicates:$KeepDuplicates
    $second = Add-CellPoints $points $pointIndices $SecondPoints -KeepDuplicates:$KeepDuplicates
    return [ordered]@{
        name = $Name
        vtkCellType = $VtkCellType
        coordinates = $points.ToArray()
        planePoint = $PlanePoint
        planeNormal = $PlaneNormal
        cells = @(
            [ordered]@{ sourceElementId = 101; vtkCellType = $VtkCellType; localPointIndices = $first },
            [ordered]@{ sourceElementId = 102; vtkCellType = $VtkCellType; localPointIndices = $second })
    }
}

$hexa = @(
    @(0,0,0), @(1,0,0), @(1,1,0), @(0,1,0),
    @(0,0,1), @(1,0,1), @(1,1,1), @(0,1,1))
$quadraticHex = @($hexa) + @(
    @(0.5,0,0), @(1,0.5,0), @(0.5,1,0), @(0,0.5,0),
    @(0.5,0,1), @(1,0.5,1), @(0.5,1,1), @(0,0.5,1),
    @(0,0,0.5), @(1,0,0.5), @(1,1,0.5), @(0,1,0.5))
$triQuadraticHex = @($quadraticHex) + @(
    @(0,0.5,0.5), @(1,0.5,0.5), @(0.5,0,0.5), @(0.5,1,0.5),
    @(0.5,0.5,0), @(0.5,0.5,1), @(0.5,0.5,0.5))
$tetra = @(@(0,0,0), @(1,0,0), @(0,1,0), @(0,0,1))
$quadraticTetra = @($tetra) + @(
    @(0.5,0,0), @(0.5,0.5,0), @(0,0.5,0),
    @(0,0,0.5), @(0.5,0,0.5), @(0,0.5,0.5))
$wedge = @(
    @(0,0,0), @(1,0,0), @(0,1,0), @(0,0,1), @(1,0,1), @(0,1,1),
    @(0.5,0,0), @(0.5,0.5,0), @(0,0.5,0),
    @(0.5,0,1), @(0.5,0.5,1), @(0,0.5,1),
    @(0,0,0.5), @(1,0,0.5), @(0,1,0.5))
$wedgeMirror = @(
    @(0,0,0), @(0,1,0), @(-1,0,0), @(0,0,1), @(0,1,1), @(-1,0,1),
    @(0,0.5,0), @(-0.5,0.5,0), @(-0.5,0,0),
    @(0,0.5,1), @(-0.5,0.5,1), @(-0.5,0,1),
    @(0,0,0.5), @(0,1,0.5), @(-1,0,0.5))
$pyramid = @(
    @(0,0,0), @(1,0,0), @(1,1,0), @(0,1,0), @(0.5,0.5,1),
    @(0.5,0,0), @(1,0.5,0), @(0.5,1,0), @(0,0.5,0),
    @(0.25,0.25,0.5), @(0.75,0.25,0.5), @(0.75,0.75,0.5), @(0.25,0.75,0.5))
$pyramidMirror = @(
    @(0,0,0), @(1,0,0), @(1,1,0), @(0,1,0), @(0.5,0.5,-1),
    @(0.5,0,0), @(1,0.5,0), @(0.5,1,0), @(0,0.5,0),
    @(0.25,0.25,-0.5), @(0.75,0.25,-0.5), @(0.75,0.75,-0.5), @(0.25,0.75,-0.5))
$voxel = @($hexa[0], $hexa[1], $hexa[3], $hexa[2], $hexa[4], $hexa[5], $hexa[7], $hexa[6])
$linearWedge = @($wedge[0..5])
$linearWedgeMirror = @($wedgeMirror[0..5])
$linearPyramid = @($pyramid[0..4])
$linearPyramidMirror = @($pyramidMirror[0..4])

function Shift-X([object[]]$points, [double]$offset) {
    $shifted = [Collections.Generic.List[object]]::new()
    foreach ($point in $points) {
        $shifted.Add(@(([double]$point[0] + $offset), ([double]$point[1]), ([double]$point[2])))
    }
    Write-Output -NoEnumerate $shifted.ToArray()
}

$tetraMirror = @(@(0,0,0), @(0,1,0), @(1,0,0), @(0,0,-1))
$quadraticTetraMirror = @($tetraMirror) + @(
    @(0,0.5,0), @(0.5,0.5,0), @(0.5,0,0),
    @(0,0,-0.5), @(0,0.5,-0.5), @(0.5,0,-0.5))

$cases = @(
    (New-AdjacentCase 'tetra10-shared-face' 10 $tetra $tetraMirror @(0,0,0) @(0,0,1)),
    (New-AdjacentCase 'voxel11-shared-face' 11 $voxel (Shift-X $voxel 1) @(1,0,0) @(1,0,0)),
    (New-AdjacentCase 'hexa12-shared-face' 12 $hexa (Shift-X $hexa 1) @(1,0,0) @(1,0,0)),
    (New-AdjacentCase 'hexa12-shared-nodes' 12 $hexa (Shift-X $hexa 1) @(0,0,0) @(0,1,-1)),
    (New-AdjacentCase 'hexa12-duplicate-interface' 12 $hexa (Shift-X $hexa 1) @(0,0.5,0) @(0,1,0) -KeepDuplicates),
    (New-AdjacentCase 'wedge13-shared-face' 13 $linearWedge $linearWedgeMirror @(0,0,0) @(1,0,0)),
    (New-AdjacentCase 'pyramid14-shared-base' 14 $linearPyramid $linearPyramidMirror @(0,0,0) @(0,0,1)),
    (New-AdjacentCase 'tetra24-shared-face' 24 $quadraticTetra $quadraticTetraMirror @(0,0,0) @(0,0,1)),
    (New-AdjacentCase 'hexa25-shared-face' 25 $quadraticHex (Shift-X $quadraticHex 1) @(1,0,0) @(1,0,0)),
    (New-AdjacentCase 'wedge26-shared-face' 26 $wedge $wedgeMirror @(0,0,0) @(1,0,0)),
    (New-AdjacentCase 'pyramid27-shared-base' 27 $pyramid $pyramidMirror @(0,0,0) @(0,0,1)),
    (New-AdjacentCase 'hexa29-shared-face' 29 $triQuadraticHex (Shift-X $triQuadraticHex 1) @(1,0,0) @(1,0,0)))

$inputPath = [IO.Path]::GetTempFileName()
$serverPath = [IO.Path]::GetTempFileName()
try {
    $cases | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $inputPath -Encoding UTF8
    $project = Join-Path $PSScriptRoot '..\tools\PrePoMax.Render.SectionOracle\PrePoMax.Render.SectionOracle.csproj'
    & dotnet run --project $project -c Release -- $inputPath $serverPath
    if ($LASTEXITCODE -ne 0) { throw "The production section oracle exited with code $LASTEXITCODE." }
    $serverResults = Get-Content -LiteralPath $serverPath -Raw | ConvertFrom-Json
    $serverByName = @{}
    foreach ($result in $serverResults) { $serverByName[$result.name] = $result }

    $totalTriangles = 0
    foreach ($item in $cases) {
        $points = [Kitware.VTK.vtkPoints]::New()
        $scalars = [Kitware.VTK.vtkDoubleArray]::New()
        [void]$scalars.SetName('value')
        [void]$scalars.SetNumberOfComponents(1)
        foreach ($point in $item.coordinates) {
            [void]$points.InsertNextPoint([double]$point[0], [double]$point[1], [double]$point[2])
            [void]$scalars.InsertNextValue([double]$point[0] + 2 * [double]$point[1] + 3 * [double]$point[2])
        }
        $grid = [Kitware.VTK.vtkUnstructuredGrid]::New()
        [void]$grid.SetPoints($points)
        [void]$grid.GetPointData().SetScalars($scalars)
        foreach ($cellData in $item.cells) {
            $pointIds = [Kitware.VTK.vtkIdList]::New()
            foreach ($pointIndex in $cellData.localPointIndices) { [void]$pointIds.InsertNextId([long]$pointIndex) }
            [void]$grid.InsertNextCell([int]$cellData.vtkCellType, $pointIds)
        }
        $plane = [Kitware.VTK.vtkPlane]::New()
        [void]$plane.SetOrigin([double]$item.planePoint[0], [double]$item.planePoint[1], [double]$item.planePoint[2])
        [void]$plane.SetNormal([double]$item.planeNormal[0], [double]$item.planeNormal[1], [double]$item.planeNormal[2])
        $cutter = [Kitware.VTK.vtkCutter]::New()
        [void]$cutter.SetCutFunction($plane)
        [void]$cutter.SetInput(0, $grid)
        [void]$cutter.Update()
        $output = $cutter.GetOutput()
        $actual = [Collections.Generic.List[string]]::new()
        $actualPoints = [Collections.Generic.List[string]]::new()
        $outputScalars = $output.GetPointData().GetScalars()
        for ($pointIndex = 0; $pointIndex -lt $output.GetNumberOfPoints(); $pointIndex++) {
            $position = $output.GetPoint($pointIndex)
            $actualPoints.Add([string]::Join(',', @(
                [long][Math]::Round([double]$position[0] * 100000),
                [long][Math]::Round([double]$position[1] * 100000),
                [long][Math]::Round([double]$position[2] * 100000),
                [long][Math]::Round([double]$outputScalars.GetTuple1($pointIndex) * 10000))))
        }
        for ($cellIndex = 0; $cellIndex -lt $output.GetNumberOfCells(); $cellIndex++) {
            $cell = $output.GetCell($cellIndex)
            if ($cell.GetNumberOfPoints() -ne 3) { throw "ActiViz case '$($item.name)' emitted a non-triangle cell." }
            $vertices = [string[]]::new(3)
            for ($vertex = 0; $vertex -lt 3; $vertex++) {
                $pointId = $cell.GetPointId($vertex)
                $position = $output.GetPoint($pointId)
                $vertices[$vertex] = [string]::Join(',', @(
                    [long][Math]::Round([double]$position[0] * 100000),
                    [long][Math]::Round([double]$position[1] * 100000),
                    [long][Math]::Round([double]$position[2] * 100000),
                    [long][Math]::Round([double]$outputScalars.GetTuple1($pointId) * 10000)))
            }
            [Array]::Sort($vertices, [StringComparer]::Ordinal)
            $actual.Add([string]::Join('|', $vertices))
        }
        $actualTriangles = @($actual | Sort-Object)
        $expected = $serverByName[$item.name]
        $expectedTriangles = @($expected.triangles)
        if ($output.GetNumberOfPoints() -ne [int]$expected.pointCount) {
            throw "Case '$($item.name)' point count differs: ActiViz=$($output.GetNumberOfPoints()) [$(@($actualPoints | Sort-Object) -join '; ')], production=$($expected.pointCount) [$(@($expected.points) -join '; ')]."
        }
        if ($actualTriangles.Count -ne $expectedTriangles.Count) {
            throw "Case '$($item.name)' triangle count differs: ActiViz=$($actualTriangles.Count), production=$($expectedTriangles.Count)."
        }
        for ($triangle = 0; $triangle -lt $actualTriangles.Count; $triangle++) {
            if ($actualTriangles[$triangle] -ne $expectedTriangles[$triangle]) {
                throw "Case '$($item.name)' triangle $triangle differs: ActiViz='$($actualTriangles[$triangle])', production='$($expectedTriangles[$triangle])'."
            }
        }
        $totalTriangles += $actualTriangles.Count
        Write-Host "Passed $($item.name): $($output.GetNumberOfPoints()) points, $($actualTriangles.Count) triangles."
    }
    Write-Host "Coincident multi-cell ActiViz differential passed: $($cases.Count) cases, $totalTriangles triangles."
}
finally {
    if (Test-Path -LiteralPath $inputPath) { Remove-Item -LiteralPath $inputPath -Force }
    if (Test-Path -LiteralPath $serverPath) { Remove-Item -LiteralPath $serverPath -Force }
}
