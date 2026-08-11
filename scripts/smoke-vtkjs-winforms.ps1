param(
    [string]$ModelPath,
    [switch]$ExpectFiniteScalars,
    [string]$SwitchField,
    [switch]$ExercisePick,
    [switch]$ExerciseAnnotation,
    [switch]$ExerciseExtrema,
    [switch]$ExerciseAnnotationInteraction,
    [switch]$ExerciseLargeSelection,
    [switch]$ExerciseCameraState,
    [switch]$ExerciseBoxZoom,
    [switch]$ExerciseVisibilityCamera,
    [switch]$ExerciseExplodedPreview,
    [switch]$ExerciseSectionView,
    [ValidateRange(0, 8000000)]
    [int]$SelectionCapacityIds = 0,
    [switch]$ExerciseAnimation,
    [switch]$MeasureAnimationCadence,
    [ValidateSet('ScaleFactor', 'TimeIncrements', 'Harmonic')]
    [string]$AnimationMode = 'ScaleFactor',
    [ValidateSet('Element', 'Surface', 'GeometryEdge', 'Geometry', 'GeometryPoint')]
    [string]$PickItem = 'Element'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$executable = Resolve-Path (Join-Path $repositoryRoot 'PrePoMax\bin\x64\Release\PrePoMax.exe')
$env:PREPOMAX_RENDERER = 'vtkjs'
$traceFile = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-smoke-{0}.log" -f [Guid]::NewGuid().ToString('N'))
$fieldSwitchTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-field-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
$pickTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-pick-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
$animationTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-animation-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
$animationFrameTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-animation-frame-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
$env:PREPOMAX_RENDER_TRACE_FILE = $traceFile
if (-not [string]::IsNullOrWhiteSpace($SwitchField)) {
    $env:PREPOMAX_VTKJS_SMOKE_FIELD = $SwitchField
    $env:PREPOMAX_VTKJS_SMOKE_FIELD_TRIGGER = $fieldSwitchTrigger
}
if ($ExercisePick) {
    $env:PREPOMAX_VTKJS_SMOKE_PICK_TRIGGER = $pickTrigger
    $env:PREPOMAX_VTKJS_SMOKE_PICK_ITEM = $PickItem
}
if ($ExerciseAnnotation) { $env:PREPOMAX_VTKJS_SMOKE_ANNOTATION = '1' }
if ($ExerciseExtrema) { $env:PREPOMAX_VTKJS_SMOKE_EXTREMA = '1' }
if ($ExerciseAnnotationInteraction) { $env:PREPOMAX_VTKJS_SMOKE_ANNOTATION_INTERACTION = '1' }
if ($ExerciseLargeSelection) { $env:PREPOMAX_VTKJS_SMOKE_LARGE_SELECTION = '1' }
if ($ExerciseCameraState) { $env:PREPOMAX_VTKJS_SMOKE_CAMERA = '1' }
if ($ExerciseBoxZoom) { $env:PREPOMAX_VTKJS_SMOKE_BOX_ZOOM = '1' }
if ($ExerciseVisibilityCamera) { $env:PREPOMAX_VTKJS_SMOKE_VISIBILITY_CAMERA = '1' }
if ($ExerciseExplodedPreview) { $env:PREPOMAX_VTKJS_SMOKE_EXPLODED_PREVIEW = '1' }
if ($ExerciseSectionView) { $env:PREPOMAX_VTKJS_SMOKE_SECTION_VIEW = '1' }
if ($SelectionCapacityIds -gt 0) { $env:PREPOMAX_VTKJS_SMOKE_SELECTION_CAPACITY = $SelectionCapacityIds.ToString() }
if ($ExerciseAnimation) {
    $env:PREPOMAX_VTKJS_SMOKE_ANIMATION = '1'
    $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_MODE = $AnimationMode
    $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_TRIGGER = $animationTrigger
    $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_FRAME_TRIGGER = $animationFrameTrigger
    if ($MeasureAnimationCadence) { $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_SEQUENCE = '1' }
}
elseif ($MeasureAnimationCadence) { throw 'MeasureAnimationCadence requires ExerciseAnimation.' }
$prePoMaxProcess = $null
$rendererProcessId = $null
$switchedScene = $null
$scalarOnlySwitch = $false
$validatedPickApplied = $false
$selectionOverlayVisible = $false
$selectedIds = $null
$locatorPoints = 0
$locatorCells = 0
$serverFrustumApplied = $false
$annotationVisible = $false
$extremaVisible = $false
$annotationInteractionApplied = $false
$largeSelectionCount = 0
$cameraStateRetained = $false
$boxZoomApplied = $false
$visibilityCameraApplied = $false
$explodedPreviewApplied = $false
$sectionViewApplied = $false
$sectionCapActors = 0
$sectionSkippedCells = 0
$animationFramePublished = $false
$animationFrames = 0
$animationFramesPublished = 0
$animationFrameLatencies = New-Object System.Collections.Generic.List[double]
$browserFrameSamples = New-Object System.Collections.Generic.List[object]
$hostFrameSamples = New-Object System.Collections.Generic.List[object]
$captureFrameSamples = New-Object System.Collections.Generic.List[object]
$animationHostFrameBytes = 0L
$overlayItem = if ($PickItem -eq 'GeometryEdge') { 'geometry-edge' }
    elseif ($PickItem -eq 'GeometryPoint') { 'geometry' }
    else { $PickItem.ToLowerInvariant() }

Add-Type -AssemblyName UIAutomationClient
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class PrePoMaxSmokeNativeMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
'@
function Invoke-CloseConfirmation([int]$ProcessId) {
    $processCondition = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
    $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll(
        [Windows.Automation.TreeScope]::Children, $processCondition)
    foreach ($window in $windows) {
        foreach ($buttonName in @('No', 'OK')) {
            $nameCondition = New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::NameProperty, $buttonName)
            $button = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $nameCondition)
            if ($null -ne $button) {
                $pattern = $button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
                if ($null -ne $pattern) {
                    try { $pattern.Invoke(); return $true }
                    catch [Windows.Automation.ElementNotEnabledException] { continue }
                    catch [Windows.Automation.ElementNotAvailableException] { continue }
                }
            }
        }
    }
    return $false
}

