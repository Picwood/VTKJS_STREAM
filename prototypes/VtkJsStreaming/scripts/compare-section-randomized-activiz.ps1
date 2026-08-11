param(
    [string]$ActiVizBin = 'C:\Program Files\ActiViz.NET 5.8.0 OpenSource Edition\bin',
    [int]$PlanesPerType = 8,
    [int]$WarpedPlanesPerType = 8,
    [int]$Seed = 58027
)

$ErrorActionPreference = 'Stop'
if ($PlanesPerType -lt 1 -or $PlanesPerType -gt 100) { throw 'PlanesPerType must be between 1 and 100.' }
if ($WarpedPlanesPerType -lt 0 -or $WarpedPlanesPerType -gt 100) {
    throw 'WarpedPlanesPerType must be between 0 and 100.'
}
$ActiVizBin = [IO.Path]::GetFullPath($ActiVizBin)
if (-not (Test-Path -LiteralPath (Join-Path $ActiVizBin 'Kitware.VTK.dll'))) {
    throw "ActiViz 5.8 was not found at '$ActiVizBin'."
}

$env:Path = "$ActiVizBin;$env:Path"
Add-Type -Path (Join-Path $ActiVizBin 'Kitware.mummy.Runtime.dll')
Add-Type -Path (Join-Path $ActiVizBin 'Kitware.VTK.dll')

function Test-VertexClose([string]$First, [string]$Second) {
    $firstValues = @($First.Split(',') | ForEach-Object { [long]$_ })
    $secondValues = @($Second.Split(',') | ForEach-Object { [long]$_ })
    if ($firstValues.Count -ne 4 -or $secondValues.Count -ne 4) { return $false }
    for ($component = 0; $component -lt 3; $component++) {
        if ([Math]::Abs($firstValues[$component] - $secondValues[$component]) -gt 2) { return $false }
    }
    return [Math]::Abs($firstValues[3] - $secondValues[3]) -le 2
}

function Test-TriangleClose([string]$First, [string]$Second) {
    $firstVertices = @($First -split '\|')
    $secondVertices = @($Second -split '\|')
    if ($firstVertices.Count -ne 3 -or $secondVertices.Count -ne 3) { return $false }
    $used = [bool[]]::new(3)
    foreach ($firstVertex in $firstVertices) {
        $match = -1
        for ($secondIndex = 0; $secondIndex -lt 3; $secondIndex++) {
            if (-not $used[$secondIndex] -and (Test-VertexClose $firstVertex $secondVertices[$secondIndex])) {
                $match = $secondIndex
                break
            }
        }
        if ($match -lt 0) { return $false }
        $used[$match] = $true
    }
    return $true
}

$tetra = @(
    @(0,0,0), @(1,0,0), @(0,1,0), @(0,0,1),
    @(0.5,0,0), @(0.5,0.5,0), @(0,0.5,0),
    @(0,0,0.5), @(0.5,0,0.5), @(0,0.5,0.5)
)
$quadraticHex = @(
    @(0,0,0), @(1,0,0), @(1,1,0), @(0,1,0), @(0,0,1), @(1,0,1), @(1,1,1), @(0,1,1),
    @(0.5,0,0), @(1,0.5,0), @(0.5,1,0), @(0,0.5,0),
    @(0.5,0,1), @(1,0.5,1), @(0.5,1,1), @(0,0.5,1),
    @(0,0,0.5), @(1,0,0.5), @(1,1,0.5), @(0,1,0.5)
)
$triQuadraticHex = @($quadraticHex) + @(
    @(0,0.5,0.5), @(1,0.5,0.5), @(0.5,0,0.5), @(0.5,1,0.5),
    @(0.5,0.5,0), @(0.5,0.5,1), @(0.5,0.5,0.5)
)
$wedge = @(
    @(0,0,0), @(1,0,0), @(0,1,0), @(0,0,1), @(1,0,1), @(0,1,1),
    @(0.5,0,0), @(0.5,0.5,0), @(0,0.5,0),
    @(0.5,0,1), @(0.5,0.5,1), @(0,0.5,1),
    @(0,0,0.5), @(1,0,0.5), @(0,1,0.5)
)
$pyramid = @(
    @(0,0,0), @(1,0,0), @(1,1,0), @(0,1,0), @(0.5,0.5,1),
    @(0.5,0,0), @(1,0.5,0), @(0.5,1,0), @(0,0.5,0),
    @(0.25,0.25,0.5), @(0.75,0.25,0.5), @(0.75,0.75,0.5), @(0.25,0.75,0.5)
)
$templates = @(
    @{ Prefix = 'tetra24'; Type = 24; Coordinates = $tetra; Center = @(0.2,0.2,0.2); Corners = 4 },
    @{ Prefix = 'hexa25'; Type = 25; Coordinates = $quadraticHex; Center = @(0.5,0.5,0.5); Corners = 8 },
    @{ Prefix = 'wedge26'; Type = 26; Coordinates = $wedge; Center = @(0.3,0.3,0.5); Corners = 6 },
    @{ Prefix = 'pyramid27'; Type = 27; Coordinates = $pyramid; Center = @(0.5,0.5,0.25); Corners = 5 },
    @{ Prefix = 'hexa29'; Type = 29; Coordinates = $triQuadraticHex; Center = @(0.5,0.5,0.5); Corners = 8 }
)

