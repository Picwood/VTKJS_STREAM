param(
    [string]$FixturePath = (Join-Path $PSScriptRoot '..\examples\complex-frequency-paired-mini.frd'),
    [string]$AssemblyDirectory = (Join-Path $PSScriptRoot '..\CaeResults\bin\x64\Release')
)

$ErrorActionPreference = 'Stop'

$fixture = (Resolve-Path -LiteralPath $FixturePath).Path
$assemblyDirectoryPath = [IO.Path]::GetFullPath($AssemblyDirectory)
$caeResultsAssembly = Join-Path $assemblyDirectoryPath 'CaeResults.dll'
if (-not (Test-Path -LiteralPath $caeResultsAssembly)) {
    throw "CaeResults.dll was not found in '$assemblyDirectoryPath'. Run scripts/build-vtkjs-mvp.ps1 first."
}

$resolveHandler = [ResolveEventHandler] {
    param($sender, $eventArgs)

    $assemblyName = ([Reflection.AssemblyName]$eventArgs.Name).Name + '.dll'
    $candidate = Join-Path $assemblyDirectoryPath $assemblyName
    if (Test-Path -LiteralPath $candidate) {
        return [Reflection.Assembly]::LoadFrom($candidate)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolveHandler)
$originalCulture = [Globalization.CultureInfo]::CurrentCulture
$originalUICulture = [Globalization.CultureInfo]::CurrentUICulture

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Near([double]$Actual, [double]$Expected, [double]$Tolerance, [string]$Message) {
    if ([double]::IsNaN($Actual) -or [Math]::Abs($Actual - $Expected) -gt $Tolerance) {
        throw "$Message Expected $Expected +/- $Tolerance; got $Actual."
    }
}

try {
    # PrePoMax establishes invariant numeric parsing during application startup.
    # Reproduce that host contract when invoking the reader directly from PowerShell.
    [Globalization.CultureInfo]::CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
    [Globalization.CultureInfo]::CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture
    [void][Reflection.Assembly]::LoadFrom($caeResultsAssembly)
    $fixtureText = [IO.File]::ReadAllText($fixture)
    Assert-True ($fixtureText.Contains('1PMODE')) 'Fixture must contain 1PMODE metadata.'
    Assert-True (-not $fixtureText.Contains('MODAL')) 'Fixture must omit the legacy MODAL marker to exercise the 1PMODE parser path.'

    $results = [CaeResults.FrdFileReader]::Read($fixture)
    Assert-True ($null -ne $results) 'FRD reader returned no results.'
    Assert-True ($results.ContainsComplexResults()) 'Paired DISP/DISPI fields were not recognized as complex results.'

    $fieldData = @($results.GetAllFieldData())
    $fieldNames = @($results.GetAllFieldNames())
    foreach ($requiredName in @('DISP', 'DISPR', 'DISPI', 'DISPMAG', 'DISPPHA')) {
        Assert-True ($fieldNames -contains $requiredName) "Missing synthesized complex field '$requiredName'."
    }

    $frequencyFields = @($fieldData | Where-Object {
        $_.StepType.ToString() -eq 'Frequency' -and $_.StepId -eq 1 -and $_.StepIncrementId -eq 1
    })
    Assert-True ($frequencyFields.Count -ge 5) '1PMODE did not classify the paired result block as frequency mode 1.'

    $realData = $results.GetFieldData('DISPR', 'U1', 1, 1, $true)
    $imaginaryData = $results.GetFieldData('DISPI', 'U1', 1, 1, $true)
    $magnitudeData = $results.GetFieldData('DISPMAG', 'U1', 1, 1, $true)
    Assert-True ($realData.Valid -and $imaginaryData.Valid -and $magnitudeData.Valid) 'Complex U1 field lookup failed.'

    $realU1 = $results.GetField($realData, $false).GetComponentValues('U1')
    $imaginaryU1 = $results.GetField($imaginaryData, $false).GetComponentValues('U1')
    $magnitudeU1 = $results.GetField($magnitudeData, $false).GetComponentValues('U1')
    Assert-Near $realU1[0] 3.0 0.000001 'Unexpected real U1 at node 1.'
    Assert-Near $imaginaryU1[0] 4.0 0.000001 'Unexpected imaginary U1 at node 1.'
    Assert-Near $magnitudeU1[0] 5.0 0.000001 'Unexpected complex magnitude U1 at node 1.'

    [pscustomobject]@{
        Fixture = $fixture
        Bytes = (Get-Item -LiteralPath $fixture).Length
        StepType = $frequencyFields[0].StepType.ToString()
        StepId = $frequencyFields[0].StepId
        Mode = $frequencyFields[0].StepIncrementId
        Frequency = $frequencyFields[0].Time
        Complex = $results.ContainsComplexResults()
        Fields = ($fieldNames -join ',')
        Node1U1 = "3 + 4i"
        Node1U1Magnitude = $magnitudeU1[0]
    } | Format-List
}
finally {
    [Globalization.CultureInfo]::CurrentCulture = $originalCulture
    [Globalization.CultureInfo]::CurrentUICulture = $originalUICulture
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolveHandler)
}