function Invoke-DismissStartupPrompt([int]$ProcessId) {
    $processCondition = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
    $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll(
        [Windows.Automation.TreeScope]::Children, $processCondition)
    foreach ($window in $windows) {
        $unitSystemCondition = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::NameProperty, 'Unit System Type')
        $unitSystemControl = $window.FindFirst(
            [Windows.Automation.TreeScope]::Descendants, $unitSystemCondition)
        if ($null -ne $unitSystemControl) {
            $okCondition = New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::NameProperty, 'OK')
            $okButton = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $okCondition)
            if ($null -ne $okButton) {
                $okPattern = $null
                if ($okButton.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$okPattern)) {
                    try { $okPattern.Invoke(); return $true }
                    catch [Windows.Automation.ElementNotEnabledException] { }
                    catch [Windows.Automation.ElementNotAvailableException] { }
                }
                $buttonHandle = [IntPtr]$okButton.Current.NativeWindowHandle
                if ($buttonHandle -ne [IntPtr]::Zero) {
                    [PrePoMaxSmokeNativeMethods]::PostMessage(
                        $buttonHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
                    return $true
                }
            }
        }
        # A stale recovery file must not override the explicitly supplied smoke-test model.
        # Cancel is intentionally attempted only during startup; using it during shutdown
        # would cancel FrmMain's close confirmation.
        foreach ($buttonName in @('Cancel', 'No', 'OK')) {
            $nameCondition = New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::NameProperty, $buttonName)
            $button = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $nameCondition)
            if ($null -ne $button) {
                $pattern = $button.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
                if ($null -ne $pattern) {
                    try { $pattern.Invoke(); return $true }
                    catch [Windows.Automation.ElementNotEnabledException] { continue }
                    catch [Windows.Automation.ElementNotAvailableException] { continue }
                }
            }
        }
    }
    return $false
}

function Invoke-PrepareInputDeckPrompt([int]$ProcessId, [bool]$AllowRecovery, [bool]$AllowUnits) {
    $processCondition = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
    $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll(
        [Windows.Automation.TreeScope]::Children, $processCondition)
    foreach ($window in $windows) {
        $recoveryCondition = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::NameProperty,
            'A recovery file from a previous PrePoMax session exists. Would you like to try to recover it?')
        if ($AllowRecovery -and
            $null -ne $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $recoveryCondition)) {
            $cancelCondition = New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::NameProperty, 'Cancel')
            $cancelButton = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $cancelCondition)
            if ($null -ne $cancelButton) {
                $buttonHandle = [IntPtr]$cancelButton.Current.NativeWindowHandle
                if ($buttonHandle -ne [IntPtr]::Zero) {
                    [PrePoMaxSmokeNativeMethods]::PostMessage(
                        $buttonHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
                    return 'Recovery'
                }
            }
        }
        if (-not $AllowUnits) { continue }
        $unitSystemCondition = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::NameProperty, 'Unit System Type')
        if ($null -eq $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $unitSystemCondition)) {
            continue
        }
        $okCondition = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::NameProperty, 'OK')
        $okButton = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $okCondition)
        if ($null -eq $okButton) { continue }
        $okPattern = $null
        if ($okButton.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern, [ref]$okPattern)) {
            try { $okPattern.Invoke(); return 'Units' }
            catch [Windows.Automation.ElementNotEnabledException] { }
            catch [Windows.Automation.ElementNotAvailableException] { }
        }
        $buttonHandle = [IntPtr]$okButton.Current.NativeWindowHandle
        if ($buttonHandle -ne [IntPtr]::Zero) {
            [PrePoMaxSmokeNativeMethods]::PostMessage(
                $buttonHandle, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            return 'Units'
        }
    }
    return $null
}

function Get-WindowDiagnostic([int]$ProcessId) {
    $processCondition = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
    $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll(
        [Windows.Automation.TreeScope]::Children, $processCondition)
    $descriptions = foreach ($window in $windows) {
        $names = @($window.FindAll([Windows.Automation.TreeScope]::Descendants,
                    [Windows.Automation.Condition]::TrueCondition) |
            ForEach-Object { $_.Current.Name } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Unique -First 12)
        "window='$($window.Current.Name)' controls='$($names -join ' | ')'"
    }
    return $descriptions -join '; '
}