$random = [Random]::new($Seed)
$cases = [Collections.Generic.List[object]]::new()
foreach ($template in $templates) {
    for ($planeIndex = 0; $planeIndex -lt $PlanesPerType; $planeIndex++) {
        do {
            $normal = @(
                ($random.NextDouble() * 2 - 1),
                ($random.NextDouble() * 2 - 1),
                ($random.NextDouble() * 2 - 1))
            $length = [Math]::Sqrt($normal[0] * $normal[0] + $normal[1] * $normal[1] + $normal[2] * $normal[2])
        } while ($length -lt 0.2)
        $normal = @(
            ($normal[0] / $length),
            ($normal[1] / $length),
            ($normal[2] / $length))
        $point = @(
            ([double]$template.Center[0] + ($random.NextDouble() * 2 - 1) * 0.035),
            ([double]$template.Center[1] + ($random.NextDouble() * 2 - 1) * 0.035),
            ([double]$template.Center[2] + ($random.NextDouble() * 2 - 1) * 0.035))
        $cases.Add([ordered]@{
            name = ('{0}-{1:D2}' -f $template.Prefix, $planeIndex)
            vtkCellType = $template.Type
            coordinates = $template.Coordinates
            planePoint = $point
            planeNormal = $normal
        })
    }
    for ($warpedIndex = 0; $warpedIndex -lt $WarpedPlanesPerType; $warpedIndex++) {
        $scaleX = 0.9 + $random.NextDouble() * 0.2
        $scaleY = 0.9 + $random.NextDouble() * 0.2
        $scaleZ = 0.9 + $random.NextDouble() * 0.2
        $shearXY = ($random.NextDouble() * 2 - 1) * 0.12
        $shearXZ = ($random.NextDouble() * 2 - 1) * 0.12
        $shearYX = ($random.NextDouble() * 2 - 1) * 0.12
        $shearYZ = ($random.NextDouble() * 2 - 1) * 0.12
        $shearZX = ($random.NextDouble() * 2 - 1) * 0.12
        $shearZY = ($random.NextDouble() * 2 - 1) * 0.12
        $warped = [Collections.Generic.List[object]]::new()
        for ($node = 0; $node -lt $template.Coordinates.Count; $node++) {
            $source = $template.Coordinates[$node]
            $amplitude = if ($node -lt $template.Corners) { 0.012 } else { 0.04 }
            $x = $scaleX * [double]$source[0] + $shearXY * [double]$source[1] +
                $shearXZ * [double]$source[2] + ($random.NextDouble() * 2 - 1) * $amplitude
            $y = $shearYX * [double]$source[0] + $scaleY * [double]$source[1] +
                $shearYZ * [double]$source[2] + ($random.NextDouble() * 2 - 1) * $amplitude
            $z = $shearZX * [double]$source[0] + $shearZY * [double]$source[1] +
                $scaleZ * [double]$source[2] + ($random.NextDouble() * 2 - 1) * $amplitude
            $warped.Add(@($x, $y, $z))
        }
        $center = @(0.0, 0.0, 0.0)
        for ($corner = 0; $corner -lt $template.Corners; $corner++) {
            for ($component = 0; $component -lt 3; $component++) {
                $center[$component] += [double]$warped[$corner][$component]
            }
        }
        for ($component = 0; $component -lt 3; $component++) {
            $center[$component] /= [double]$template.Corners
        }
        do {
            $normal = @(
                ($random.NextDouble() * 2 - 1),
                ($random.NextDouble() * 2 - 1),
                ($random.NextDouble() * 2 - 1))
            $length = [Math]::Sqrt($normal[0] * $normal[0] + $normal[1] * $normal[1] + $normal[2] * $normal[2])
        } while ($length -lt 0.2)
        $normal = @(
            ($normal[0] / $length),
            ($normal[1] / $length),
            ($normal[2] / $length))
        $point = @(
            ($center[0] + ($random.NextDouble() * 2 - 1) * 0.015),
            ($center[1] + ($random.NextDouble() * 2 - 1) * 0.015),
            ($center[2] + ($random.NextDouble() * 2 - 1) * 0.015))
        $cases.Add([ordered]@{
            name = ('{0}-warped-{1:D2}' -f $template.Prefix, $warpedIndex)
            vtkCellType = $template.Type
            coordinates = $warped.ToArray()
            planePoint = $point
            planeNormal = $normal
        })
    }
}

