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
    @(0,0,0), @(1,0,0), @(0,1,0), @(0,0,1), @(1,0,1), @(0,1,1),
    @(0.5,0,0), @(0.5,0.5,0), @(0,0.5,0),
    @(0.5,0,1), @(0.5,0.5,1), @(0,0.5,1),
    @(0,0,0.5), @(1,0,0.5), @(0,1,0.5)
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
[void]$grid.InsertNextCell(26, $pointIds) # VTK_QUADRATIC_WEDGE
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

if ($output.GetNumberOfPoints() -ne 12 -or $output.GetNumberOfPolys() -ne 12) {
    throw "Expected 12 points and 12 triangles; got $($output.GetNumberOfPoints()) points and $($output.GetNumberOfPolys()) triangles."
}
if (($coordinateKeys | Sort-Object) -join ',' -ne '0,1,2,10,11,12,20,21,22,30,31,32') {
    throw "ActiViz returned unexpected in-plane coordinates: $(($coordinateKeys | Sort-Object) -join ',')."
}

# Force the first legacy sub-wedge to case 43, whose table emits four triangles.
$signedX = @(1, 1, 1, 1, 1, 1, 1, -1, -1, -10, -1, 10, 1, -10, 10)
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

if ($ambiguous.GetNumberOfPoints() -ne 20 -or $ambiguous.GetNumberOfPolys() -ne 18) {
    throw "Expected 20 points and 18 triangles for the warped case-43 corpus; got $($ambiguous.GetNumberOfPoints()) points and $($ambiguous.GetNumberOfPolys()) triangles."
}
Write-Host 'ActiViz 5.8 quadratic-wedge section oracle passed: canonical grid and generated case-43 topology.'
