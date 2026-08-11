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
    @(0.0, 0.0, 0.0), @(1.0, 0.0, 0.0), @(0.0, 1.0, 0.0), @(0.0, 0.0, 1.0),
    @(0.5, 0.0, 0.0), @(0.5, 0.5, 0.0), @(0.0, 0.5, 0.0),
    @(0.0, 0.0, 0.5), @(0.5, 0.0, 0.5), @(0.0, 0.5, 0.5)
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
[void]$grid.InsertNextCell(24, $pointIds) # VTK_QUADRATIC_TETRA

$plane = [Kitware.VTK.vtkPlane]::New()
[void]$plane.SetOrigin(0.25, 0.0, 0.0)
[void]$plane.SetNormal(1.0, 0.0, 0.0)
$cutter = [Kitware.VTK.vtkCutter]::New()
[void]$cutter.SetCutFunction($plane)
[void]$cutter.SetInput(0, $grid)
[void]$cutter.Update()
$output = $cutter.GetOutput()

if ($output.GetNumberOfPoints() -ne 10 -or $output.GetNumberOfPolys() -ne 9) {
    throw "Expected the VTK 5.8 8-tetra contour to produce 10 points and 9 triangles; got $($output.GetNumberOfPoints()) points and $($output.GetNumberOfPolys()) triangles."
}
$outputScalars = $output.GetPointData().GetScalars()
$coordinateKeys = @()
for ($index = 0; $index -lt $output.GetNumberOfPoints(); $index++) {
    $point = $output.GetPoint($index)
    if ([Math]::Abs($point[0] - 0.25) -gt 1e-12) {
        throw "ActiViz point $index is not on the requested plane: [$($point -join ', ')]."
    }
    $expectedScalar = $point[1] + 2.0 * $point[2]
    if ([Math]::Abs($outputScalars.GetTuple1($index) - $expectedScalar) -gt 1e-12) {
        throw "ActiViz point $index did not preserve linear scalar interpolation."
    }
    $coordinateKeys += [int][Math]::Round($point[1] * 4.0) * 10 + [int][Math]::Round($point[2] * 4.0)
}
$actualKeys = ($coordinateKeys | Sort-Object) -join ','
$expectedKeys = '0,1,2,3,10,11,12,20,21,30'
if ($actualKeys -ne $expectedKeys) {
    throw "ActiViz returned unexpected in-plane coordinates: $actualKeys."
}

Write-Host 'ActiViz 5.8 quadratic-tetra section oracle passed: 10 points, 9 triangles, exact plane coordinates and interpolated scalars.'
