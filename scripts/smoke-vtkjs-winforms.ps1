param(
    [string]$ModelPath,
    [switch]$ExpectFiniteScalars,
    [string]$SwitchField,
    [switch]$ExercisePick,
    [switch]$ExerciseAnnotation,
    [switch]$ExerciseExtrema,
    [switch]$ExerciseAnnotationInteraction,
    [switch]$ExerciseAnnotationSettings,
    [switch]$ExerciseLargeSelection,
    [switch]$ExerciseCameraState,
    [switch]$ExerciseBoxZoom,
    [switch]$ExerciseVisibilityCamera,
    [switch]$ExerciseTransformations,
    [switch]$ExerciseExplodedPreview,
    [switch]$ExerciseSectionView,
    [switch]$ExerciseSphereSymbol,
    [switch]$ExerciseCoordinateOverlay,
    [switch]$ExerciseDirectionalGlyph,
    [switch]$ExerciseGeneralConeGlyph,
    [switch]$ExerciseRotationalGlyph,
    [switch]$ExerciseArrowGlyph,
    [switch]$ExerciseDoubleArrowGlyph,
    [switch]$ExerciseSpringGlyph,
    [switch]$ExerciseThermalGlyph,
    [switch]$ExerciseFluxGlyph,
    [switch]$ExerciseViewportAppearance,
    [switch]$ExerciseOrientationMarker,
    [switch]$ExerciseScaleWidget,
    [switch]$ExerciseScalarLegend,
    [switch]$ExerciseColorBar,
    [switch]$ExerciseStatusBlock,
    [switch]$ExerciseActorPresentation,
    [switch]$ExerciseActorHighlight,
    [switch]$ExerciseSceneClear,
    [switch]$ExerciseCorePartConstruction,
    [switch]$ExerciseThreeMfExportInput,
    [switch]$ExerciseStaticScalarConstruction,
    [switch]$ExerciseSelectionSurfaceConstruction,
    [switch]$ExerciseSelectionPrimitiveConstruction,
    [switch]$ExerciseAuxiliaryConstruction,
    [switch]$ExerciseGenericConstruction,
    [switch]$ExerciseCellClassification,
    [ValidateRange(0, 8000000)]
    [int]$SelectionCapacityIds = 0,
    [switch]$ExerciseAnimation,
    [switch]$ExerciseAnimationPngCapture,
    [switch]$ExerciseAnimationAviCapture,
    [switch]$ExerciseFollowerRedraw,
    [switch]$MeasureAnimationCadence,
    [ValidateSet('ScaleFactor', 'TimeIncrements', 'Harmonic')]
    [string]$AnimationMode = 'ScaleFactor',
    [ValidateSet('Element', 'Surface', 'GeometryEdge', 'Geometry', 'GeometryPoint')]
    [string]$PickItem = 'Element'
)

