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

$coordinates = @(
    @(0,0,0), @(1,0,0), @(1,1,0), @(0,1,0), @(0,0,1), @(1,0,1), @(1,1,1), @(0,1,1),
    @(0.5,0,0), @(1,0.5,0), @(0.5,1,0), @(0,0.5,0),
    @(0.5,0,1), @(1,0.5,1), @(0.5,1,1), @(0,0.5,1),
    @(0,0,0.5), @(1,0,0.5), @(1,1,0.5), @(0,1,0.5),
    @(0,0.5,0.5), @(1,0.5,0.5), @(0.5,0,0.5), @(0.5,1,0.5),
    @(0.5,0.5,0), @(0.5,0.5,1), @(0.5,0.5,0.5)
)
$points = [Kitware.VTK.vtkPoints]::New()
$scalars = [Kitware.VTK.vtkDoubleArray]::New()
[void]$scalars.SetName('value')
[void]$scalars.SetNumberOfComponents(1)
$pointIds = [Kitware.VTK.vtkIdList]::New()
for ($index = 0; $index -lt $coordinates.Count; $index++) {
    $point = $coordinates[$index]
    [void]$points.InsertNextPoint([double]$point[0], [double]$point[1], [double]$point[2])
    [void]$scalars.InsertNextValue([double]$point[1] + 2.0 * [double]$point[2])
    [void]$pointIds.InsertNextId($index)
}

$grid = [Kitware.VTK.vtkUnstructuredGrid]::New()
[void]$grid.SetPoints($points)
[void]$grid.GetPointData().SetScalars($scalars)
[void]$grid.InsertNextCell(29, $pointIds) # VTK_TRIQUADRATIC_HEXAHEDRON
$plane = [Kitware.VTK.vtkPlane]::New()
[void]$plane.SetOrigin(0.25, 0.0, 0.0)
[void]$plane.SetNormal(1.0, 0.0, 0.0)
$cutter = [Kitware.VTK.vtkCutter]::New()
[void]$cutter.SetCutFunction($plane)
[void]$cutter.SetInput(0, $grid)
[void]$cutter.Update()
$output = $cutter.GetOutput()

if ($output.GetNumberOfPoints() -ne 9 -or $output.GetNumberOfPolys() -ne 8) {
    throw "Expected 9 points and 8 triangles; got $($output.GetNumberOfPoints()) points and $($output.GetNumberOfPolys()) triangles."
}
$outputScalars = $output.GetPointData().GetScalars()
$coordinateKeys = @()
for ($index = 0; $index -lt $output.GetNumberOfPoints(); $index++) {
    $point = $output.GetPoint($index)
    if ([Math]::Abs($point[0] - 0.25) -gt 1e-12) { throw "Point $index is outside the cut plane." }
    if ([Math]::Abs($outputScalars.GetTuple1($index) - ($point[1] + 2.0 * $point[2])) -gt 1e-12) {
        throw "Point $index did not preserve scalar interpolation."
    }
    $coordinateKeys += [int][Math]::Round($point[1] * 4.0) * 10 + [int][Math]::Round($point[2] * 4.0)
}
if (($coordinateKeys | Sort-Object) -join ',' -ne '0,2,4,20,22,24,40,42,44') {
    throw "ActiViz returned unexpected in-plane coordinates: $(($coordinateKeys | Sort-Object) -join ',')."
}

# Force the first legacy linear sub-hexahedron to ambiguous marching-cubes case 90.
# The remaining nodes stay below the x=0 plane; this is intentionally warped and
# differentiates the VTK edge table from angle-ordering all intersections into a fan.
$firstSubcell = @(0, 8, 24, 11, 16, 22, 26, 20)
$positiveVertices = @(1, 3, 4, 6)
for ($index = 0; $index -lt $coordinates.Count; $index++) {
    $point = $coordinates[$index]
    [void]$points.SetPoint($index, -1.0, [double]$point[1] + $index * 0.007,
        [double]$point[2] + $index * $index * 0.0003)
}
for ($vertex = 0; $vertex -lt $firstSubcell.Count; $vertex++) {
    if ($positiveVertices -contains $vertex) {
        $index = $firstSubcell[$vertex]
        $point = $coordinates[$index]
        [void]$points.SetPoint($index, 1.0, [double]$point[1] + $index * 0.007,
            [double]$point[2] + $index * $index * 0.0003)
    }
}
$points.Modified()
$grid.Modified()
$plane.SetOrigin(0.0, 0.0, 0.0)
$cutter.Update()
$ambiguous = $cutter.GetOutput()

if ($ambiguous.GetNumberOfPoints() -ne 18 -or $ambiguous.GetNumberOfPolys() -ne 14) {
    throw "Expected 18 points and 14 triangles for the warped case-90 corpus; got $($ambiguous.GetNumberOfPoints()) points and $($ambiguous.GetNumberOfPolys()) triangles."
}
Write-Host 'ActiViz 5.8 triquadratic-hexa section oracle passed: canonical grid and ambiguous case-90 topology.'