try {
    $argumentList = $null
    if (-not [string]::IsNullOrWhiteSpace($ModelPath)) {
        $resolvedModel = Resolve-Path $ModelPath
        $argumentList = '-f "' + $resolvedModel.Path + '"'
    }
    $startParameters = @{
        FilePath = $executable.Path
        WorkingDirectory = Split-Path -Parent $executable.Path
        WindowStyle = 'Minimized'
        PassThru = $true
    }
    if ($null -ne $argumentList) { $startParameters.ArgumentList = $argumentList }
    $prePoMaxProcess = Start-Process @startParameters

    for ($attempt = 0; $attempt -lt 100 -and $null -eq $rendererProcessId; $attempt++) {
        $child = Get-CimInstance Win32_Process -Filter "ParentProcessId = $($prePoMaxProcess.Id)" |
            Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'PrePoMax.Render.Server.dll' } |
            Select-Object -First 1
        if ($null -ne $child) { $rendererProcessId = [int]$child.ProcessId }
        else { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $rendererProcessId) {
        throw 'FrmMain did not start its VTK.js renderer child process.'
    }

    $webViewStarted = $false
    for ($attempt = 0; $attempt -lt 100 -and -not $webViewStarted; $attempt++) {
        $webViewStarted = @(Get-Process -Name 'msedgewebview2' -ErrorAction SilentlyContinue).Count -gt 0
        if (-not $webViewStarted) { Start-Sleep -Milliseconds 100 }
    }
    if (-not $webViewStarted) { throw 'The WebView2 runtime process was not observed.' }

    $uploadedScene = $null
    if (-not [string]::IsNullOrWhiteSpace($ModelPath)) {
        $listeningPort = $null
        for ($attempt = 0; $attempt -lt 100 -and $null -eq $listeningPort; $attempt++) {
            $connection = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
                Where-Object { $_.OwningProcess -eq $rendererProcessId -and $_.LocalAddress -eq '127.0.0.1' } |
                Select-Object -First 1
            if ($null -ne $connection) { $listeningPort = $connection.LocalPort }
            else { Start-Sleep -Milliseconds 100 }
        }
        if ($null -eq $listeningPort) { throw 'The renderer child loopback port was not discovered.' }
        $baseUrl = "http://127.0.0.1:$listeningPort"
        $session = Invoke-RestMethod -Method Post "$baseUrl/api/v1/sessions"
        $headers = @{ 'X-PrePoMax-Session' = $session.accessToken }
        $isInputDeck = [string]::Equals(
            [System.IO.Path]::GetExtension($resolvedModel.Path), '.inp', [System.StringComparison]::OrdinalIgnoreCase)
        $recoveryPromptHandled = $false
        $unitPromptHandled = $false
        $sceneAttempts = if ($ExerciseLargeSelection -or $SelectionCapacityIds -gt 0 -or $isInputDeck) { 480 } else { 60 }
        for ($attempt = 0; $attempt -lt $sceneAttempts -and $null -eq $uploadedScene; $attempt++) {
            if ($isInputDeck -and $attempt -lt 80) {
                $promptAction = Invoke-PrepareInputDeckPrompt $prePoMaxProcess.Id `
                    (-not $recoveryPromptHandled) (-not $unitPromptHandled)
                if ($promptAction -eq 'Recovery') { $recoveryPromptHandled = $true }
                elseif ($promptAction -eq 'Units') { $unitPromptHandled = $true }
            }
            elseif (-not $isInputDeck -and $attempt -lt 10) {
                Invoke-DismissStartupPrompt $prePoMaxProcess.Id | Out-Null
            }
            try {
                $scenes = Invoke-RestMethod "$baseUrl/api/v1/scenes" -Headers $headers
                if ($scenes.Count -gt 0) {
                    $candidate = Invoke-RestMethod "$baseUrl/api/v1/scenes/$($scenes[0].sceneId)/manifest" -Headers $headers
                    $inputDeckReady = -not $isInputDeck -or ($unitPromptHandled -and
                        (Test-Path $traceFile) -and (Select-String -Path $traceFile -Pattern 'camera view=' -Quiet))
                    if ($inputDeckReady -and $candidate.name -eq 'PrePoMax current surface snapshot' -and
                        $candidate.actors.Count -gt 0) {
                        $uploadedScene = $candidate
                    }
                }
            }
            catch { }
            if ($null -eq $uploadedScene) { Start-Sleep -Milliseconds 250 }
        }
        if ($null -eq $uploadedScene) {
            $lastScene = if ($null -eq $candidate) { '<none>' } else { "$($candidate.name), actors=$($candidate.actors.Count)" }
            $windowDiagnostic = Get-WindowDiagnostic $prePoMaxProcess.Id
            $captureDiagnostic = if (Test-Path $traceFile) {
                $important = @(Get-Content $traceFile | Where-Object {
                    $_ -match 'captured actor|rejected part|error:|renderer |cleared |publishing |published '
                })
                if ($important.Count -gt 0) { ($important | Select-Object -Last 20) -join ' | ' }
                else { (Get-Content $traceFile -Tail 12) -join ' | ' }
            } else { '<none>' }
            throw "PrePoMax did not publish a non-empty real surface snapshot. Last scene: $lastScene. UI: $windowDiagnostic Capture: $captureDiagnostic"
        }
        if ($ExpectFiniteScalars -and
            $uploadedScene.field.validRange[0] -eq 0 -and $uploadedScene.field.validRange[1] -eq 1) {
            throw 'The uploaded result scene did not expose a non-default finite scalar range.'
        }
        if ($ExpectFiniteScalars -and $uploadedScene.field.name -eq 'PrePoMax values') {
            throw 'The uploaded result scene did not expose real PrePoMax field metadata.'
        }
        if ($ExerciseAnimation) {
            $staticSceneId = $uploadedScene.sceneId
            $staticRevision = [long]$uploadedScene.sceneRevision
            New-Item -ItemType File -Path $animationTrigger -Force | Out-Null
            $animationBaseline = $null
            for ($attempt = 0; $attempt -lt 160 -and $null -eq $animationBaseline; $attempt++) {
                Start-Sleep -Milliseconds 250
                if (-not (Test-Path $traceFile)) { continue }
                $setupFailure = Select-String -Path $traceFile -Pattern 'acceptance: animation setup failed:' |
                    Select-Object -Last 1
                if ($null -ne $setupFailure) { throw $setupFailure.Line }
                $metadataLine = Select-String -Path $traceFile -Pattern "animation metadata type=$AnimationMode frames=([0-9]+)" |
                    Select-Object -Last 1
                if ($null -eq $metadataLine -or $metadataLine.Line -notmatch 'frames=([0-9]+)') { continue }
                $publishedTopologyLine = Select-String -Path $traceFile -Pattern 'published topology generation=' |
                    Select-Object -Last 1
                if ($null -eq $publishedTopologyLine -or
                    $publishedTopologyLine.LineNumber -le $metadataLine.LineNumber) { continue }
                $animationFrames = [int]$Matches[1]
                $currentScenes = Invoke-RestMethod "$baseUrl/api/v1/scenes" -Headers $headers
                if ($currentScenes.Count -eq 0) { continue }
                $candidateAnimationBaseline = Invoke-RestMethod "$baseUrl/api/v1/scenes/$($currentScenes[0].sceneId)/manifest" -Headers $headers
                if ($candidateAnimationBaseline.sceneId -ne $staticSceneId -or
                    $candidateAnimationBaseline.sceneRevision -gt $staticRevision) {
                    $animationBaseline = $candidateAnimationBaseline
                }
            }
            if ($null -eq $animationBaseline) {
                $animationDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
                throw "PrePoMax did not publish animation frame 0 before the delta. Trace: $animationDiagnostic"
            }
            if ($animationFrames -lt 2) {
                throw "The $AnimationMode acceptance model exposes only $animationFrames animation frame; at least two are required."
            }
            $animationHostFrameBytes = 36L
            foreach ($actor in $animationBaseline.actors) {
                $actorPointCount = ($actor.chunks.pointCount | Measure-Object -Sum).Sum
                $animationHostFrameBytes += 12L +
                    [System.Text.Encoding]::UTF8.GetByteCount([string]$actor.name) + 16L * $actorPointCount
            }
            $baselineTopologyIds = @($animationBaseline.actors | ForEach-Object {
                $actor = $_
                $_.chunks | ForEach-Object {
                    "$($actor.actorId)|$($_.chunkId)|$($_.resources.triangleConnectivity.resourceId)|" +
                    "$($_.resources.sourcePointIds.resourceId)|$($_.resources.sourceElementIds.resourceId)"
                }
            })
            $baselinePositionHashes = @($animationBaseline.actors | ForEach-Object {
                $actor = $_
                $_.chunks | ForEach-Object {
                    $positionId = $_.resources.positions.resourceId
                    $hash = ($animationBaseline.resources | Where-Object resourceId -eq $positionId).sha256
                    "$($actor.actorId)|$($_.chunkId)|$hash"
                }
            })
            $targetFrames = if ($MeasureAnimationCadence) { @(1..($animationFrames - 1)) } else { @(1) }
            $previousScene = $animationBaseline
            $previousPositionHashes = $baselinePositionHashes
            foreach ($targetFrame in $targetFrames) {
                [System.IO.File]::WriteAllText($animationFrameTrigger, $targetFrame.ToString())
                $frameTimer = [System.Diagnostics.Stopwatch]::StartNew()
                $frameAccepted = $false
                for ($attempt = 0; $attempt -lt 240 -and -not $frameAccepted; $attempt++) {
                    Start-Sleep -Milliseconds 250
                    $traceReady = (Test-Path $traceFile) -and
                        (Select-String -Path $traceFile -Pattern "animation frame=$targetFrame type=$AnimationMode .*generation=" -Quiet) -and
                        (Select-String -Path $traceFile -Pattern "acceptance: animation target requested type=$AnimationMode frame=$targetFrame" -Quiet) -and
                        (Select-String -Path $traceFile -Pattern "compatibility bypass capability=camera operation=animation-frame-adjust-$targetFrame" -Quiet)
                    if (-not $traceReady) { continue }
                    $currentScenes = Invoke-RestMethod "$baseUrl/api/v1/scenes" -Headers $headers
                    if ($currentScenes.Count -eq 0) { continue }
                    $animationScene = Invoke-RestMethod "$baseUrl/api/v1/scenes/$($currentScenes[0].sceneId)/manifest" -Headers $headers
                    if ($animationScene.sceneRevision -le $previousScene.sceneRevision) { continue }
                    $animationPositionHashes = @($animationScene.actors | ForEach-Object {
                        $actor = $_
                        $_.chunks | ForEach-Object {
                            $positionId = $_.resources.positions.resourceId
                            $hash = ($animationScene.resources | Where-Object resourceId -eq $positionId).sha256
                            "$($actor.actorId)|$($_.chunkId)|$hash"
                        }
                    })
                    $animationTopologyIds = @($animationScene.actors | ForEach-Object {
                        $actor = $_
                        $_.chunks | ForEach-Object {
                            "$($actor.actorId)|$($_.chunkId)|$($_.resources.triangleConnectivity.resourceId)|" +
                            "$($_.resources.sourcePointIds.resourceId)|$($_.resources.sourceElementIds.resourceId)"
                        }
                    })
                    $animationSameScene = $animationScene.sceneId -eq $animationBaseline.sceneId
                    $animationRevisionAdvanced = $animationScene.sceneRevision -gt $previousScene.sceneRevision
                    $animationPositionsChanged = @(Compare-Object $previousPositionHashes $animationPositionHashes).Count -gt 0
                    $animationTopologyStable = @(Compare-Object $baselineTopologyIds $animationTopologyIds).Count -eq 0
                    $frameAccepted = $animationSameScene -and $animationRevisionAdvanced -and
                        $animationPositionsChanged -and $animationTopologyStable
                }
                $frameTimer.Stop()
                if (-not $frameAccepted) {
                    $animationDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
                    throw "PrePoMax did not publish $AnimationMode frame $targetFrame with the required invariants " +
                        "(sameScene=$animationSameScene revisionAdvanced=$animationRevisionAdvanced " +
                        "positionsChanged=$animationPositionsChanged topologyStable=$animationTopologyStable). " +
                        "Trace: $animationDiagnostic"
                }
                $browserLine = $null
                $browserRevision = [long]$animationScene.sceneRevision
                for ($attempt = 0; $attempt -lt 240 -and $null -eq $browserLine; $attempt++) {
                    Start-Sleep -Milliseconds 250
                    $browserLine = Select-String -Path $traceFile -Pattern "browser frame activated revision=$browserRevision " |
                        Select-Object -Last 1
                }
                if ($null -eq $browserLine -or $browserLine.Line -notmatch
                    'chunks=([0-9]+) bytes=([0-9]+) downloadMs=([0-9]+(?:\.[0-9]+)?) applyMs=([0-9]+(?:\.[0-9]+)?) paintMs=([0-9]+(?:\.[0-9]+)?) totalMs=([0-9]+(?:\.[0-9]+)?) cameraAdjusted=True') {
                    throw "The browser did not report stable activation for scene revision $browserRevision."
                }
                $invariant = [Globalization.CultureInfo]::InvariantCulture
                $browserFrameSamples.Add([pscustomobject]@{
                    Frame = $targetFrame
                    SceneRevision = $browserRevision
                    Chunks = [int]$Matches[1]
                    Bytes = [long]$Matches[2]
                    DownloadMs = [double]::Parse($Matches[3], $invariant)
                    ApplyMs = [double]::Parse($Matches[4], $invariant)
                    StablePaintMs = [double]::Parse($Matches[5], $invariant)
                    TotalMs = [double]::Parse($Matches[6], $invariant)
                })
                $transportLine = Select-String -Path $traceFile -Pattern 'frame transport generation=' |
                    Select-Object -Last 1
                if ($null -eq $transportLine -or $transportLine.Line -notmatch
                    'writeMs=([0-9]+(?:\.[0-9]+)?) responseMs=([0-9]+(?:\.[0-9]+)?) totalMs=([0-9]+(?:\.[0-9]+)?) serverReadMs=([0-9]+(?:\.[0-9]+)?) serverUpdateMs=([0-9]+(?:\.[0-9]+)?) serverTotalMs=([0-9]+(?:\.[0-9]+)?) serverReadAllocatedBytes=([0-9]+) serverUpdateAllocatedBytes=([0-9]+) validationMs=([0-9]+(?:\.[0-9]+)?) validationAllocatedBytes=([0-9]+) resourcesMs=([0-9]+(?:\.[0-9]+)?) resourcesAllocatedBytes=([0-9]+) manifestMs=([0-9]+(?:\.[0-9]+)?) manifestAllocatedBytes=([0-9]+) refitMs=([0-9]+(?:\.[0-9]+)?) refitAllocatedBytes=([0-9]+)') {
                    throw "The host did not report frame transport timing for frame $targetFrame."
                }
                $hostFrameSamples.Add([pscustomobject]@{
                    Frame = $targetFrame
                    WriteMs = [double]::Parse($Matches[1], $invariant)
                    ResponseMs = [double]::Parse($Matches[2], $invariant)
                    TotalMs = [double]::Parse($Matches[3], $invariant)
                    ServerReadMs = [double]::Parse($Matches[4], $invariant)
                    ServerUpdateMs = [double]::Parse($Matches[5], $invariant)
                    ServerTotalMs = [double]::Parse($Matches[6], $invariant)
                    ServerReadAllocatedBytes = [long]$Matches[7]
                    ServerUpdateAllocatedBytes = [long]$Matches[8]
                    ValidationMs = [double]::Parse($Matches[9], $invariant)
                    ValidationAllocatedBytes = [long]$Matches[10]
                    ResourcesMs = [double]::Parse($Matches[11], $invariant)
                    ResourcesAllocatedBytes = [long]$Matches[12]
                    ManifestMs = [double]::Parse($Matches[13], $invariant)
                    ManifestAllocatedBytes = [long]$Matches[14]
                    RefitMs = [double]::Parse($Matches[15], $invariant)
                    RefitAllocatedBytes = [long]$Matches[16]
                })
                $captureLine = Select-String -Path $traceFile -Pattern "frame capture frame=$targetFrame " |
                    Select-Object -Last 1
                $activationLine = Select-String -Path $traceFile -Pattern "acceptance: animation activation frame=$targetFrame " |
                    Select-Object -Last 1
                if ($null -eq $captureLine -or $captureLine.Line -notmatch 'captureMs=([0-9]+(?:\.[0-9]+)?)' -or
                    $null -eq $activationLine -or $activationLine.Line -notmatch
                    'legacyMs=([0-9]+(?:\.[0-9]+)?) mirrorMs=([0-9]+(?:\.[0-9]+)?) totalMs=([0-9]+(?:\.[0-9]+)?)') {
                    throw "PrePoMax did not report capture timing for frame $targetFrame."
                }
                $captureFrameSamples.Add([pscustomobject]@{
                    Frame = $targetFrame
                    CaptureMs = [double]::Parse(($captureLine.Line -replace '^.*captureMs=', ''), $invariant)
                    LegacyActivationMs = [double]::Parse($Matches[1], $invariant)
                    MirrorMs = [double]::Parse($Matches[2], $invariant)
                    TotalActivationMs = [double]::Parse($Matches[3], $invariant)
                })
                $animationFrameLatencies.Add($frameTimer.Elapsed.TotalMilliseconds)
                $animationFramesPublished++
                $previousScene = $animationScene
                $previousPositionHashes = $animationPositionHashes
            }
            $animationFramePublished = $animationFramesPublished -eq $targetFrames.Count
            $uploadedScene = $animationScene
        }
        if (-not [string]::IsNullOrWhiteSpace($SwitchField)) {
            $switchParts = $SwitchField.Split('/', 2, [System.StringSplitOptions]::RemoveEmptyEntries)
            if ($switchParts.Count -ne 2) { throw 'SwitchField must use the FIELD/COMPONENT form.' }
            $expectedField = $switchParts[0].Trim()
            $expectedComponent = $switchParts[1].Trim()
            if ($uploadedScene.field.name -eq $expectedField -and $uploadedScene.field.component -eq $expectedComponent) {
                throw 'The field switch occurred before the initial manifest baseline was captured.'
            }
            $initialRevision = [long]$uploadedScene.sceneRevision
            $initialSceneId = $uploadedScene.sceneId
            $initialTopologyIds = @($uploadedScene.actors.chunks | ForEach-Object {
                $_.resources.positions.resourceId
                $_.resources.triangleConnectivity.resourceId
                $_.resources.sourcePointIds.resourceId
                $_.resources.sourceElementIds.resourceId
            })
            $initialScalarIds = @($uploadedScene.actors.chunks | ForEach-Object { $_.resources.pointScalars.resourceId })
            New-Item -ItemType File -Path $fieldSwitchTrigger -Force | Out-Null
            for ($attempt = 0; $attempt -lt 80 -and $null -eq $switchedScene; $attempt++) {
                Start-Sleep -Milliseconds 250
                $candidate = Invoke-RestMethod "$baseUrl/api/v1/scenes/$initialSceneId/manifest" -Headers $headers
                if ($candidate.sceneRevision -gt $initialRevision -and
                    $candidate.field.name -eq $expectedField -and
                    $candidate.field.component -eq $expectedComponent) {
                    $switchedScene = $candidate
                }
            }
            if ($null -eq $switchedScene) {
                $switchDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 20) -join ' | ' } else { '<none>' }
                throw "PrePoMax did not switch to result field '$SwitchField'. Last field='$($candidate.field.name)/$($candidate.field.component)'. Trace: $switchDiagnostic"
            }
            $switchedTopologyIds = @($switchedScene.actors.chunks | ForEach-Object {
                $_.resources.positions.resourceId
                $_.resources.triangleConnectivity.resourceId
                $_.resources.sourcePointIds.resourceId
                $_.resources.sourceElementIds.resourceId
            })
            $switchedScalarIds = @($switchedScene.actors.chunks | ForEach-Object { $_.resources.pointScalars.resourceId })
            if (@(Compare-Object $initialTopologyIds $switchedTopologyIds -SyncWindow 0).Count -ne 0) {
                throw 'A real result-component change replaced topology or source-ID resources.'
            }
            if (@(Compare-Object $initialScalarIds $switchedScalarIds -SyncWindow 0).Count -eq 0) {
                throw 'A real result-component change did not replace scalar resources.'
            }
            $scalarOnlySwitch = (Test-Path $traceFile) -and
                (Select-String -Path $traceFile -Pattern 'published scalars generation=' -Quiet)
            if (-not $scalarOnlySwitch) { throw 'The real result-component change did not use the scalar-only publisher.' }
            $uploadedScene = $switchedScene
        }
        if ($ExercisePick) {
            New-Item -ItemType File -Path $pickTrigger -Force | Out-Null
            $pickAttempts = if ($PickItem -eq 'GeometryEdge') { 160 } else { 80 }
            for ($attempt = 0; $attempt -lt $pickAttempts -and -not $validatedPickApplied; $attempt++) {
                Start-Sleep -Milliseconds 250
                if (Test-Path $traceFile) {
                    $selectionPattern = if ($PickItem -eq 'Geometry') {
                        "acceptance: area selection applied item=$PickItem ids="
                    } elseif ($PickItem -eq 'GeometryPoint') {
                        'acceptance: selection applied item=Geometry ids='
                    } else {
                        "acceptance: selection applied item=$PickItem ids="
                    }
                    $selectionLine = Select-String -Path $traceFile -Pattern $selectionPattern |
                        Select-Object -Last 1
                    $validatedPickApplied = $null -ne $selectionLine
                }
            }
            if (-not $validatedPickApplied) {
                $pickDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 25) -join ' | ' } else { '<none>' }
                throw "The browser pick was not applied to the PrePoMax $PickItem selection. Trace: $pickDiagnostic"
            }
            if ($selectionLine.Line -match 'ids=([0-9,-]+)') { $selectedIds = $Matches[1] }
            if ($PickItem -eq 'Geometry') {
                $serverFrustumApplied = $selectionLine.Line -match 'locator=True'
                if (-not $serverFrustumApplied) {
                    throw 'The geometry-area smoke fell back to visible triangles instead of the server locator frustum.'
                }
            }
            for ($attempt = 0; $attempt -lt 40 -and -not $selectionOverlayVisible; $attempt++) {
                Start-Sleep -Milliseconds 250
                $selectionOverlayVisible = (Test-Path $traceFile) -and
                    (Select-String -Path $traceFile -Pattern ("acceptance: selection overlay $overlayItem\|[1-9][0-9]*\|false") -Quiet)
            }
            if (-not $selectionOverlayVisible) {
                $overlayDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 25) -join ' | ' } else { '<none>' }
                throw "The PrePoMax selection did not produce a visible renderer overlay. Trace: $overlayDiagnostic"
            }
        }
        if ($ExerciseAnnotation) {
            for ($attempt = 0; $attempt -lt 80 -and -not $annotationVisible; $attempt++) {
                Start-Sleep -Milliseconds 250
                $annotationVisible = (Test-Path $traceFile) -and
                    (Select-String -Path $traceFile -Pattern 'acceptance: regular annotation true' -Quiet)
            }
            if (-not $annotationVisible) {
                $annotationDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 25) -join ' | ' } else { '<none>' }
                throw "The PrePoMax annotation did not produce a visible renderer callout. Trace: $annotationDiagnostic"
            }
        }
        if ($ExerciseExtrema) {
            for ($attempt = 0; $attempt -lt 80 -and -not $extremaVisible; $attempt++) {
                Start-Sleep -Milliseconds 250
                $extremaVisible = (Test-Path $traceFile) -and
                    (Select-String -Path $traceFile -Pattern 'acceptance: extrema true' -Quiet)
            }
            if (-not $extremaVisible) {
                $extremaDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 30) -join ' | ' } else { '<none>' }
                throw "The PrePoMax result extrema did not produce visible Min/Max renderer callouts. Trace: $extremaDiagnostic"
            }
        }
        if ($ExerciseAnnotationInteraction) {
            for ($attempt = 0; $attempt -lt 80 -and -not $annotationInteractionApplied; $attempt++) {
                Start-Sleep -Milliseconds 250
                $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
                $annotationInteractionApplied =
                    ($trace -match "annotation moved name='R_Annotation[^']+'.*offset=25,20").Count -gt 0 -and
                    ($trace -match "acceptance: annotation edit applied name='R_Annotation[^']+'").Count -gt 0 -and
                    ($trace -match 'acceptance: annotation interaction true').Count -gt 0 -and
                    ($trace -match "acceptance: annotation action applied name='R_Annotation[^']+' action=reset").Count -gt 0 -and
                    ($trace -match "acceptance: annotation action applied name='R_Annotation[^']+' action=delete").Count -gt 0 -and
                    ($trace -match 'acceptance: annotation context actions true').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=annotations operation=add').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=annotations operation=remove-named').Count -gt 0
            }
            if (-not $annotationInteractionApplied) {
                $interactionDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
                throw "The browser annotation move/edit/reset/delete workflow was not applied and echoed by PrePoMax. Trace: $interactionDiagnostic"
            }
        }
        if ($ExerciseLargeSelection -or $SelectionCapacityIds -gt 0) {
            for ($attempt = 0; $attempt -lt 160 -and $largeSelectionCount -eq 0; $attempt++) {
                Start-Sleep -Milliseconds 250
                if (Test-Path $traceFile) {
                    $largePattern = 'expected=([0-9]+) actual=node\|([0-9]+)\|[1-9][0-9]*\|false'
                    $largeLine = Select-String -Path $traceFile -Pattern $largePattern | Select-Object -Last 1
                    if ($null -ne $largeLine -and $largeLine.Line -match $largePattern -and
                        $Matches[1] -eq $Matches[2]) {
                        $largeSelectionCount = [int]$Matches[1]
                    }
                }
            }
            $sharedPublished = (Test-Path $traceFile) -and
                (Select-String -Path $traceFile -Pattern 'selection shared item=node ids=[1-9][0-9]{5,} bytes=' -Quiet)
            $minimumExpectedCount = if ($SelectionCapacityIds -gt 0) { $SelectionCapacityIds } else { 100001 }
            if ($largeSelectionCount -lt $minimumExpectedCount -or -not $sharedPublished) {
                $largeDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
                throw "The large PrePoMax selection did not cross the shared-buffer path and render visibly. Trace: $largeDiagnostic"
            }
        }
        $requiresInitialCameraForward = -not [string]::Equals(
            [System.IO.Path]::GetExtension($ModelPath), '.inp', [System.StringComparison]::OrdinalIgnoreCase)
        $cameraForwarded = (Test-Path $traceFile) -and
            (Select-String -Path $traceFile -Pattern 'camera view=' -Quiet)
        for ($attempt = 0; $attempt -lt 100 -and $requiresInitialCameraForward -and -not $cameraForwarded; $attempt++) {
            $cameraForwarded = (Test-Path $traceFile) -and (Select-String -Path $traceFile -Pattern 'camera view=' -Quiet)
            if (-not $cameraForwarded) { Start-Sleep -Milliseconds 100 }
        }
        if ($requiresInitialCameraForward -and -not $cameraForwarded) {
            $cameraDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
            throw "PrePoMax did not forward its initial camera view to the renderer host. Trace: $cameraDiagnostic"
        }
        if ($ExerciseCameraState) {
            for ($attempt = 0; $attempt -lt 80 -and -not $cameraStateRetained; $attempt++) {
                Start-Sleep -Milliseconds 250
                $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
                $cameraStateRetained =
                    ($trace -match 'camera state received scale=').Count -gt 0 -and
                    ($trace -match 'acceptance: camera state retained').Count -gt 0 -and
                    ($trace -match 'acceptance: camera retained roundtrip scale=').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=query-parameters').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=set-custom').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=set-front-back').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=set-normal-fit').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=set-vertical-fit').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=acceptance-adjust').Count -gt 0 -and
                    ($trace -match 'custom camera updated operation=fit').Count -ge 2 -and
                    ($trace -match 'custom camera updated operation=adjust').Count -gt 0
            }
            if (-not $cameraStateRetained) {
                $cameraDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
                throw "The browser camera state was not retained, queried, and round-tripped without ActiViz. Trace: $cameraDiagnostic"
            }
        }
        if ($ExerciseBoxZoom) {
            for ($attempt = 0; $attempt -lt 80 -and -not $boxZoomApplied; $attempt++) {
                Start-Sleep -Milliseconds 250
                $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
                $boxZoomApplied =
                    ($trace -match 'compatibility bypass capability=camera operation=start-box-zoom').Count -gt 0 -and
                    ($trace -match 'box zoom enabled').Count -gt 0 -and
                    ($trace -match 'acceptance: browser box zoom requested').Count -gt 0 -and
                    ($trace -match 'box zoom ended applied=True').Count -gt 0 -and
                    ($trace -match 'acceptance: box zoom completed applied=True scaleReduced=True').Count -gt 0
            }
            if (-not $boxZoomApplied) {
                $boxZoomDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
                throw "The browser-native box zoom did not complete and reduce the retained camera scale. Trace: $boxZoomDiagnostic"
            }
        }
        if ($ExerciseVisibilityCamera) {
            for ($attempt = 0; $attempt -lt 80 -and -not $visibilityCameraApplied; $attempt++) {
                Start-Sleep -Milliseconds 250
                $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
                $visibilityCameraApplied =
                    ($trace -match 'compatibility bypass capability=camera operation=actor-visibility-hide').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=actor-visibility-show').Count -gt 0 -and
                    ($trace -match 'actor visibility visible=False count=1').Count -gt 0 -and
                    ($trace -match 'actor visibility visible=True count=1').Count -gt 0 -and
                    ($trace -match "acceptance: visibility camera completed actor='").Count -gt 0
            }
            if (-not $visibilityCameraApplied) {
                $visibilityDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
                throw "Actor visibility did not update browser camera ownership without hidden adjustment. Trace: $visibilityDiagnostic"
            }
        }
        if ($ExerciseExplodedPreview) {
            for ($attempt = 0; $attempt -lt 80 -and -not $explodedPreviewApplied; $attempt++) {
                Start-Sleep -Milliseconds 250
                $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
                $directPreviewApplied =
                    ($trace -match 'compatibility bypass capability=geometry operation=exploded-preview').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=geometry operation=exploded-preview-remove').Count -gt 0 -and
                    ($trace -match 'browser exploded preview applied actors=1 animated=False').Count -ge 2 -and
                    ($trace -match 'exploded preview completion browser-acknowledged request=').Count -gt 0 -and
                    ($trace -match "acceptance: exploded preview handshake acknowledged actor='").Count -gt 0 -and
                    ($trace -match "acceptance: exploded preview completed actor='").Count -gt 0
                $controllerLifecycleApplied =
                    ($trace -match 'acceptance: exploded lifecycle activation completed').Count -gt 0 -and
                    ($trace -match 'acceptance: exploded lifecycle deactivation completed').Count -gt 0 -and
                    ($trace -match 'exploded preview completion browser-acknowledged request=').Count -ge 2 -and
                    ($trace -match 'scene applied completion browser-acknowledged').Count -ge 2
                $explodedPreviewApplied = $directPreviewApplied -or $controllerLifecycleApplied
            }
            if (-not $explodedPreviewApplied) {
                $explodedDiagnostic = if (Test-Path $traceFile) {
                    ((Get-Content $traceFile) | Where-Object { $_ -match 'exploded|scene applied' }) -join ' | '
                } else { '<none>' }
                throw "The browser exploded-view preview did not apply and clear without ActiViz. Trace: $explodedDiagnostic"
            }
        }
        if ($ExerciseSectionView) {
            for ($attempt = 0; $attempt -lt 480 -and -not $sectionViewApplied; $attempt++) {
                Start-Sleep -Milliseconds 250
                $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
                $sectionViewApplied =
                    ($trace -match 'compatibility bypass capability=geometry operation=section-view-create').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=geometry operation=section-view-update').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=geometry operation=section-view-remove').Count -gt 0 -and
                    ($trace -match 'browser section view applied enabled=True').Count -ge 2 -and
                    ($trace -match 'browser section view applied enabled=False').Count -gt 0 -and
                    ($trace -match 'browser section view applied enabled=True .*capActors=[1-9][0-9]*').Count -gt 0 -and
                    ($trace -match 'acceptance: section view lifecycle completed').Count -gt 0
            }
            if (-not $sectionViewApplied) {
                $sectionDiagnostic = if (Test-Path $traceFile) {
                    ((Get-Content $traceFile) | Where-Object { $_ -match 'section view|section-view' }) -join ' | '
                } else { '<none>' }
                throw "The browser section-view lifecycle did not complete without ActiViz. Trace: $sectionDiagnostic"
            }
            $enabledSectionLines = $trace | Where-Object { $_ -match 'browser section view applied enabled=True' }
            $sectionCapActors = ($enabledSectionLines | ForEach-Object {
                if ($_ -match 'capActors=([0-9]+)') { [int]$Matches[1] }
            } | Measure-Object -Maximum).Maximum
            $sectionSkippedCells = ($enabledSectionLines | ForEach-Object {
                if ($_ -match 'skippedCells=([0-9]+)') { [int]$Matches[1] }
            } | Measure-Object -Maximum).Maximum
        }
    }

    Start-Sleep -Seconds 2
    for ($attempt = 0; $attempt -lt 20 -and -not $prePoMaxProcess.HasExited; $attempt++) {
        $prePoMaxProcess.Refresh()
        $prePoMaxProcess.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 100
        Invoke-CloseConfirmation $prePoMaxProcess.Id | Out-Null
        $prePoMaxProcess.WaitForExit(500) | Out-Null
    }
    if (-not $prePoMaxProcess.HasExited) {
        $prePoMaxProcess.Refresh()
        throw "PrePoMax did not close after WM_CLOSE. Window='$($prePoMaxProcess.MainWindowTitle)', handle=$($prePoMaxProcess.MainWindowHandle)."
    }

    $rendererExited = $false
    for ($attempt = 0; $attempt -lt 50 -and -not $rendererExited; $attempt++) {
        $rendererExited = $null -eq (Get-Process -Id $rendererProcessId -ErrorAction SilentlyContinue)
        if (-not $rendererExited) { Start-Sleep -Milliseconds 100 }
    }
    if (-not $rendererExited) { throw 'The renderer child remained alive after FrmMain closed.' }

    if (Test-Path $traceFile) {
        $locatorLine = Select-String -Path $traceFile -Pattern 'publishing topology .*locatorPoints=[0-9]+ locatorCells=[0-9]+' |
            Select-Object -Last 1
        if ($null -ne $locatorLine -and $locatorLine.Line -match 'locatorPoints=([0-9]+) locatorCells=([0-9]+)') {
            $locatorPoints = [long]$Matches[1]
            $locatorCells = [long]$Matches[2]
            if ($ExerciseAnimation) { $animationHostFrameBytes += 12L * $locatorPoints }
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($ModelPath) -and ($locatorPoints -le 0 -or $locatorCells -le 0)) {
        throw 'The real model snapshot did not include full locator topology.'
    }

    [pscustomobject]@{
        PrePoMaxStarted = $true
        RendererChildStarted = $true
        WebView2RuntimeObserved = $true
        GracefulClose = $true
        RendererChildExited = $true
        UploadedActors = if ($null -eq $uploadedScene) { 0 } else { $uploadedScene.actors.Count }
        UploadedPoints = if ($null -eq $uploadedScene) { 0 } else { ($uploadedScene.actors.chunks.pointCount | Measure-Object -Sum).Sum }
        UploadedTriangles = if ($null -eq $uploadedScene) { 0 } else { ($uploadedScene.actors.chunks.triangleCount | Measure-Object -Sum).Sum }
        LocatorPoints = $locatorPoints
        LocatorCells = $locatorCells
        UploadedVisibleActors = if ($null -eq $uploadedScene) { 0 } else { @($uploadedScene.actors | Where-Object visible).Count }
        HostCameraForwarded = if ([string]::IsNullOrWhiteSpace($ModelPath)) { $false } else { $cameraForwarded }
        RetainedCameraRoundtrip = $cameraStateRetained
        BrowserBoxZoomApplied = $boxZoomApplied
        BrowserVisibilityCameraApplied = $visibilityCameraApplied
        ScalarMinimum = if ($null -eq $uploadedScene) { $null } else { $uploadedScene.field.validRange[0] }
        ScalarMaximum = if ($null -eq $uploadedScene) { $null } else { $uploadedScene.field.validRange[1] }
        FieldName = if ($null -eq $uploadedScene) { $null } else { $uploadedScene.field.name }
        FieldComponent = if ($null -eq $uploadedScene) { $null } else { $uploadedScene.field.component }
        FieldUnits = if ($null -eq $uploadedScene) { $null } else { $uploadedScene.field.units }
        ScalarOnlyFieldSwitch = $scalarOnlySwitch
        SwitchedSceneRevision = if ($null -eq $switchedScene) { $null } else { $switchedScene.sceneRevision }
        ValidatedBrowserPickApplied = $validatedPickApplied
        PickItem = if ($ExercisePick) { $PickItem } else { $null }
        SelectedIds = $selectedIds
        VisibleSelectionOverlay = $selectionOverlayVisible
        ServerFrustumApplied = $serverFrustumApplied
        VisibleAnnotation = $annotationVisible
        VisibleExtrema = $extremaVisible
        AnnotationInteractionApplied = $annotationInteractionApplied
        ExplodedPreviewApplied = $explodedPreviewApplied
        SectionViewApplied = $sectionViewApplied
        SectionCapActors = $sectionCapActors
        SectionSkippedCells = $sectionSkippedCells
        LargeSelectionCount = $largeSelectionCount
        AnimationFramePublished = $animationFramePublished
        AnimationMode = if ($ExerciseAnimation) { $AnimationMode } else { $null }
        AnimationFrames = if ($ExerciseAnimation) { $animationFrames } else { 0 }
        AnimationFramesPublished = $animationFramesPublished
        AnimationHostFrameBytes = if ($ExerciseAnimation) { $animationHostFrameBytes } else { 0 }
        BrowserFrameSamples = $browserFrameSamples.ToArray()
        HostFrameSamples = $hostFrameSamples.ToArray()
        CaptureFrameSamples = $captureFrameSamples.ToArray()
        BrowserMedianActivationMs = if ($browserFrameSamples.Count -eq 0) { $null } else {
            $sortedBrowserLatencies = @($browserFrameSamples.TotalMs | Sort-Object)
            $sortedBrowserLatencies[[int][Math]::Floor($sortedBrowserLatencies.Count / 2)]
        }
        AnimationMedianLatencyMs = if ($animationFrameLatencies.Count -eq 0) { $null } else {
            $sortedLatencies = @($animationFrameLatencies | Sort-Object)
            $sortedLatencies[[int][Math]::Floor($sortedLatencies.Count / 2)]
        }
    }
}
finally {
    if ($null -ne $rendererProcessId) {
        $rendererProcess = Get-Process -Id $rendererProcessId -ErrorAction SilentlyContinue
        if ($null -ne $rendererProcess) { $rendererProcess.Kill() }
    }
    if ($null -ne $prePoMaxProcess -and -not $prePoMaxProcess.HasExited) {
        $prePoMaxProcess.Kill()
        $prePoMaxProcess.WaitForExit(2000) | Out-Null
    }
    Remove-Item -LiteralPath $traceFile -Force -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_RENDER_TRACE_FILE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_FIELD -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_FIELD_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_PICK_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_PICK_ITEM -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANNOTATION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_EXTREMA -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANNOTATION_INTERACTION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_LARGE_SELECTION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_CAMERA -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_BOX_ZOOM -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_VISIBILITY_CAMERA -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_EXPLODED_PREVIEW -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SECTION_VIEW -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SELECTION_CAPACITY -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_SEQUENCE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_FRAME_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fieldSwitchTrigger -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $pickTrigger -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $animationTrigger -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $animationFrameTrigger -Force -ErrorAction SilentlyContinue
}