$inputPath = [IO.Path]::GetTempFileName()
$serverPath = [IO.Path]::GetTempFileName()
try {
    $cases | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $inputPath -Encoding UTF8
    $project = Join-Path $PSScriptRoot '..\tools\PrePoMax.Render.SectionOracle\PrePoMax.Render.SectionOracle.csproj'
    & dotnet run --project $project -c Release --no-build -- $inputPath $serverPath
    if ($LASTEXITCODE -ne 0) { throw "The production section oracle exited with code $LASTEXITCODE." }
    $serverResults = Get-Content -LiteralPath $serverPath -Raw | ConvertFrom-Json
    $serverByName = @{}
    foreach ($result in $serverResults) { $serverByName[$result.name] = @($result.triangles) }

    $totalTriangles = 0
    foreach ($item in $cases) {
        $points = [Kitware.VTK.vtkPoints]::New()
        $scalars = [Kitware.VTK.vtkDoubleArray]::New()
        [void]$scalars.SetName('value')
        [void]$scalars.SetNumberOfComponents(1)
        $pointIds = [Kitware.VTK.vtkIdList]::New()
        for ($index = 0; $index -lt $item.coordinates.Count; $index++) {
            $point = $item.coordinates[$index]
            [void]$points.InsertNextPoint([double]$point[0], [double]$point[1], [double]$point[2])
            [void]$scalars.InsertNextValue([double]$point[0] + 2.0 * [double]$point[1] + 3.0 * [double]$point[2])
            [void]$pointIds.InsertNextId($index)
        }
        $grid = [Kitware.VTK.vtkUnstructuredGrid]::New()
        [void]$grid.SetPoints($points)
        [void]$grid.GetPointData().SetScalars($scalars)
        [void]$grid.InsertNextCell([int]$item.vtkCellType, $pointIds)
        $plane = [Kitware.VTK.vtkPlane]::New()
        [void]$plane.SetOrigin([double]$item.planePoint[0], [double]$item.planePoint[1], [double]$item.planePoint[2])
        [void]$plane.SetNormal([double]$item.planeNormal[0], [double]$item.planeNormal[1], [double]$item.planeNormal[2])
        $cutter = [Kitware.VTK.vtkCutter]::New()
        [void]$cutter.SetCutFunction($plane)
        [void]$cutter.SetInput(0, $grid)
        [void]$cutter.Update()
        $output = $cutter.GetOutput()
        $outputScalars = $output.GetPointData().GetScalars()
        $triangles = [Collections.Generic.List[string]]::new()
        for ($cellIndex = 0; $cellIndex -lt $output.GetNumberOfCells(); $cellIndex++) {
            $cell = $output.GetCell($cellIndex)
            if ($cell.GetNumberOfPoints() -ne 3) {
                throw "ActiViz case '$($item.name)' emitted a non-triangle cell."
            }
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
            $triangles.Add([string]::Join('|', $vertices))
        }
        $actual = $triangles.ToArray()
        [Array]::Sort($actual, [StringComparer]::Ordinal)
        $expected = $serverByName[$item.name]
        if ($null -eq $expected) {
            throw "The production result for '$($item.name)' is missing; available: $($serverByName.Keys -join ',')."
        }
        if ($actual.Count -ne $expected.Count) {
            throw "Case '$($item.name)' triangle count differs: ActiViz=$($actual.Count), production=$($expected.Count)."
        }
        $remaining = [Collections.Generic.List[string]]::new()
        foreach ($triangle in $expected) { $remaining.Add([string]$triangle) }
        foreach ($actualTriangle in $actual) {
            $match = -1
            for ($expectedIndex = 0; $expectedIndex -lt $remaining.Count; $expectedIndex++) {
                if (Test-TriangleClose $actualTriangle $remaining[$expectedIndex]) {
                    $match = $expectedIndex
                    break
                }
            }
            if ($match -lt 0) {
                throw "Case '$($item.name)' has no production match for ActiViz triangle '$actualTriangle'."
            }
            $remaining.RemoveAt($match)
        }
        if ($remaining.Count -ne 0) {
            throw "Case '$($item.name)' has $($remaining.Count) unmatched production triangles."
        }
        $totalTriangles += $actual.Count
    }
    Write-Host "Randomized ActiViz differential passed: $($cases.Count) cases, $PlanesPerType canonical and $WarpedPlanesPerType warped planes per type, $totalTriangles triangles, seed $Seed."
}
finally {
    if (Test-Path -LiteralPath $inputPath) { Remove-Item -LiteralPath $inputPath -Force }
    if (Test-Path -LiteralPath $serverPath) { Remove-Item -LiteralPath $serverPath -Force }
}