$ErrorActionPreference = 'Stop'
if ($ExerciseFollowerRedraw -and -not $ExerciseAnimation) {
    throw 'ExerciseFollowerRedraw requires ExerciseAnimation.'
}
if ($ExerciseAnimationPngCapture -and -not $ExerciseAnimation) {
    throw 'ExerciseAnimationPngCapture requires ExerciseAnimation.'
}
if ($ExerciseAnimationAviCapture -and -not $ExerciseAnimation) {
    throw 'ExerciseAnimationAviCapture requires ExerciseAnimation.'
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$executable = Resolve-Path (Join-Path $repositoryRoot 'PrePoMax\bin\x64\Release\PrePoMax.exe')
$env:PREPOMAX_RENDERER = 'vtkjs'
$hostTokenBytes = New-Object byte[] 32
$hostTokenGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $hostTokenGenerator.GetBytes($hostTokenBytes) }
finally { $hostTokenGenerator.Dispose() }
$smokeHostToken = [Convert]::ToBase64String($hostTokenBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')
$env:PREPOMAX_VTKJS_HOST_TOKEN_OVERRIDE = $smokeHostToken
$traceFile = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-smoke-{0}.log" -f [Guid]::NewGuid().ToString('N'))
$fieldSwitchTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-field-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
$pickTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-pick-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
$animationTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-animation-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
$animationFrameTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-animation-frame-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
$sceneClearTrigger = Join-Path ([System.IO.Path]::GetTempPath()) ("prepomax-vtkjs-scene-clear-{0}.trigger" -f [Guid]::NewGuid().ToString('N'))
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
if ($ExerciseAnnotationSettings) { $env:PREPOMAX_VTKJS_SMOKE_ANNOTATION_SETTINGS = '1' }
if ($ExerciseLargeSelection) { $env:PREPOMAX_VTKJS_SMOKE_LARGE_SELECTION = '1' }
if ($ExerciseCameraState) { $env:PREPOMAX_VTKJS_SMOKE_CAMERA = '1' }
if ($ExerciseBoxZoom) { $env:PREPOMAX_VTKJS_SMOKE_BOX_ZOOM = '1' }
if ($ExerciseVisibilityCamera) { $env:PREPOMAX_VTKJS_SMOKE_VISIBILITY_CAMERA = '1' }
if ($ExerciseTransformations) { $env:PREPOMAX_VTKJS_SMOKE_TRANSFORMATIONS = '1' }
if ($ExerciseExplodedPreview) { $env:PREPOMAX_VTKJS_SMOKE_EXPLODED_PREVIEW = '1' }
if ($ExerciseSectionView) { $env:PREPOMAX_VTKJS_SMOKE_SECTION_VIEW = '1' }
if ($ExerciseSphereSymbol) { $env:PREPOMAX_VTKJS_SMOKE_SPHERE_SYMBOL = '1' }
if ($ExerciseCoordinateOverlay) { $env:PREPOMAX_VTKJS_SMOKE_COORDINATE_OVERLAY = '1' }
if ($ExerciseDirectionalGlyph) { $env:PREPOMAX_VTKJS_SMOKE_DIRECTIONAL_GLYPH = '1' }
if ($ExerciseGeneralConeGlyph) { $env:PREPOMAX_VTKJS_SMOKE_GENERAL_CONE_GLYPH = '1' }
if ($ExerciseRotationalGlyph) { $env:PREPOMAX_VTKJS_SMOKE_ROTATIONAL_GLYPH = '1' }
if ($ExerciseArrowGlyph) { $env:PREPOMAX_VTKJS_SMOKE_ARROW_GLYPH = '1' }
if ($ExerciseDoubleArrowGlyph) { $env:PREPOMAX_VTKJS_SMOKE_DOUBLE_ARROW_GLYPH = '1' }
if ($ExerciseSpringGlyph) { $env:PREPOMAX_VTKJS_SMOKE_SPRING_GLYPH = '1' }
if ($ExerciseThermalGlyph) { $env:PREPOMAX_VTKJS_SMOKE_THERMAL_GLYPH = '1' }
if ($ExerciseFluxGlyph) { $env:PREPOMAX_VTKJS_SMOKE_FLUX_GLYPH = '1' }
if ($ExerciseViewportAppearance) { $env:PREPOMAX_VTKJS_SMOKE_VIEWPORT_APPEARANCE = '1' }
if ($ExerciseOrientationMarker) { $env:PREPOMAX_VTKJS_SMOKE_ORIENTATION_MARKER = '1' }
if ($ExerciseScaleWidget) { $env:PREPOMAX_VTKJS_SMOKE_SCALE_WIDGET = '1' }
if ($ExerciseScalarLegend) { $env:PREPOMAX_VTKJS_SMOKE_SCALAR_LEGEND = '1' }
if ($ExerciseColorBar) { $env:PREPOMAX_VTKJS_SMOKE_COLOR_BAR = '1' }
if ($ExerciseStatusBlock) { $env:PREPOMAX_VTKJS_SMOKE_STATUS_BLOCK = '1' }
if ($ExerciseActorPresentation) { $env:PREPOMAX_VTKJS_SMOKE_ACTOR_PRESENTATION = '1' }
if ($ExerciseActorHighlight) { $env:PREPOMAX_VTKJS_SMOKE_ACTOR_HIGHLIGHT = '1' }
if ($ExerciseSceneClear) {
    $env:PREPOMAX_VTKJS_SMOKE_SCENE_CLEAR = '1'
    $env:PREPOMAX_VTKJS_SMOKE_SCENE_CLEAR_TRIGGER = $sceneClearTrigger
}
if ($ExerciseThreeMfExportInput) { $env:PREPOMAX_VTKJS_SMOKE_3MF_EXPORT_INPUT = '1' }
if ($ExerciseStaticScalarConstruction) { $env:PREPOMAX_VTKJS_SMOKE_STATIC_SCALAR_CONSTRUCTION = '1' }
if ($ExerciseSelectionSurfaceConstruction) { $env:PREPOMAX_VTKJS_SMOKE_SELECTION_SURFACE = '1' }
if ($ExerciseSelectionPrimitiveConstruction) { $env:PREPOMAX_VTKJS_SMOKE_SELECTION_PRIMITIVES = '1' }
if ($ExerciseAuxiliaryConstruction) { $env:PREPOMAX_VTKJS_SMOKE_AUXILIARY_CONSTRUCTION = '1' }
if ($ExerciseGenericConstruction) { $env:PREPOMAX_VTKJS_SMOKE_GENERIC_CONSTRUCTION = '1' }
if ($ExerciseCellClassification) { $env:PREPOMAX_VTKJS_SMOKE_CELL_CLASSIFICATION = '1' }
if ($SelectionCapacityIds -gt 0) { $env:PREPOMAX_VTKJS_SMOKE_SELECTION_CAPACITY = $SelectionCapacityIds.ToString() }
if ($ExerciseAnimation) {
    $env:PREPOMAX_VTKJS_SMOKE_ANIMATION = '1'
    $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_MODE = $AnimationMode
    $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_TRIGGER = $animationTrigger
    $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_FRAME_TRIGGER = $animationFrameTrigger
    if ($MeasureAnimationCadence) { $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_SEQUENCE = '1' }
    if ($ExerciseFollowerRedraw) { $env:PREPOMAX_VTKJS_SMOKE_FOLLOWER_REDRAW = '1' }
    if ($ExerciseAnimationPngCapture) { $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_PNG_CAPTURE = '1' }
    if ($ExerciseAnimationAviCapture) { $env:PREPOMAX_VTKJS_SMOKE_ANIMATION_AVI_CAPTURE = '1' }
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
$annotationSettingsApplied = $false
$largeSelectionCount = 0
$cameraStateRetained = $false
$boxZoomApplied = $false
$visibilityCameraApplied = $false
$transformationsBrowserOnly = $false
$explodedPreviewApplied = $false
$sectionViewApplied = $false
$sphereSymbolApplied = $false
$coordinateOverlayApplied = $false
$directionalGlyphApplied = $false
$generalConeGlyphApplied = $false
$rotationalGlyphApplied = $false
$arrowGlyphApplied = $false
$doubleArrowGlyphApplied = $false
$springGlyphApplied = $false
$thermalGlyphApplied = $false
$fluxGlyphApplied = $false
$viewportAppearanceApplied = $false
$orientationMarkerBrowserOnly = $false
$scaleWidgetBrowserOnly = $false
$scalarLegendBrowserOnly = $false
$colorBarBrowserOnly = $false
$statusBlockBrowserOnly = $false
$actorPresentationApplied = $false
$actorHighlightApplied = $false
$sceneClearApplied = $false
$corePartConstructionBrowserOnly = $false
$threeMfExportRetainedInput = $false
$staticScalarConstructionBrowserOnly = $false
$selectionSurfaceConstructionBrowserOnly = $false
$selectionPrimitiveConstructionBrowserOnly = $false
$auxiliaryConstructionBrowserOnly = $false
$genericConstructionBrowserOnly = $false
$cellClassificationBrowserOnly = $false
$sectionCapActors = 0
$sectionSkippedCells = 0
$animationFramePublished = $false
$animationFrameActiVizBypassed = $false
$animationConstructionBrowserOnly = $false
$animationExportAdapterMaterialized = $false
$animationPngCaptureBrowserOnly = $false
$animationAviCaptureBrowserOnly = $false
$followerRedrawApplied = $false
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
function Invoke-AnnotationSettingsDialog([int]$ProcessId) {
    try {
        $processCondition = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
        $windows = [Windows.Automation.AutomationElement]::RootElement.FindAll(
            [Windows.Automation.TreeScope]::Descendants, $processCondition)
        foreach ($window in $windows) {
            if ($window.Current.Name -ne 'Edit Settings') { continue }
            $cancelCondition = New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::NameProperty, 'Cancel')
            $cancel = $window.FindFirst([Windows.Automation.TreeScope]::Descendants, $cancelCondition)
            if ($null -eq $cancel) { continue }
            $invoke = $cancel.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
            if ($null -eq $invoke) { continue }
            $invoke.Invoke()
            return $true
        }
    }
    catch [Windows.Automation.ElementNotAvailableException] { return $false }
    catch [Windows.Automation.ElementNotEnabledException] { return $false }
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
        $session = Invoke-RestMethod -Method Post "$baseUrl/api/v1/sessions" `
            -Headers @{ 'X-PrePoMax-Host-Token' = $smokeHostToken }
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
        if ($ExerciseSceneClear) {
            New-Item -ItemType File -Path $sceneClearTrigger -Force | Out-Null
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
            $animationConstructionBrowserOnly =
                (Select-String -Path $traceFile -Pattern 'compatibility bypass capability=result-animation operation=construction-browser-only' -Quiet) -and
                -not (Select-String -Path $traceFile -Pattern 'compatibility bypass capability=result-animation operation=construction-rejected' -Quiet) -and
                (Select-String -Path $traceFile -Pattern 'acceptance: animation construction browserOnly actors=[1-9][0-9]* legacyMaterialized=False' -Quiet)
            if (-not $animationConstructionBrowserOnly) {
                throw 'Animated result construction did not remain browser-only before export materialization.'
            }
            if ($ExerciseAnimationPngCapture) {
                for ($attempt = 0; $attempt -lt 160 -and -not $animationPngCaptureBrowserOnly; $attempt++) {
                    Start-Sleep -Milliseconds 250
                    $setupFailure = Select-String -Path $traceFile -Pattern 'acceptance: animation setup failed:' |
                        Select-Object -Last 1
                    if ($null -ne $setupFailure) { throw $setupFailure.Line }
                    $animationPngCaptureBrowserOnly =
                        (Select-String -Path $traceFile -Pattern 'compatibility bypass capability=result-animation operation=images-browser-only' -Quiet) -and
                        (Select-String -Path $traceFile -Pattern 'acceptance: browser animation images=2 bytes=[1-9][0-9]* width=[1-9][0-9]* height=[1-9][0-9]*' -Quiet) -and
                        (Select-String -Path $traceFile -Pattern 'acceptance: animation export images=2 bytes=[1-9][0-9]* distinct=True' -Quiet) -and
                        ((Select-String -Path $traceFile -Pattern 'browser PNG captured bytes=[1-9][0-9]* width=[1-9][0-9]* height=[1-9][0-9]*').Count -eq 2)
                }
                if (-not $animationPngCaptureBrowserOnly) {
                    throw 'The browser did not capture, validate, and write both PNG animation frames.'
                }
                if (Select-String -Path $traceFile -Pattern 'compatibility bypass capability=result-animation operation=export-legacy-materialized' -Quiet) {
                    throw 'Browser PNG capture unexpectedly materialized the legacy animation export adapter.'
                }
            }
            if ($ExerciseAnimationAviCapture) {
                for ($attempt = 0; $attempt -lt 160 -and -not $animationAviCaptureBrowserOnly; $attempt++) {
                    Start-Sleep -Milliseconds 250
                    $setupFailure = Select-String -Path $traceFile -Pattern 'acceptance: animation setup failed:' |
                        Select-Object -Last 1
                    if ($null -ne $setupFailure) { throw $setupFailure.Line }
                    $animationAviCaptureBrowserOnly =
                        (Select-String -Path $traceFile -Pattern 'compatibility bypass capability=result-animation operation=avi-browser-only' -Quiet) -and
                        (Select-String -Path $traceFile -Pattern 'acceptance: browser animation avi frames=2 jpegBytes=[1-9][0-9]* fileBytes=[1-9][0-9]* width=[1-9][0-9]* height=[1-9][0-9]* fps=12 codec=MJPG' -Quiet) -and
                        (Select-String -Path $traceFile -Pattern 'acceptance: animation export avi frames=2 bytes=[1-9][0-9]* riff=True mjpg=True indexed=True distinct=True' -Quiet) -and
                        ((Select-String -Path $traceFile -Pattern 'browser JPEG captured bytes=[1-9][0-9]* width=[1-9][0-9]* height=[1-9][0-9]*').Count -eq 2)
                }
                if (-not $animationAviCaptureBrowserOnly) {
                    throw 'The browser did not capture and encode a valid indexed two-frame Motion JPEG AVI.'
                }
                if (Select-String -Path $traceFile -Pattern 'compatibility bypass capability=result-animation operation=export-legacy-materialized' -Quiet) {
                    throw 'Browser AVI capture unexpectedly materialized the legacy animation export adapter.'
                }
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
                        (Select-String -Path $traceFile -Pattern "compatibility bypass capability=camera operation=animation-frame-adjust-$targetFrame" -Quiet) -and
                        (Select-String -Path $traceFile -Pattern 'compatibility bypass capability=result-animation operation=frame-browser-only' -Quiet)
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
                    'chunks=([0-9]+) bytes=([0-9]+) downloadMs=([0-9]+(?:\.[0-9]+)?) applyMs=([0-9]+(?:\.[0-9]+)?) paintMs=([0-9]+(?:\.[0-9]+)?) totalMs=([0-9]+(?:\.[0-9]+)?) followerAdjusted=(?:True|False) cameraAdjusted=True') {
                    $browserDiagnostic = if ($null -eq $browserLine) { '<missing>' } else { $browserLine.Line }
                    throw "The browser did not report stable activation for scene revision $browserRevision. Telemetry: $browserDiagnostic"
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
                if ($ExerciseFollowerRedraw -and $browserLine.Line -notmatch 'followerAdjusted=True') {
                    throw "The physical follower did not adjust the camera for animation frame $targetFrame."
                }
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
                if ($captureFrameSamples[$captureFrameSamples.Count - 1].LegacyActivationMs -ne 0) {
                    throw "Hidden ActiViz performed animation-frame work for frame $targetFrame."
                }
                $animationFrameActiVizBypassed = $true
                $animationFrameLatencies.Add($frameTimer.Elapsed.TotalMilliseconds)
                $animationFramesPublished++
                $previousScene = $animationScene
                $previousPositionHashes = $animationPositionHashes
            }
            $animationFramePublished = $animationFramesPublished -eq $targetFrames.Count
            if ($ExerciseFollowerRedraw) {
                $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
                $followerRedrawApplied =
                    ($trace -match 'acceptance: physical follower requested points=3').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=follower-apply').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=camera operation=follower-update').Count -gt 0 -and
                    ($trace -match 'browser frame activated .*followerAdjusted=True cameraAdjusted=True').Count -gt 0
                if (-not $followerRedrawApplied) {
                    $followerDiagnostic = ($trace | Where-Object { $_ -match 'follower|frame activated' } |
                        Select-Object -Last 35) -join ' | '
                    throw "The physical follower redraw did not traverse the controller and browser paths. Trace: $followerDiagnostic"
                }
            }
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
        if ($ExerciseAnnotationSettings) {
            for ($attempt = 0; $attempt -lt 80 -and -not $annotationSettingsApplied; $attempt++) {
                Start-Sleep -Milliseconds 250
                $settingsTraceApplied = (Test-Path $traceFile) -and
                    (Select-String -Path $traceFile -Pattern "acceptance: annotation action applied name='R_Annotation[^']+' action=settings" -Quiet) -and
                    (Select-String -Path $traceFile -Pattern "acceptance: annotation settings dialog visible=True selected='Annotations'" -Quiet)
                if ($settingsTraceApplied) {
                    $annotationSettingsApplied = Invoke-AnnotationSettingsDialog -ProcessId $prePoMaxProcess.Id
                }
            }
            if (-not $annotationSettingsApplied) {
                $settingsDiagnostic = if (Test-Path $traceFile) { (Get-Content $traceFile -Tail 35) -join ' | ' } else { '<none>' }
                throw "The annotation Settings action did not open the real dialog with Annotations selected. Trace: $settingsDiagnostic"
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
                    ($trace -match 'compatibility bypass capability=actor-presentation operation=visibility-hide').Count -gt 0 -and
                    ($trace -match 'compatibility bypass capability=actor-presentation operation=visibility-show').Count -gt 0 -and
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

    if ($ExerciseSphereSymbol) {
        for ($attempt = 0; $attempt -lt 240 -and -not $sphereSymbolApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $sphereSymbolApplied =
                ($trace -match 'compatibility bypass capability=sphere-symbols operation=add').Count -gt 0 -and
                ($trace -match 'sphere symbol state actors=1 points=1').Count -gt 0 -and
                ($trace -match 'acceptance: sphere symbols 1\|1\|true').Count -gt 0
        }
        if (-not $sphereSymbolApplied) {
            $sphereDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'sphere symbol|sphere-symbol' }) -join ' | '
            } else { '<none>' }
            throw "The browser sphere symbol was not rendered without ActiViz. Trace: $sphereDiagnostic"
        }
    }

    if ($ExerciseCoordinateOverlay) {
        for ($attempt = 0; $attempt -lt 240 -and -not $coordinateOverlayApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $coordinateOverlayApplied =
                ($trace -match 'compatibility bypass capability=coordinate-axis operation=add').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=caption operation=add').Count -gt 0 -and
                ($trace -match 'coordinate overlay state axes=1 captions=1').Count -gt 0 -and
                ($trace -match 'acceptance: coordinate overlays 1\|true\|1\|true').Count -gt 0
        }
        if (-not $coordinateOverlayApplied) {
            $coordinateOverlayDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'coordinate overlay|coordinate-axis|caption' }) -join ' | '
            } else { '<none>' }
            throw "The browser coordinate overlay was not rendered without ActiViz. Trace: $coordinateOverlayDiagnostic"
        }
    }

    if ($ExerciseDirectionalGlyph) {
        for ($attempt = 0; $attempt -lt 240 -and -not $directionalGlyphApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $directionalGlyphApplied =
                ($trace -match 'compatibility bypass capability=displacement-constraint-cones operation=add').Count -gt 0 -and
                ($trace -match 'directional glyph state actors=1 points=2').Count -gt 0 -and
                ($trace -match 'acceptance: directional glyphs 1\|2\|true').Count -gt 0
        }
        if (-not $directionalGlyphApplied) {
            $directionalGlyphDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'directional glyph|displacement-constraint' }) -join ' | '
            } else { '<none>' }
            throw "The browser displacement-constraint glyph was not rendered without ActiViz. Trace: $directionalGlyphDiagnostic"
        }
    }

    if ($ExerciseRotationalGlyph) {
        for ($attempt = 0; $attempt -lt 240 -and -not $rotationalGlyphApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $rotationalGlyphApplied =
                ($trace -match 'compatibility bypass capability=rotational-constraint operation=add').Count -gt 0 -and
                ($trace -match 'directional glyph state actors=1 points=2').Count -gt 0 -and
                ($trace -match 'acceptance: directional glyphs 1\|2\|true').Count -gt 0
        }
        if (-not $rotationalGlyphApplied) {
            $rotationalGlyphDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'directional glyph|rotational-constraint' }) -join ' | '
            } else { '<none>' }
            throw "The browser rotational-constraint glyph was not rendered without ActiViz. Trace: $rotationalGlyphDiagnostic"
        }
    }

    if ($ExerciseGeneralConeGlyph) {
        for ($attempt = 0; $attempt -lt 240 -and -not $generalConeGlyphApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $generalConeGlyphApplied =
                ($trace -match 'compatibility bypass capability=general-cone operation=add').Count -gt 0 -and
                ($trace -match 'directional glyph state actors=1 points=2').Count -gt 0 -and
                ($trace -match 'acceptance: directional glyphs 1\|2\|true').Count -gt 0
        }
        if (-not $generalConeGlyphApplied) {
            $generalConeGlyphDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'directional glyph|general-cone|general cone' }) -join ' | '
            } else { '<none>' }
            throw "The browser general-cone glyph was not rendered without ActiViz. Trace: $generalConeGlyphDiagnostic"
        }
    }

    if ($ExerciseArrowGlyph) {
        for ($attempt = 0; $attempt -lt 240 -and -not $arrowGlyphApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $arrowGlyphApplied =
                ($trace -match 'compatibility bypass capability=oriented-arrow operation=add').Count -gt 0 -and
                ($trace -match 'directional glyph state actors=1 points=2').Count -gt 0 -and
                ($trace -match 'acceptance: directional glyphs 1\|2\|true').Count -gt 0
        }
        if (-not $arrowGlyphApplied) {
            $arrowGlyphDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'directional glyph|oriented-arrow|arrow glyph' }) -join ' | '
            } else { '<none>' }
            throw "The browser oriented-arrow glyph was not rendered without ActiViz. Trace: $arrowGlyphDiagnostic"
        }
    }

    if ($ExerciseDoubleArrowGlyph) {
        for ($attempt = 0; $attempt -lt 240 -and -not $doubleArrowGlyphApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $doubleArrowGlyphApplied =
                ($trace -match 'compatibility bypass capability=double-arrow operation=add').Count -gt 0 -and
                ($trace -match 'directional glyph state actors=1 points=2').Count -gt 0 -and
                ($trace -match 'acceptance: directional glyphs 1\|2\|true').Count -gt 0
        }
        if (-not $doubleArrowGlyphApplied) {
            $doubleArrowGlyphDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'directional glyph|double-arrow|double arrow' }) -join ' | '
            } else { '<none>' }
            throw "The browser double-arrow glyph was not rendered without ActiViz. Trace: $doubleArrowGlyphDiagnostic"
        }
    }

    if ($ExerciseSpringGlyph) {
        for ($attempt = 0; $attempt -lt 240 -and -not $springGlyphApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $springGlyphApplied =
                ($trace -match 'compatibility bypass capability=oriented-spring operation=add').Count -gt 0 -and
                ($trace -match 'directional glyph state actors=1 points=2').Count -gt 0 -and
                ($trace -match 'acceptance: directional glyphs 1\|2\|true').Count -gt 0
        }
        if (-not $springGlyphApplied) {
            $springGlyphDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'directional glyph|oriented-spring|spring glyph' }) -join ' | '
            } else { '<none>' }
            throw "The browser oriented-spring glyph was not rendered without ActiViz. Trace: $springGlyphDiagnostic"
        }
    }

    if ($ExerciseThermalGlyph) {
        for ($attempt = 0; $attempt -lt 240 -and -not $thermalGlyphApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $thermalGlyphApplied =
                ($trace -match 'compatibility bypass capability=oriented-thermal operation=add').Count -gt 0 -and
                ($trace -match 'directional glyph state actors=1 points=2').Count -gt 0 -and
                ($trace -match 'acceptance: directional glyphs 1\|2\|true').Count -gt 0
        }
        if (-not $thermalGlyphApplied) {
            $thermalGlyphDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'directional glyph|oriented-thermal|thermal glyph' }) -join ' | '
            } else { '<none>' }
            throw "The browser thermal glyph was not rendered without ActiViz. Trace: $thermalGlyphDiagnostic"
        }
    }

    if ($ExerciseFluxGlyph) {
        for ($attempt = 0; $attempt -lt 240 -and -not $fluxGlyphApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $fluxGlyphApplied =
                ($trace -match 'compatibility bypass capability=oriented-flux operation=add').Count -gt 0 -and
                ($trace -match 'directional glyph state actors=1 points=2').Count -gt 0 -and
                ($trace -match 'acceptance: directional glyphs 1\|2\|true').Count -gt 0
        }
        if (-not $fluxGlyphApplied) {
            $fluxGlyphDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'directional glyph|oriented-flux|flux glyph' }) -join ' | '
            } else { '<none>' }
            throw "The browser flux glyph was not rendered without ActiViz. Trace: $fluxGlyphDiagnostic"
        }
    }

    if ($ExerciseViewportAppearance) {
        for ($attempt = 0; $attempt -lt 240 -and -not $viewportAppearanceApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $viewportAppearanceApplied =
                ($trace -match 'compatibility bypass capability=viewport-appearance operation=set-background').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=viewport-appearance operation=set-lighting').Count -gt 0 -and
                ($trace -match 'viewport appearance gradient=True ambient=0.35 diffuse=0.65').Count -gt 0 -and
                ($trace -match 'acceptance: viewport appearance true\|0.047059,0.133333,0.219608\|0.305882,0.352941,0.482353\|0.35\|0.65\|true').Count -gt 0
        }
        if (-not $viewportAppearanceApplied) {
            $viewportAppearanceDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'viewport appearance|viewport-appearance' }) -join ' | '
            } else { '<none>' }
            throw "The browser viewport appearance was not applied without ActiViz. Trace: $viewportAppearanceDiagnostic"
        }
    }

    if ($ExerciseActorPresentation) {
        for ($attempt = 0; $attempt -lt 240 -and -not $actorPresentationApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $presentationServerApplied = $false
            try {
                $presentationScenes = Invoke-RestMethod "$baseUrl/api/v1/scenes" -Headers $headers
                if ($presentationScenes.Count -gt 0) {
                    $presentationManifest = Invoke-RestMethod "$baseUrl/api/v1/scenes/$($presentationScenes[0].sceneId)/manifest" -Headers $headers
                    $presentationActor = @($presentationManifest.actors | Where-Object {
                        $_.name -match ' \[VTK\.js presentation smoke\]$'
                    }) | Select-Object -First 1
                    if ($null -ne $presentationActor) {
                        $rgba = @($presentationActor.material.frontRgba)
                        $presentationServerApplied = $rgba.Count -eq 4 -and
                            [Math]::Abs([double]$rgba[0] - (32.0 / 255.0)) -lt 0.000001 -and
                            [Math]::Abs([double]$rgba[1] - (96.0 / 255.0)) -lt 0.000001 -and
                            [Math]::Abs([double]$rgba[2] - (160.0 / 255.0)) -lt 0.000001 -and
                            [Math]::Abs([double]$rgba[3] - 1.0) -lt 0.000001
                    }
                }
            } catch { }
            $actorPresentationApplied =
                ($trace -match 'acceptance: actor registry True\|True\|True').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=actor-presentation operation=contains-query').Count -ge 3 -and
                ($trace -match 'compatibility bypass capability=actor-presentation operation=update').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=actor-presentation operation=contours').Count -gt 0 -and
                ($trace -match 'browser scene applied scene=.* actors=1 chunks=1').Count -gt 0 -and
                ($trace -match 'acceptance: actor presentation .* \[VTK.js presentation smoke\]\|0.125490,0.376471,0.627451\|1.00\|true\|false').Count -gt 0 -and
                $presentationServerApplied
        }
        if (-not $actorPresentationApplied) {
            $actorPresentationDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Select-Object -Last 100) -join ' | '
            } else { '<none>' }
            throw "Actor registry, presentation, and contour state did not bypass ActiViz. Trace: $actorPresentationDiagnostic"
        }
    }

    if ($ExerciseActorHighlight) {
        for ($attempt = 0; $attempt -lt 240 -and -not $actorHighlightApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $actorHighlightApplied =
                ($trace -match 'compatibility bypass capability=actor-highlight operation=add').Count -gt 0 -and
                ($trace -match 'actor highlight state actors=1').Count -gt 0 -and
                ($trace -match 'acceptance: selection state populated part\|1\|[1-9][0-9]*').Count -gt 0 -and
                ($trace -match 'acceptance: actor highlights added 1\|[1-9][0-9]*').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=selection-lifecycle operation=clear').Count -gt 0 -and
                ($trace -match 'acceptance: selection state cleared none\|0\|0').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=actor-highlight operation=clear').Count -gt 0 -and
                ($trace -match 'actor highlight state actors=0').Count -gt 0 -and
                ($trace -match 'acceptance: actor highlights cleared 0\|0').Count -gt 0
        }
        if (-not $actorHighlightApplied) {
            $actorHighlightDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Where-Object { $_ -match 'actor highlight|actor-highlight' }) -join ' | '
            } else { '<none>' }
            throw "Actor highlight add/clear did not bypass ActiViz. Trace: $actorHighlightDiagnostic"
        }
    }

    if ($ExerciseSceneClear) {
        for ($attempt = 0; $attempt -lt 240 -and -not $sceneClearApplied; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $sceneClearApplied =
                ($trace -match 'acceptance: scene clear requested').Count -gt 0 -and
                ($trace -match 'publishing topology generation=.* actors=0').Count -gt 0 -and
                ($trace -match 'browser scene applied scene=.* actors=0 chunks=0').Count -gt 0 -and
                ($trace -match 'acceptance: scene cleared 0\|0\|0\|0\|0\|0\|0\|0\|0\|false').Count -gt 0
        }
        if (-not $sceneClearApplied) {
            $sceneClearDiagnostic = if (Test-Path $traceFile) {
                ((Get-Content $traceFile) | Select-Object -Last 120) -join ' | '
            } else { '<none>' }
            throw "The browser scene-clear lifecycle was incomplete. Trace: $sceneClearDiagnostic"
        }
    }

    if ($ExerciseCorePartConstruction) {
        $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
        $corePartConstructionBrowserOnly =
            ($trace -match 'compatibility bypass capability=core-actor-construction operation=part(s)?-browser-only').Count -gt 0 -and
            ($trace -match 'compatibility bypass capability=core-actor-construction operation=part(s)?-legacy-fallback').Count -eq 0
        if (-not $corePartConstructionBrowserOnly) {
            $corePartDiagnostic = ($trace | Where-Object { $_ -match 'core-actor-construction' }) -join ' | '
            throw "Core part construction did not remain browser-only. Trace: $corePartDiagnostic"
        }
    }

    if ($ExerciseThreeMfExportInput) {
        for ($attempt = 0; $attempt -lt 240 -and -not $threeMfExportRetainedInput; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $threeMfExportRetainedInput =
                ($trace -match 'compatibility bypass capability=result-export operation=captured-actor-data').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-export operation=actor-data-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-export operation=lookup-table-managed').Count -gt 0 -and
                ($trace -match 'prepared retained export actors=[1-9][0-9]*').Count -gt 0 -and
                ($trace -match 'prepared retained lookup colors=[2-9][0-9]*|prepared retained lookup colors=[2-9]').Count -gt 0 -and
                ($trace -match 'acceptance: 3mf export input actors=[1-9][0-9]* points=[1-9][0-9]* cells=[1-9][0-9]* lookupColors=[2-9][0-9]* lookupRange=').Count -gt 0 -and
                ($trace -match 'acceptance: 3mf lookup palettes=14 manualColors=11').Count -gt 0
        }
        if (-not $threeMfExportRetainedInput) {
            $exportDiagnostic = ($trace | Where-Object { $_ -match 'export|retained export' } | Select-Object -Last 30) -join ' | '
            throw "The retained-scene 3MF export input was not complete. Trace: $exportDiagnostic"
        }
    }

    if ($ExerciseStaticScalarConstruction) {
        for ($attempt = 0; $attempt -lt 240 -and -not $staticScalarConstructionBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $observedStaticResult =
                ($trace -match 'acceptance: static scalar construction observed points=[1-9][0-9]* range=').Count -gt 0
            $syntheticStaticResult =
                ($trace -match 'compatibility bypass capability=result-construction operation=scalar-update-browser-only').Count -gt 0 -and
                ($trace -match 'acceptance: static scalar update applied generation=[1-9][0-9]*').Count -gt 0
            $staticScalarConstructionBrowserOnly =
                ($trace -match 'compatibility bypass capability=result-construction operation=static-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-construction operation=static-legacy-fallback').Count -eq 0 -and
                ($trace -match 'compatibility bypass capability=result-construction operation=scalar-format-camera-browser-only').Count -gt 0 -and
                ($trace -match 'acceptance: static scalar construction applied revision=[1-9][0-9]*').Count -gt 0 -and
                ($observedStaticResult -or $syntheticStaticResult)
        }
        if (-not $staticScalarConstructionBrowserOnly) {
            $staticDiagnostic = ($trace | Where-Object { $_ -match 'static scalar|result-construction' } |
                Select-Object -Last 40) -join ' | '
            throw "Static scalar-result construction did not remain browser-only. Trace: $staticDiagnostic"
        }
        $scenes = @(Invoke-RestMethod "$baseUrl/api/v1/scenes" -Headers $headers)
        $staticScene = Invoke-RestMethod "$baseUrl/api/v1/scenes/$($scenes[0].sceneId)/manifest" -Headers $headers
        if (-not $ExerciseAnimation -and
            ([double]$staticScene.field.validRange[0] -ne -5 -or [double]$staticScene.field.validRange[1] -ne 9)) {
            throw "Static scalar-result range was not published: $($staticScene.field.validRange -join '..')."
        }
        $uploadedScene = $staticScene
    }

    if ($ExerciseSelectionSurfaceConstruction) {
        for ($attempt = 0; $attempt -lt 240 -and -not $selectionSurfaceConstructionBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $selectionSurfaceConstructionBrowserOnly =
                ($trace -match 'compatibility bypass capability=selection-construction operation=surface-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=selection-construction operation=surface-legacy-fallback').Count -eq 0 -and
                ($trace -match 'acceptance: transient selection cache actorData=1 entries=1').Count -gt 0 -and
                ($trace -match 'acceptance: transient selection exportExcluded=True').Count -gt 0 -and
                ($trace -match 'acceptance: transient selection added true\|actors=[1-9][0-9]*').Count -gt 0 -and
                ($trace -match 'acceptance: transient selection cleared false\|actors=[1-9][0-9]*').Count -gt 0
        }
        if (-not $selectionSurfaceConstructionBrowserOnly) {
            $selectionDiagnostic = ($trace | Where-Object { $_ -match 'transient selection|selection-construction' } |
                Select-Object -Last 40) -join ' | '
            throw "Transient selection-surface construction did not remain browser-only. Trace: $selectionDiagnostic"
        }
    }

    if ($ExerciseSelectionPrimitiveConstruction) {
        for ($attempt = 0; $attempt -lt 240 -and -not $selectionPrimitiveConstructionBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $selectionPrimitiveConstructionBrowserOnly =
                ($trace -match 'compatibility bypass capability=selection-construction operation=points-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=selection-construction operation=lines-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=selection-construction operation=(points|lines)-legacy-fallback').Count -eq 0 -and
                ($trace -match 'acceptance: transient primitives requested pointHandleNull=True lineHandleNull=True').Count -gt 0 -and
                ($trace -match 'acceptance: transient primitives added 2\|5\|2').Count -gt 0 -and
                ($trace -match 'acceptance: transient primitives cleared 0\|0\|0').Count -gt 0
        }
        if (-not $selectionPrimitiveConstructionBrowserOnly) {
            $primitiveDiagnostic = ($trace | Where-Object { $_ -match 'transient primitive|selection-construction' } |
                Select-Object -Last 50) -join ' | '
            throw "Transient point/line selection construction did not remain browser-only. Trace: $primitiveDiagnostic"
        }
    }

    if ($ExerciseTransformations) {
        for ($attempt = 0; $attempt -lt 240 -and -not $transformationsBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $transformationsBrowserOnly =
                ($trace -match 'compatibility bypass capability=transformation operation=add-symmetry-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=transformation operation=add-linear-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=transformation operation=add-circular-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=transformation operation=apply-browser-only').Count -gt 0 -and
                ($trace -match 'acceptance: transformations applied matrices=7 actors=[1-9][0-9]* pickable=0').Count -gt 0 -and
                ($trace -match 'acceptance: transformations hidden actors=[1-9][0-9]*').Count -gt 0 -and
                ($trace -match 'acceptance: transformations shown actors=[1-9][0-9]*').Count -gt 0
        }
        if (-not $transformationsBrowserOnly) {
            $transformationDiagnostic = ($trace | Where-Object { $_ -match 'transformation' } |
                Select-Object -Last 50) -join ' | '
            throw "Browser-owned transformations were not completed. Trace: $transformationDiagnostic"
        }
    }

    if ($ExerciseAuxiliaryConstruction) {
        for ($attempt = 0; $attempt -lt 240 -and -not $auxiliaryConstructionBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $auxiliaryConstructionBrowserOnly =
                ($trace -match 'compatibility bypass capability=auxiliary-construction operation=surface-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=auxiliary-construction operation=primitive-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=auxiliary-construction operation=array-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=auxiliary-construction operation=points-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=openfoam-construction operation=native-reader-bypassed').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=auxiliary-construction operation=.*legacy-fallback').Count -eq 0 -and
                ($trace -match 'acceptance: auxiliary construction requested pointHandleNull=True openFoamHandleNull=True').Count -gt 0 -and
                ($trace -match 'acceptance: auxiliary primitives added 2\|2\|3\|1').Count -gt 0 -and
                ($trace -match 'acceptance: auxiliary primitives retained-after-selection-clear 2\|2\|3\|1').Count -gt 0 -and
                ($trace -match 'acceptance: auxiliary primitives hidden 2\|1\|3\|1').Count -gt 0 -and
                ($trace -match 'acceptance: auxiliary primitives shown 2\|2\|3\|1').Count -gt 0 -and
                ($trace -match 'acceptance: auxiliary removal contains surface=False line=False point=False').Count -gt 0 -and
                ($trace -match 'acceptance: auxiliary primitives removed 0\|0\|0\|0').Count -gt 0
        }
        if (-not $auxiliaryConstructionBrowserOnly) {
            $auxiliaryDiagnostic = ($trace | Where-Object { $_ -match 'auxiliary|openfoam' } |
                Select-Object -Last 60) -join ' | '
            throw "Browser-owned auxiliary construction was not completed. Trace: $auxiliaryDiagnostic"
        }
    }

    if ($ExerciseGenericConstruction) {
        for ($attempt = 0; $attempt -lt 240 -and -not $genericConstructionBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $genericConstructionBrowserOnly =
                ($trace -match 'compatibility bypass capability=generic-construction operation=surface-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=generic-construction operation=points-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=generic-construction operation=.*legacy-fallback').Count -eq 0 -and
                ($trace -match 'compatibility bypass capability=native-handle-construction operation=blocked-browser-only').Count -gt 0 -and
                ($trace -match 'acceptance: generic construction requested surfaceHandleNull=True pointHandleNull=True').Count -gt 0 -and
                ($trace -match 'acceptance: generic construction True\|True').Count -gt 0 -and
                ($trace -match 'acceptance: generic removal contains surface=False point=False').Count -gt 0
        }
        if (-not $genericConstructionBrowserOnly) {
            $genericDiagnostic = ($trace | Where-Object { $_ -match 'generic|native-handle' } |
                Select-Object -Last 60) -join ' | '
            throw "Browser-owned generic construction was not completed. Trace: $genericDiagnostic"
        }
    }

    if ($ExerciseOrientationMarker) {
        for ($attempt = 0; $attempt -lt 240 -and -not $orientationMarkerBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $orientationMarkerBrowserOnly =
                ($trace -match 'compatibility bypass capability=orientation-widget operation=visibility-browser-only').Count -ge 2 -and
                ($trace -match 'acceptance: orientation marker requested hidden=True').Count -gt 0 -and
                ($trace -match 'acceptance: orientation marker hidden false\|0\|true\|200\|0\.706,0\.016,0\.149\|0\.129,0\.808,0\.149\|0\.231,0\.298,0\.753').Count -gt 0 -and
                ($trace -match 'acceptance: orientation marker visible true\|1\|true\|200\|0\.706,0\.016,0\.149\|0\.129,0\.808,0\.149\|0\.231,0\.298,0\.753').Count -gt 0
        }
        if (-not $orientationMarkerBrowserOnly) {
            $orientationMarkerDiagnostic = ($trace | Where-Object { $_ -match 'orientation marker|orientation-widget' } |
                Select-Object -Last 60) -join ' | '
            $orientationMarkerTail = ($trace | Select-Object -Last 80) -join ' | '
            $orientationMarkerDiagnostic = $orientationMarkerDiagnostic + ' | tail=' + $orientationMarkerTail
            throw "Browser-owned orientation marker was not completed. Trace: $orientationMarkerDiagnostic"
        }
    }

    if ($ExerciseScaleWidget) {
        for ($attempt = 0; $attempt -lt 240 -and -not $scaleWidgetBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $scaleWidgetBrowserOnly =
                ($trace -match 'compatibility bypass capability=result-widget operation=scale-visibility-browser-only').Count -ge 2 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=scale-unit-browser-only').Count -gt 0 -and
                ($trace -match 'acceptance: scale widget hidden false\|mm\|[1-9][0-9]*\.[0-9]{3}\|[0-9.eE+-]*[1-9][0-9.eE+-]*\|6\|[^|]+ mm').Count -gt 0 -and
                ($trace -match 'acceptance: scale widget visible true\|mm\|[1-9][0-9]*\.[0-9]{3}\|[0-9.eE+-]*[1-9][0-9.eE+-]*\|6\|[^|]+ mm').Count -gt 0
        }
        if (-not $scaleWidgetBrowserOnly) {
            $scaleWidgetDiagnostic = ($trace | Where-Object { $_ -match 'scale widget|result-widget' } |
                Select-Object -Last 60) -join ' | '
            $scaleWidgetTail = ($trace | Select-Object -Last 80) -join ' | '
            throw "Browser-owned scale widget was not completed. Trace: $scaleWidgetDiagnostic | tail=$scaleWidgetTail"
        }
    }

    if ($ExerciseScalarLegend) {
        for ($attempt = 0; $attempt -lt 240 -and -not $scalarLegendBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $scalarLegendBrowserOnly =
                ($trace -match 'compatibility bypass capability=result-widget operation=scalar-legend-position-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=scalar-legend-spectrum-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=scalar-legend-text-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=scalar-legend-format-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=scalar-legend-background-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=scalar-legend-border-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=scalar-legend-visibility-browser-only').Count -ge 2 -and
                ($trace -match 'acceptance: scalar legend hidden false\|Displacements\|Magnitude\|mm\|Automatic\|E3\|[-+0-9.eE]+\|[-+0-9.eE]+\|9\|10\|true\|true\|Complex: Real~Displacements: Magnitude~Unit: mm~Automatic').Count -gt 0 -and
                ($trace -match 'acceptance: scalar legend visible true\|Displacements\|Magnitude\|mm\|Automatic\|E3\|[-+0-9.eE]+\|[-+0-9.eE]+\|9\|10\|true\|true\|Complex: Real~Displacements: Magnitude~Unit: mm~Automatic').Count -gt 0
        }
        if (-not $scalarLegendBrowserOnly) {
            $scalarLegendDiagnostic = ($trace | Where-Object { $_ -match 'scalar legend|result-widget' } |
                Select-Object -Last 80) -join ' | '
            $scalarLegendTail = ($trace | Select-Object -Last 80) -join ' | '
            throw "Browser-owned scalar legend was not completed. Trace: $scalarLegendDiagnostic | tail=$scalarLegendTail"
        }
    }

    if ($ExerciseColorBar) {
        for ($attempt = 0; $attempt -lt 240 -and -not $colorBarBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $colorBarBrowserOnly =
                ($trace -match 'compatibility bypass capability=result-widget operation=color-bar-position-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=color-bar-set-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=color-bar-add-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=color-bar-background-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=color-bar-border-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=color-bar-hide-browser-only').Count -gt 0 -and
                ($trace -match 'acceptance: color bar visible true\|3\|Front face~Back face~Load A\|1\.000,0\.000,0\.000~0\.000,0\.000,1\.000~0\.000,1\.000,0\.000\|true\|false').Count -gt 0 -and
                ($trace -match 'acceptance: color bar hidden false\|0\|\|\|true\|false').Count -gt 0
        }
        if (-not $colorBarBrowserOnly) {
            $colorBarDiagnostic = ($trace | Where-Object { $_ -match 'color bar|result-widget' } |
                Select-Object -Last 80) -join ' | '
            $colorBarTail = ($trace | Select-Object -Last 80) -join ' | '
            throw "Browser-owned categorical color bar was not completed. Trace: $colorBarDiagnostic | tail=$colorBarTail"
        }
    }

    if ($ExerciseStatusBlock) {
        for ($attempt = 0; $attempt -lt 240 -and -not $statusBlockBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $statusBlockBrowserOnly =
                ($trace -match 'compatibility bypass capability=result-widget operation=shared-position-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=color-bar-position-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=status-block-visibility-browser-only').Count -ge 2 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=status-block-background-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=status-block-border-browser-only').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=result-widget operation=status-block-content-browser-only').Count -gt 0 -and
                ($trace -match 'acceptance: status block static true\|Name: rotor_case\.frd   Date: [^|]+   Time: 14:20:30~Step: #2   Mode: #5   Frequency: 120 Hz~Deformation variable: DISP   Deformation scale factor: 2[,.]5\|true\|false\|20,20\|20,20\|20,20').Count -gt 0 -and
                ($trace -match 'acceptance: status block animated true\|Name: rotor_case\.frd   Date: [^|]+   Time: 14:20:30~Step: #3   Mode: #7   Frequency: 120 Hz~Deformation variable: DISP   Deformation scale factor: 2[,.]5~Complex angle: 45[^|]*\|true\|false\|20,20\|20,20\|20,20').Count -gt 0 -and
                ($trace -match 'acceptance: status block hidden false\|.*\|true\|false\|20,20\|20,20\|20,20').Count -gt 0
        }
        if (-not $statusBlockBrowserOnly) {
            $statusBlockDiagnostic = ($trace | Where-Object { $_ -match 'status block|result widget layout|result-widget' } |
                Select-Object -Last 100) -join ' | '
            throw "Browser-owned status block and shared layout were not completed. Trace: $statusBlockDiagnostic"
        }
    }

    if ($ExerciseCellClassification) {
        for ($attempt = 0; $attempt -lt 240 -and -not $cellClassificationBrowserOnly; $attempt++) {
            Start-Sleep -Milliseconds 250
            $trace = if (Test-Path $traceFile) { Get-Content $traceFile } else { @() }
            $cellClassificationBrowserOnly =
                ($trace -match "captured actor='__VtkJsSmokeStripPixelSurface' points=8 triangles=4").Count -gt 0 -and
                ($trace -match "captured auxiliary primitive='__VtkJsSmokePolyline' points=3 lines=2").Count -gt 0 -and
                ($trace -match "actor='__VtkJsSmokeMixedUnsupported': unsupported render cell types=10").Count -gt 0 -and
                ($trace -match 'acceptance: cell classification requested surfaceHandleNull=True polylineHandleNull=True mixedRejected=True pickablePointRejected=True').Count -gt 0 -and
                ($trace -match 'acceptance: cell classification true\|true').Count -gt 0 -and
                ($trace -match 'acceptance: cell classification removal contains surface=False polyline=False').Count -gt 0 -and
                ($trace -match 'compatibility bypass capability=auxiliary-construction operation=.*legacy-fallback').Count -eq 0
        }
        if (-not $cellClassificationBrowserOnly) {
            $cellClassificationDiagnostic = ($trace | Where-Object { $_ -match 'classification|StripPixel|Polyline|MixedUnsupported|PickablePoint' } |
                Select-Object -Last 60) -join ' | '
            if ([string]::IsNullOrWhiteSpace($cellClassificationDiagnostic)) {
                $cellClassificationDiagnostic = ($trace | Select-Object -Last 80) -join ' | '
            }
            throw "Browser-owned cell classification was not completed. Trace: $cellClassificationDiagnostic"
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
        $locatorLines = @(Select-String -Path $traceFile -Pattern 'publishing topology .*locatorPoints=[0-9]+ locatorCells=[0-9]+')
        for ($index = $locatorLines.Count - 1; $index -ge 0; $index--) {
            if ($locatorLines[$index].Line -match 'locatorPoints=([0-9]+) locatorCells=([0-9]+)' -and
                [long]$Matches[1] -gt 0 -and [long]$Matches[2] -gt 0) {
                $locatorPoints = [long]$Matches[1]
                $locatorCells = [long]$Matches[2]
                if ($ExerciseAnimation) { $animationHostFrameBytes += 12L * $locatorPoints }
                break
            }
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
        TransformationsBrowserOnly = $transformationsBrowserOnly
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
        AnnotationSettingsApplied = $annotationSettingsApplied
        FollowerRedrawApplied = $followerRedrawApplied
        ExplodedPreviewApplied = $explodedPreviewApplied
        SectionViewApplied = $sectionViewApplied
        SphereSymbolApplied = $sphereSymbolApplied
        CoordinateOverlayApplied = $coordinateOverlayApplied
        DirectionalGlyphApplied = $directionalGlyphApplied
        GeneralConeGlyphApplied = $generalConeGlyphApplied
        RotationalGlyphApplied = $rotationalGlyphApplied
        ArrowGlyphApplied = $arrowGlyphApplied
        DoubleArrowGlyphApplied = $doubleArrowGlyphApplied
        SpringGlyphApplied = $springGlyphApplied
        ThermalGlyphApplied = $thermalGlyphApplied
        FluxGlyphApplied = $fluxGlyphApplied
        ViewportAppearanceApplied = $viewportAppearanceApplied
        OrientationMarkerBrowserOnly = $orientationMarkerBrowserOnly
        ScaleWidgetBrowserOnly = $scaleWidgetBrowserOnly
        ScalarLegendBrowserOnly = $scalarLegendBrowserOnly
        ColorBarBrowserOnly = $colorBarBrowserOnly
        StatusBlockBrowserOnly = $statusBlockBrowserOnly
        ActorPresentationApplied = $actorPresentationApplied
        ActorHighlightApplied = $actorHighlightApplied
        BrowserSceneClearApplied = $sceneClearApplied
        CorePartConstructionBrowserOnly = $corePartConstructionBrowserOnly
        ThreeMfExportRetainedInput = $threeMfExportRetainedInput
        StaticScalarConstructionBrowserOnly = $staticScalarConstructionBrowserOnly
        SelectionSurfaceConstructionBrowserOnly = $selectionSurfaceConstructionBrowserOnly
        SelectionPrimitiveConstructionBrowserOnly = $selectionPrimitiveConstructionBrowserOnly
        AuxiliaryConstructionBrowserOnly = $auxiliaryConstructionBrowserOnly
        GenericConstructionBrowserOnly = $genericConstructionBrowserOnly
        CellClassificationBrowserOnly = $cellClassificationBrowserOnly
        SectionCapActors = $sectionCapActors
        SectionSkippedCells = $sectionSkippedCells
        LargeSelectionCount = $largeSelectionCount
        AnimationFramePublished = $animationFramePublished
        AnimationFrameActiVizBypassed = $animationFrameActiVizBypassed
        AnimationConstructionBrowserOnly = $animationConstructionBrowserOnly
        AnimationExportAdapterMaterialized = $animationExportAdapterMaterialized
        AnimationPngCaptureBrowserOnly = $animationPngCaptureBrowserOnly
        AnimationAviCaptureBrowserOnly = $animationAviCaptureBrowserOnly
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
    Remove-Item Env:PREPOMAX_VTKJS_HOST_TOKEN_OVERRIDE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_FIELD -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_FIELD_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_PICK_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_PICK_ITEM -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANNOTATION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_EXTREMA -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANNOTATION_INTERACTION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANNOTATION_SETTINGS -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_LARGE_SELECTION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_CAMERA -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_BOX_ZOOM -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_VISIBILITY_CAMERA -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_TRANSFORMATIONS -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_EXPLODED_PREVIEW -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SECTION_VIEW -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SPHERE_SYMBOL -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_COORDINATE_OVERLAY -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_DIRECTIONAL_GLYPH -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_GENERAL_CONE_GLYPH -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ROTATIONAL_GLYPH -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ARROW_GLYPH -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_DOUBLE_ARROW_GLYPH -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SPRING_GLYPH -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_THERMAL_GLYPH -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_FLUX_GLYPH -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_VIEWPORT_APPEARANCE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ORIENTATION_MARKER -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SCALE_WIDGET -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SCALAR_LEGEND -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_COLOR_BAR -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_STATUS_BLOCK -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ACTOR_PRESENTATION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SCENE_CLEAR -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SCENE_CLEAR_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_3MF_EXPORT_INPUT -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_STATIC_SCALAR_CONSTRUCTION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SELECTION_SURFACE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SELECTION_PRIMITIVES -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_AUXILIARY_CONSTRUCTION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_GENERIC_CONSTRUCTION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_CELL_CLASSIFICATION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_SELECTION_CAPACITY -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_SEQUENCE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_FOLLOWER_REDRAW -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_PNG_CAPTURE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_AVI_CAPTURE -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item Env:PREPOMAX_VTKJS_SMOKE_ANIMATION_FRAME_TRIGGER -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fieldSwitchTrigger -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $pickTrigger -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $animationTrigger -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $animationFrameTrigger -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $sceneClearTrigger -Force -ErrorAction SilentlyContinue
}
