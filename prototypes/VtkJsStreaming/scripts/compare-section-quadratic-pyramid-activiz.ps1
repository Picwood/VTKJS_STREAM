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
    @(0,0,0), @(1,0,0), @(1,1,0), @(0,1,0), @(0.5,0.5,1),
    @(0.5,0,0), @(1,0.5,0), @(0.5,1,0), @(0,0.5,0),
    @(0.25,0.25,0.5), @(0.75,0.25,0.5), @(0.75,0.75,0.5), @(0.25,0.75,0.5)
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
[void]$grid.InsertNextCell(27, $pointIds) # VTK_QUADRATIC_PYRAMID
$plane = [Kitware.VTK.vtkPlane]::New()
[void]$plane.SetOrigin(0.25, 0.0, 0.0)
[void]$plane.SetNormal(1.0, 0.0, 0.0)
$cutter = [Kitware.VTK.vtkCutter]::New()
[void]$cutter.SetCutFunction($plane)
[void]$cutter.SetInput(0, $grid)
[void]$cutter.Update()
$output = $cutter.GetOutput()
$outputScalars = $output.GetPointData().GetScalars()
$coordinateKeys = @()
for ($index = 0; $index -lt $output.GetNumberOfPoints(); $index++) {
    $point = $output.GetPoint($index)
    if ([Math]::Abs($point[0] - 0.25) -gt 1e-12) { throw "Point $index is outside the cut plane." }
    if ([Math]::Abs($outputScalars.GetTuple1($index) - ($point[1] + 2.0 * $point[2])) -gt 1e-12) {
        throw "Point $index did not preserve scalar interpolation."
    }
    $coordinateKeys += [int][Math]::Round($point[1] * 4.0) * 10 + [int][Math]::Round($point[2] * 2.0)
}

if ($output.GetNumberOfPoints() -ne 5 -or $output.GetNumberOfPolys() -ne 3) {
    throw "Expected 5 points and 3 triangles; got $($output.GetNumberOfPoints()) points and $($output.GetNumberOfPolys()) triangles."
}
if (($coordinateKeys | Sort-Object) -join ',' -ne '0,11,20,31,40') {
    throw "ActiViz returned unexpected in-plane coordinates: $(($coordinateKeys | Sort-Object) -join ',')."
}

# Force the first legacy sub-pyramid to case 21. The generated base center is
# positive while its two adjacent quadratic edge nodes are negative.
$signedX = @(1, 1, 1, 1, 1, -1, 10, 10, -1, 1, 1, 1, 1)
for ($index = 0; $index -lt $coordinates.Count; $index++) {
    $point = $coordinates[$index]
    $y = [double]$point[1] + $index * 0.007
    $z = [double]$point[2] + $index * $index * 0.0003
    [void]$points.SetPoint($index, [double]$signedX[$index], $y, $z)
    [void]$scalars.SetValue($index, $y + 2.0 * $z)
}
$points.Modified()
$scalars.Modified()
$grid.Modified()
$plane.SetOrigin(0.0, 0.0, 0.0)
$cutter.Update()
$ambiguous = $cutter.GetOutput()

if ($ambiguous.GetNumberOfPoints() -ne 10 -or $ambiguous.GetNumberOfPolys() -ne 8) {
    throw "Expected 10 points and 8 triangles for the warped case-21 corpus; got $($ambiguous.GetNumberOfPoints()) points and $($ambiguous.GetNumberOfPolys()) triangles."
}
Write-Host 'ActiViz 5.8 quadratic-pyramid section oracle passed: canonical coordinates and generated case-21 topology.'
