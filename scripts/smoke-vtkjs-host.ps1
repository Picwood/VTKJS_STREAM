param(
    [int]$Port = 5187
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serverDll = Resolve-Path (Join-Path $repositoryRoot 'prototypes\VtkJsStreaming\src\PrePoMax.Render.Server\bin\Release\net8.0\PrePoMax.Render.Server.dll')
$clientRoot = Resolve-Path (Join-Path $repositoryRoot 'prototypes\VtkJsStreaming\src\client\dist')
$tokenBytes = New-Object byte[] 32
$tokenGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
$tokenGenerator.GetBytes($tokenBytes)
$tokenGenerator.Dispose()
$hostToken = [Convert]::ToBase64String($tokenBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')
$stdoutPath = Join-Path ([IO.Path]::GetTempPath()) ('prepomax-vtkjs-' + [Guid]::NewGuid().ToString('N') + '.log')
$stderrPath = $stdoutPath + '.err'
$serverProcess = $null

$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:PREPOMAX_RENDER_PORT = $Port.ToString([Globalization.CultureInfo]::InvariantCulture)
$env:PREPOMAX_RENDER_CLIENT_ROOT = $clientRoot.Path
$env:PREPOMAX_RENDER_HOST_TOKEN = $hostToken

try {
    $serverProcess = Start-Process -FilePath 'dotnet' -ArgumentList ('"' + $serverDll.Path + '"') `
        -WorkingDirectory (Split-Path -Parent $serverDll.Path) -WindowStyle Hidden -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath -PassThru
    $baseUrl = "http://127.0.0.1:$Port"
    $health = $null
    for ($attempt = 0; $attempt -lt 50 -and $null -eq $health; $attempt++) {
        try { $health = Invoke-RestMethod "$baseUrl/healthz" -TimeoutSec 1 }
        catch { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $health) { throw 'The renderer host did not become healthy.' }

    $index = Invoke-WebRequest "$baseUrl/" -UseBasicParsing
    $session = Invoke-RestMethod -Method Post "$baseUrl/api/v1/sessions" `
        -Headers @{ 'X-PrePoMax-Host-Token' = $hostToken }
    $shutdown = Invoke-WebRequest -Method Post "$baseUrl/api/v1/host/shutdown" `
        -Headers @{ 'X-PrePoMax-Host-Token' = $hostToken } -UseBasicParsing
    $serverProcess.WaitForExit(5000) | Out-Null

    if ($index.StatusCode -ne 200 -or $index.Content -notmatch 'PrePoMax streaming renderer') {
        throw 'The renderer client was not served.'
    }
    if ($shutdown.StatusCode -ne 202 -or -not $serverProcess.HasExited) {
        throw 'The renderer host did not shut down gracefully.'
    }
    $capturedLog = ((Get-Content -LiteralPath $stdoutPath, $stderrPath -Raw -ErrorAction SilentlyContinue) -join "`n")
    if ($capturedLog.Contains($hostToken) -or $capturedLog.Contains($clientRoot.Path) -or
        $capturedLog.Contains($repositoryRoot)) {
        throw 'Production renderer logs exposed a credential or physical filesystem path.'
    }

    [pscustomobject]@{
        Health = $health.status
        IndexStatus = $index.StatusCode
        SceneId = $session.sceneId
        ShutdownStatus = $shutdown.StatusCode
        Exited = $serverProcess.HasExited
        SensitiveLogDataAbsent = $true
    }
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        $serverProcess.Kill()
        $serverProcess.WaitForExit(2000) | Out-Null
    }
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
}
