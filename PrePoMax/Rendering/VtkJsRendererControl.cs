using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using CaeGlobals;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PrePoMax.Rendering
{
    internal sealed class VtkJsPngCaptureResult
    {
        public int Width { get; }
        public int Height { get; }
        public long Bytes { get; }

        public VtkJsPngCaptureResult(int width, int height, long bytes)
        {
            Width = width;
            Height = height;
            Bytes = bytes;
        }
    }

    internal sealed class VtkJsJpegCaptureResult
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Bytes { get; }

        public VtkJsJpegCaptureResult(int width, int height, byte[] bytes)
        {
            Width = width;
            Height = height;
            Bytes = bytes;
        }
    }

    internal sealed class VtkJsRendererControl : UserControl
    {
        private const int MaximumSharedSelectionIds = 8000000;
        private const long MaximumSharedSelectionBytes = 64L * 1024 * 1024;
        private const char ActorNameSeparator = ':';
        private readonly WebView2 _webView;
        private readonly Label _status;
        private readonly VtkJsRendererHost _host;
        private readonly VtkJsSceneCapture _sceneCapture;
        private readonly Timer _publishTimer;
        private readonly Timer _annotationPublishTimer;
        private readonly Timer _sphereSymbolPublishTimer;
        private readonly Timer _transientPrimitivePublishTimer;
        private readonly Timer _coordinateOverlayPublishTimer;
        private readonly Timer _directionalGlyphPublishTimer;
        private readonly Timer _explodedPreviewCompletionTimer;
        private readonly Timer _sceneAppliedCompletionTimer;
        private TaskCompletionSource<long> _pendingAnimationFrameActivation;
        private long _pendingAnimationFrameMinimumRevision;
        private Guid _pendingAnimationFramePriorSceneId;
        private Uri _baseUri;
        private bool _started;
        private bool _publishing;
        private bool _sessionBootstrapInFlight;
        private int _captureGeneration;
        private bool _topologyDirty = true;
        private bool _frameDirty;
        private readonly HashSet<string> _pendingScalarActors = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, vtkControl.vtkMaxActorData> _animationSources =
            new Dictionary<string, vtkControl.vtkMaxActorData>(StringComparer.Ordinal);
        private vtkControl.vtkMaxAnimationType _animationType = vtkControl.vtkMaxAnimationType.ScaleFactor;
        private float[] _animationTime = new float[0];
        private int[] _animationStepId = new int[0];
        private int[] _animationStepIncrementId = new int[0];
        private float[] _animationScale = new float[0];
        private double[] _animationScalarRange = new double[0];
        private bool _useAllFramesScalarRange;
        private readonly vtkControl.vtkMaxColorSpectrum _colorSpectrum = new vtkControl.vtkMaxColorSpectrum();
        private string _fieldName = "PrePoMax values";
        private string _fieldComponent = "value";
        private string _fieldUnits = "model-unit";
        private VtkJsSelectionStateMessage _selectionState = VtkJsSelectionStateMessage.Empty;
        private readonly Dictionary<string, VtkJsAnnotationMessage> _annotations =
            new Dictionary<string, VtkJsAnnotationMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsSphereSymbolMessage> _sphereSymbols =
            new Dictionary<string, VtkJsSphereSymbolMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsTransientPrimitiveMessage> _transientPrimitives =
            new Dictionary<string, VtkJsTransientPrimitiveMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsTransientPrimitiveMessage> _auxiliaryPrimitives =
            new Dictionary<string, VtkJsTransientPrimitiveMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsCoordinateAxisMessage> _coordinateAxes =
            new Dictionary<string, VtkJsCoordinateAxisMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsCaptionMessage> _captions =
            new Dictionary<string, VtkJsCaptionMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsDirectionalGlyphMessage> _directionalGlyphs =
            new Dictionary<string, VtkJsDirectionalGlyphMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsActorExtremaState> _actorExtrema =
            new Dictionary<string, VtkJsActorExtremaState>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _actorColorContours =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly HashSet<string> _hiddenActorNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _highlightedActorNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _transientSelectionActorNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _transientSelectionSecondary =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsExtremaAnnotationSettings> _extremaAnnotationSettings =
            new Dictionary<string, VtkJsExtremaAnnotationSettings>(StringComparer.Ordinal);
        private readonly Dictionary<string, double[]> _annotationOffsets =
            new Dictionary<string, double[]>(StringComparer.Ordinal);
        private bool _boxZoomEnabled;
        private VtkJsFollowerViewStateMessage _followerViewState = VtkJsFollowerViewStateMessage.Empty;
        private readonly Dictionary<string, double[]> _explodedPreviewOffsets =
            new Dictionary<string, double[]>(StringComparer.Ordinal);
        private VtkJsSectionViewStateMessage _sectionViewState = VtkJsSectionViewStateMessage.Disabled;
        private readonly VtkJsViewportAppearanceMessage _viewportAppearanceState =
            VtkJsViewportAppearanceMessage.Default;
        private bool _orientationMarkerVisible = true;
        private bool _scaleWidgetVisible = true;
        private string _scaleWidgetUnit = "";
        private bool _scalarLegendVisible;
        private string _scalarLegendFieldName = "PrePoMax values";
        private string _scalarLegendComponentName = "value";
        private string _scalarLegendUnit = "";
        private string _scalarLegendComplexComponent = "";
        private string _scalarLegendMinMaxType = "Automatic";
        private string _scalarLegendNumberFormat = "G4";
        private bool _scalarLegendDrawBackground;
        private bool _scalarLegendDrawBorder;
        private readonly List<Color> _colorBarColors = new List<Color>();
        private readonly List<string> _colorBarLabels = new List<string>();
        private bool _colorBarVisible;
        private bool _colorBarDrawBackground;
        private bool _colorBarDrawBorder = true;
        private bool _statusBlockDrawEnabled = true;
        private bool _statusBlockVisible;
        private bool _statusBlockDrawBackground;
        private bool _statusBlockDrawBorder;
        private string _statusBlockName = "";
        private DateTime _statusBlockDateTime;
        private float _statusBlockAnalysisTime;
        private string _statusBlockUnit = "";
        private string _statusBlockDeformationVariable = "";
        private float _statusBlockDeformationScaleFactor = 1;
        private vtkControl.vtkMaxFieldDataType _statusBlockFieldType = vtkControl.vtkMaxFieldDataType.Static;
        private int _statusBlockStepNumber;
        private int _statusBlockIncrementNumber;
        private float _statusBlockAnimationScaleFactor = -1;
        private vtkControl.vtkMaxAnimationType _statusBlockAnimationType = vtkControl.vtkMaxAnimationType.ScaleFactor;
        private bool _statusBlockInitialized;
        private bool _pointSmoothing = true;
        private bool _lineSmoothing = true;
        private Color _primaryHighlightColor = Color.Red;
        private Color _secondaryHighlightColor = Color.Violet;
        private Color _mouseHighlightColor = Color.Orange;
        private readonly VtkJsResultWidgetLayoutMessage _resultWidgetLayoutState =
            VtkJsResultWidgetLayoutMessage.Default;
        private readonly List<VtkJsTransformationStep> _transformationSteps =
            new List<VtkJsTransformationStep>();
        private VtkJsTransformationStateMessage _transformationState = VtkJsTransformationStateMessage.Empty;
        private string _pendingExplodedPreviewRequestId;
        private Action _pendingExplodedPreviewCompletion;
        private Action _pendingSceneAppliedCompletion;
        private bool _hasBootstrappedNonEmptyScene;
        private bool _traceNextStaticScalarUpdate;

        public bool HasPublishedTopology { get; private set; }
        public bool IsBrowserReady { get; private set; }
        public int BrowserAppliedActorCount { get; private set; }
        public int BrowserAppliedPipelineChunks { get; private set; }
        public long BrowserAppliedSceneRevision { get; private set; }
        public Guid BrowserAppliedSceneId { get; private set; }
        public int BrowserAppliedTransformationMatrices { get; private set; }
        public int BrowserAppliedTransformationActors { get; private set; }
        public int BrowserVisibleTransformationActors { get; private set; }
        public int BrowserPickableTransformationActors { get; private set; }
        public event EventHandler<VtkJsValidatedPickEventArgs> ValidatedPick;
        public event EventHandler<VtkJsValidatedAreaEventArgs> ValidatedArea;
        public event EventHandler<VtkJsAnnotationEditedEventArgs> AnnotationEdited;
        public event EventHandler<VtkJsAnnotationActionEventArgs> AnnotationActionRequested;
        public event EventHandler<VtkJsCameraChangedEventArgs> CameraChanged;
        public event EventHandler<VtkJsBoxZoomEndedEventArgs> BoxZoomEnded;

        public void WriteAcceptanceTrace(string message)
        {
            WriteTrace("acceptance: " + message);
        }

        public void WriteCompatibilityTrace(string capability, string operation)
        {
            WriteTrace("compatibility bypass capability=" + capability + " operation=" + operation);
        }

        public string[] GetCapturedActorNames()
        {
            return _sceneCapture.GetActorNames().Concat(_auxiliaryPrimitives.Keys)
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        public void ArmStaticScalarUpdateAcceptance()
        {
            _traceNextStaticScalarUpdate = true;
        }

        public bool ContainsActor(string actorName)
        {
            return _sceneCapture.ContainsActor(actorName) ||
                   (actorName != null && _auxiliaryPrimitives.ContainsKey(actorName));
        }

        public VtkJsRendererControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(20, 24, 31);
            _host = new VtkJsRendererHost();
            _sceneCapture = new VtkJsSceneCapture();
            _publishTimer = new Timer { Interval = 250 };
            _publishTimer.Tick += async (sender, args) => await PublishCapturedSceneAsync();
            _annotationPublishTimer = new Timer { Interval = 50 };
            _annotationPublishTimer.Tick += (sender, args) =>
            {
                _annotationPublishTimer.Stop();
                PublishAnnotationState();
            };
            _sphereSymbolPublishTimer = new Timer { Interval = 50 };
            _sphereSymbolPublishTimer.Tick += (sender, args) =>
            {
                _sphereSymbolPublishTimer.Stop();
                PublishSphereSymbolState();
            };
            _transientPrimitivePublishTimer = new Timer { Interval = 50 };
            _transientPrimitivePublishTimer.Tick += (sender, args) =>
            {
                _transientPrimitivePublishTimer.Stop();
                PublishTransientPrimitiveState();
            };
            _coordinateOverlayPublishTimer = new Timer { Interval = 50 };
            _coordinateOverlayPublishTimer.Tick += (sender, args) =>
            {
                _coordinateOverlayPublishTimer.Stop();
                PublishCoordinateOverlayState();
            };
            _directionalGlyphPublishTimer = new Timer { Interval = 50 };
            _directionalGlyphPublishTimer.Tick += (sender, args) =>
            {
                _directionalGlyphPublishTimer.Stop();
                PublishDirectionalGlyphState();
            };
            _explodedPreviewCompletionTimer = new Timer();
            _explodedPreviewCompletionTimer.Tick += (sender, args) =>
                CompleteExplodedViewPreview(_pendingExplodedPreviewRequestId, "timeout");
            _sceneAppliedCompletionTimer = new Timer { Interval = 15000 };
            _sceneAppliedCompletionTimer.Tick += (sender, args) => CompletePendingSceneApplied("timeout");
            _webView = new WebView2 { Dock = DockStyle.Fill, Visible = false };
            _status = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = BackColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Starting VTK.js renderer…"
            };
            Controls.Add(_webView);
            Controls.Add(_status);
        }

        public async Task StartAsync()
        {
            try
            {
                WriteTrace("renderer start requested");
                _baseUri = await _host.StartAsync();
                await _host.SetFieldMetadataAsync(_fieldName, _fieldComponent, _fieldUnits);
                VtkJsSceneSnapshot initialSnapshot = _sceneCapture.Snapshot();
                if (initialSnapshot.ActorCount > 0) await _host.UploadSceneAsync(initialSnapshot);
                _topologyDirty = false;
                _frameDirty = false;
                _pendingScalarActors.Clear();
                HasPublishedTopology = initialSnapshot.ActorCount > 0;
                _hasBootstrappedNonEmptyScene = initialSnapshot.ActorCount > 0;
                _started = true;
                WriteTrace("renderer host ready; initial actors=" + initialSnapshot.ActorCount);
                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PrePoMax", "WebView2");
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userData);
                await _webView.EnsureCoreWebView2Async(environment);
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                _webView.CoreWebView2.NewWindowRequested += (sender, args) => args.Handled = true;
                _webView.NavigationCompleted += (sender, args) =>
                {
                    IsBrowserReady = args.IsSuccess;
                    BrowserAppliedActorCount = 0;
                    BrowserAppliedPipelineChunks = 0;
                    BrowserAppliedSceneRevision = 0;
                    BrowserAppliedSceneId = Guid.Empty;
                    BrowserAppliedTransformationMatrices = 0;
                    BrowserAppliedTransformationActors = 0;
                    BrowserVisibleTransformationActors = 0;
                    BrowserPickableTransformationActors = 0;
                    if (args.IsSuccess)
                    {
                        PublishSelectionState();
                        PublishAnnotationState();
                        PublishSphereSymbolState();
                        PublishTransientPrimitiveState();
                        PublishCoordinateOverlayState();
                        PublishDirectionalGlyphState();
                        PublishActorContourState();
                        PublishActorHighlightState();
                        PublishViewportAppearanceState(true);
                        PublishOrientationMarkerState();
                        PublishScaleWidgetState();
                        PublishScalarLegendState();
                        PublishColorBarState();
                        PublishStatusBlockState();
                        PublishResultWidgetLayoutState();
                        PublishGraphicsInteractionState();
                        PublishBoxZoomState();
                        PublishFollowerViewState();
                        PublishExplodedViewState(false, 0);
                        PublishSectionViewState();
                        PublishTransformationState();
                    }
                    if (!args.IsSuccess) ShowError("The embedded renderer failed to load: " + args.WebErrorStatus + ".");
                };
                _webView.Source = new Uri(_baseUri, "/?embedded=1");
                _status.Visible = false;
                _webView.Visible = true;
                _webView.BringToFront();
            }
            catch (Exception exception)
            {
                ShowError("VTK.js renderer unavailable. " + exception.Message);
            }
        }

        public async Task StopAsync()
        {
            CancelPendingAnimationFrameActivation();
            _started = false;
            IsBrowserReady = false;
            BrowserAppliedActorCount = 0;
            BrowserAppliedPipelineChunks = 0;
            BrowserAppliedSceneRevision = 0;
            BrowserAppliedSceneId = Guid.Empty;
            BrowserAppliedTransformationMatrices = 0;
            BrowserAppliedTransformationActors = 0;
            BrowserVisibleTransformationActors = 0;
            BrowserPickableTransformationActors = 0;
            _sessionBootstrapInFlight = false;
            _publishTimer.Stop();
            _annotationPublishTimer.Stop();
            _sphereSymbolPublishTimer.Stop();
            _transientPrimitivePublishTimer.Stop();
            _coordinateOverlayPublishTimer.Stop();
            _directionalGlyphPublishTimer.Stop();
            _explodedPreviewCompletionTimer.Stop();
            _sceneAppliedCompletionTimer.Stop();
            _webView.Visible = false;
            await _host.StopAsync();
        }

        public async void TriggerAcceptancePick()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            bool geometryArea = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_PICK_ITEM"),
                "Geometry", StringComparison.OrdinalIgnoreCase);
            bool geometryEdge = string.Equals(
                Environment.GetEnvironmentVariable("PREPOMAX_VTKJS_SMOKE_PICK_ITEM"),
                "GeometryEdge", StringComparison.OrdinalIgnoreCase);
            string script = geometryArea ? @"(() => {
                let attempts = 0;
                const timer = setInterval(() => {
                    const viewport = document.querySelector('#viewport');
                    const canvas = viewport?.querySelector('canvas');
                    if (viewport && canvas) {
                        const box = viewport.getBoundingClientRect();
                        const start = { x: box.left + box.width * 0.05, y: box.top + box.height * 0.05 };
                        const end = { x: box.left + box.width * 0.95, y: box.top + box.height * 0.95 };
                        canvas.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerId: 73,
                            button: 0, buttons: 1, altKey: true, clientX: start.x, clientY: start.y }));
                        canvas.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerId: 73,
                            button: 0, buttons: 1, altKey: true, clientX: end.x, clientY: end.y }));
                        canvas.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerId: 73,
                            button: 0, buttons: 0, altKey: true, clientX: end.x, clientY: end.y }));
                        clearInterval(timer);
                    } else if (++attempts >= 40) clearInterval(timer);
                }, 250);
            })();" : geometryEdge ? @"(() => {
                const points = [[0.42,0.62]];
                for (let x = 0.25; x <= 0.751; x += 0.05) {
                    for (let y = 0.25; y <= 0.751; y += 0.05) points.push([x, y]);
                }
                let index = 0;
                let waits = 0;
                const timer = setInterval(() => {
                    const viewport = document.querySelector('#viewport');
                    const canvas = viewport?.querySelector('canvas');
                    if (viewport && canvas) {
                        const box = viewport.getBoundingClientRect();
                        const point = points[index++];
                        canvas.dispatchEvent(new MouseEvent('click', {
                            bubbles: true,
                            clientX: box.left + box.width * point[0],
                            clientY: box.top + box.height * point[1]
                        }));
                        if (index >= points.length) clearInterval(timer);
                    } else if (++waits >= 40) clearInterval(timer);
                }, 150);
            })();" : @"(() => {
                let attempts = 0;
                const timer = setInterval(() => {
                    const viewport = document.querySelector('.viewport-shell');
                    const canvas = viewport?.querySelector('canvas');
                    if (viewport && canvas) {
                        const box = viewport.getBoundingClientRect();
                        canvas.dispatchEvent(new MouseEvent('click', {
                            bubbles: true,
                            clientX: box.left + box.width * 0.42,
                            clientY: box.top + box.height * 0.62
                        }));
                        clearInterval(timer);
                    } else if (++attempts >= 40) {
                        clearInterval(timer);
                    }
                }, 250);
            })();";
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
                WriteTrace(geometryArea ? "acceptance: browser area requested" : "acceptance: browser pick requested");
            }
            catch (Exception exception)
            {
                WriteTrace("acceptance: browser pick request failed: " + exception.Message);
            }
        }

        public async void TriggerAcceptanceBoxZoom()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            string script = @"(() => {
                let attempts = 0;
                const timer = setInterval(() => {
                    const viewport = document.querySelector('#viewport');
                    const canvas = viewport?.querySelector('canvas');
                    if (viewport?.dataset.boxZoom === 'true' && canvas) {
                        const box = viewport.getBoundingClientRect();
                        const start = { x: box.left + box.width * 0.25, y: box.top + box.height * 0.25 };
                        const end = { x: box.left + box.width * 0.75, y: box.top + box.height * 0.75 };
                        canvas.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerId: 91,
                            button: 0, buttons: 1, clientX: start.x, clientY: start.y }));
                        canvas.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerId: 91,
                            button: 0, buttons: 1, clientX: end.x, clientY: end.y }));
                        canvas.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerId: 91,
                            button: 0, buttons: 0, clientX: end.x, clientY: end.y }));
                        clearInterval(timer);
                    } else if (++attempts >= 40) clearInterval(timer);
                }, 100);
            })();";
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
                WriteTrace("acceptance: browser box zoom requested");
            }
            catch (Exception exception)
            {
                WriteTrace("acceptance: browser box zoom request failed: " + exception.Message);
            }
        }

        public void SetSelection(string item, int[] ids, string[] actorNames, int[][] faceNodeIds = null,
                                 int[][] edgeNodeIds = null)
        {
            ids = ids ?? new int[0];
            actorNames = actorNames ?? new string[0];
            faceNodeIds = faceNodeIds ?? new int[0][];
            edgeNodeIds = edgeNodeIds ?? new int[0][];
            long faceNodeCount = 0;
            bool invalidFaces = faceNodeIds.Length > 250000;
            foreach (int[] face in faceNodeIds)
            {
                if (face == null || face.Length < 3 || face.Length > 32) invalidFaces = true;
                else faceNodeCount += face.Length;
            }
            long edgeNodeCount = 0;
            bool invalidEdges = edgeNodeIds.Length > 250000;
            foreach (int[] edge in edgeNodeIds)
            {
                if (edge == null || (edge.Length != 2 && edge.Length != 3)) invalidEdges = true;
                else edgeNodeCount += edge.Length;
            }
            if (ids.Length > MaximumSharedSelectionIds || actorNames.Length > 4096 || invalidFaces || faceNodeCount > 2000000 ||
                invalidEdges || edgeNodeCount > 750000)
            {
                _selectionState = new VtkJsSelectionStateMessage
                {
                    Type = "prepomax.selection.updated-v1",
                    Item = "unsupported",
                    Ids = new int[0],
                    ActorNames = new string[0],
                    FaceNodeIds = new int[0][],
                    EdgeNodeIds = new int[0][]
                };
            }
            else
            {
                _selectionState = new VtkJsSelectionStateMessage
                {
                    Type = "prepomax.selection.updated-v1",
                    Item = item,
                    Ids = (int[])ids.Clone(),
                    ActorNames = (string[])actorNames.Clone(),
                    FaceNodeIds = Array.ConvertAll(faceNodeIds, face => (int[])face.Clone()),
                    EdgeNodeIds = Array.ConvertAll(edgeNodeIds, edge => (int[])edge.Clone())
                };
            }
            PublishSelectionState();
        }

        public void ClearSelectionState()
        {
            _selectionState = VtkJsSelectionStateMessage.CreateEmpty();
            PublishSelectionState();
        }

        public void SetAnnotation(string name, string text, string numberFormat, double[] anchorPoint,
                                  bool drawBackground, bool drawBorder, bool visible)
        {
            if (name == "Min_Annotation" || name == "Max_Annotation")
            {
                if (string.IsNullOrWhiteSpace(numberFormat) || numberFormat.Length > 128)
                {
                    WriteTrace("ignored invalid extrema annotation format");
                    return;
                }
                _extremaAnnotationSettings[name] = new VtkJsExtremaAnnotationSettings
                {
                    NumberFormat = numberFormat,
                    DrawBackground = drawBackground,
                    DrawBorder = drawBorder,
                    Visible = visible
                };
                RefreshExtremaAnnotations();
                return;
            }
            if (string.IsNullOrWhiteSpace(name) || name.Length > 1024 || text == null || text.Length > 65536 ||
                anchorPoint == null || anchorPoint.Length != 3 ||
                Array.Exists(anchorPoint, value => double.IsNaN(value) || double.IsInfinity(value)))
            {
                WriteTrace("ignored invalid annotation update");
                return;
            }
            long existingLength = _annotations.TryGetValue(name, out VtkJsAnnotationMessage existing)
                ? existing.Text.Length : 0;
            long totalLength = _annotations.Values.Sum(annotation => (long)annotation.Text.Length) -
                               existingLength + text.Length;
            if ((!_annotations.ContainsKey(name) && _annotations.Count >= 4096) || totalLength > 1000000)
            {
                WriteTrace("ignored annotation update above safety bound");
                return;
            }
            _annotations[name] = new VtkJsAnnotationMessage
            {
                Name = name,
                Text = text,
                AnchorPoint = (double[])anchorPoint.Clone(),
                Offset = GetAnnotationOffset(name),
                DrawBackground = drawBackground,
                DrawBorder = drawBorder,
                Visible = visible
            };
            ScheduleAnnotationPublish();
        }

        public void RemoveAnnotations(string[] names)
        {
            if (names == null) return;
            foreach (string name in names)
            {
                if (name == null) continue;
                _annotations.Remove(name);
                _extremaAnnotationSettings.Remove(name);
                _annotationOffsets.Remove(name);
            }
            ScheduleAnnotationPublish();
        }

        public void ClearAnnotations()
        {
            _annotations.Clear();
            _extremaAnnotationSettings.Clear();
            _annotationOffsets.Clear();
            ScheduleAnnotationPublish();
        }

        public void UpdateActorExtrema(string actorName, NodesExchangeData extremeNodes)
        {
            if (string.IsNullOrWhiteSpace(actorName)) return;
            VtkJsActorExtremaState previous;
            bool exists = _actorExtrema.TryGetValue(actorName, out previous);
            if (!TryReadActorExtrema(extremeNodes, out VtkJsExtremeNode minimum, out VtkJsExtremeNode maximum))
            {
                _actorExtrema.Remove(actorName);
            }
            else
            {
                _actorExtrema[actorName] = new VtkJsActorExtremaState
                {
                    Minimum = minimum,
                    Maximum = maximum,
                    Visible = !exists || previous.Visible,
                    ColorContours = !exists || previous.ColorContours
                };
            }
            RefreshExtremaAnnotations();
        }

        public void SetActorColorContours(string[] actorNames, bool colorContours)
        {
            if (actorNames == null) return;
            foreach (string actorName in actorNames)
            {
                if (string.IsNullOrWhiteSpace(actorName) || actorName.Length > 1024) continue;
                _actorColorContours[actorName] = colorContours;
                if (_actorExtrema.TryGetValue(actorName, out VtkJsActorExtremaState state))
                    state.ColorContours = colorContours;
            }
            RefreshExtremaAnnotations();
            PublishActorContourState();
            PublishScalarLegendState();
        }

        public void HighlightActor(string actorName)
        {
            if (string.IsNullOrWhiteSpace(actorName) || actorName.Length > 1024 ||
                !_sceneCapture.ContainsActor(actorName)) return;
            if (_highlightedActorNames.Count >= 4096 && !_highlightedActorNames.Contains(actorName))
            {
                WriteTrace("ignored actor highlight above safety bound");
                return;
            }
            if (_highlightedActorNames.Add(actorName)) PublishActorHighlightState();
        }

        public void ClearActorHighlights()
        {
            if (_highlightedActorNames.Count == 0) return;
            _highlightedActorNames.Clear();
            PublishActorHighlightState();
        }

        private void PublishActorHighlightState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var message = new VtkJsActorHighlightStateMessage
                {
                    Type = "prepomax.actor-highlights.updated-v1",
                    ActorNames = _highlightedActorNames.OrderBy(name => name, StringComparer.Ordinal).ToArray()
                };
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsActorHighlightStateMessage));
                    serializer.WriteObject(stream, message);
                    _webView.CoreWebView2.PostWebMessageAsJson(Encoding.UTF8.GetString(stream.ToArray()));
                }
                WriteTrace("actor highlight state actors=" + message.ActorNames.Length);
            }
            catch (Exception exception)
            {
                WriteTrace("actor highlight state publish failed: " + exception.Message);
            }
        }

        private void PublishActorContourState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            var message = new VtkJsActorContourStateMessage
            {
                Type = "prepomax.actor-contours.updated-v1",
                Actors = _actorColorContours.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Take(4096)
                    .Select(entry => new VtkJsActorContourMessage
                    {
                        Name = entry.Key,
                        ColorContours = entry.Value
                    }).ToArray()
            };
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsActorContourStateMessage));
                serializer.WriteObject(stream, message);
                _webView.CoreWebView2.PostWebMessageAsJson(Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        public async void TraceAcceptanceSelectionOverlay()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                await Task.Delay(750);
                const string script = @"(() => {
                    try {
                        const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                            ?.renderer?.selection;
                        return value ? `${value.item}|${value.overlayActors}|${value.overflow}` : 'missing';
                    } catch { return 'invalid'; }
                })();";
                string encoded = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                string value;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(encoded)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(string));
                    value = serializer.ReadObject(stream) as string;
                }
                WriteTrace("acceptance: selection overlay " + value);
            }
            catch (Exception exception)
            {
                WriteTrace("acceptance: selection overlay check failed: " + exception.Message);
            }
        }

        public async void TraceAcceptanceSelectionState(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("selection state " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.selection;
                    return value ? `${value.item}|${value.selectedActorCount}|${value.overlayActors}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceLargeSelection(int expectedCount)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                await Task.Delay(1000);
                const string script = @"(() => {
                    try {
                        const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                            ?.renderer?.selection;
                        return value ? `${value.item}|${value.selectedIdCount}|${value.overlayActors}|${value.overflow}` : 'missing';
                    } catch { return 'invalid'; }
                })();";
                string encoded = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                string value;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(encoded)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(string));
                    value = serializer.ReadObject(stream) as string;
                }
                WriteTrace("acceptance: large selection expected=" + expectedCount + " actual=" + value);
            }
            catch (Exception exception)
            {
                WriteTrace("acceptance: large selection check failed: " + exception.Message);
            }
        }

        public async void TraceAcceptanceAnnotations()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                await Task.Delay(750);
                const string script = @"(() => {
                    try {
                        const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                            ?.renderer?.annotations;
                        return value ? `${value.count}|${value.visible}` : 'missing';
                    } catch { return 'invalid'; }
                })();";
                string encoded = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                string value;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(encoded)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(string));
                    value = serializer.ReadObject(stream) as string;
                }
                WriteTrace("acceptance: annotations " + value);
            }
            catch (Exception exception)
            {
                WriteTrace("acceptance: annotation check failed: " + exception.Message);
            }
        }

        public async void TraceAcceptanceSphereSymbols()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("sphere symbols", @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.sphereSymbols;
                    return value ? `${value.actors}|${value.points}|${value.scales.every(scale => scale > 0)}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceCoordinateOverlays()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("coordinate overlays", @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')?.renderer;
                    const caption = document.querySelector('.coordinate-caption[title=""VTK.js axis caption smoke""]');
                    return value ? `${value.coordinateAxes.actors}|${value.coordinateAxes.scales.every(scale => scale > 0)}|${value.captions.count}|${!!caption && !caption.hidden && caption.textContent === '<X & axis>'}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceDirectionalGlyphs()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("directional glyphs", @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.directionalGlyphs;
                    return value ? `${value.actors}|${value.points}|${value.scales.every(scale => scale > 0)}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceViewportAppearance()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("viewport appearance", @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.viewportAppearance;
                    return value ? `${value.gradient}|${value.topRgb.map(x => x.toFixed(6)).join(',')}|${value.bottomRgb.map(x => x.toFixed(6)).join(',')}|${value.ambient.toFixed(2)}|${value.diffuse.toFixed(2)}|${value.actorLighting.every(x => Math.abs(x.ambient - 0.35) < 1e-9 && Math.abs(x.diffuse - 0.65) < 1e-9)}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceOrientationMarker(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("orientation marker " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.orientationMarker;
                    return value ? `${value.visible}|${value.rendererActors}|${value.cameraAligned}|${value.maxPixelSize}|${value.xColor.map(x => x.toFixed(3)).join(',')}|${value.yColor.map(x => x.toFixed(3)).join(',')}|${value.zColor.map(x => x.toFixed(3)).join(',')}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceScaleWidget(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("scale widget " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.scaleWidget;
                    return value ? `${value.visible}|${value.unit}|${value.pixelWidth.toFixed(3)}|${value.worldLength}|${value.labels.length}|${value.labels[5]}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceScalarLegend(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("scalar legend " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.scalarLegend;
                    return value ? `${value.visible}|${value.fieldName}|${value.componentName}|${value.unit}|${value.minMaxType}|${value.numberFormat}|${value.minimum}|${value.maximum}|${value.colors}|${value.labels.length}|${value.drawBackground}|${value.drawBorder}|${value.text.replace(/\n/g, '~')}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceColorBar(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("color bar " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.colorBar;
                    const colors = value?.colors?.map(color => color.map(channel => channel.toFixed(3)).join(',')).join('~');
                    return value ? `${value.visible}|${value.entries}|${value.labels.join('~')}|${colors}|${value.drawBackground}|${value.drawBorder}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceStatusBlock(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("status block " + label, @"(() => {
                try {
                    const renderer = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')?.renderer;
                    const value = renderer?.statusBlock;
                    const layout = renderer?.resultWidgetLayout;
                    return value && layout ? `${value.visible}|${value.lines.join('~')}|${value.drawBackground}|${value.drawBorder}|${layout.scalarLegend.top},${layout.scalarLegend.left}|${layout.colorBar.top},${layout.colorBar.left}|${layout.statusBlock.top},${layout.statusBlock.right}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceGraphicsInteraction(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("graphics interaction " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.graphicsInteraction;
                    const rgb = color => color.map(channel => channel.toFixed(3)).join(',');
                    const renderer = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')?.renderer;
                    const highlightColors = renderer?.actorHighlights?.colors?.map(rgb).join('~') || '';
                    const transientColors = renderer?.transientPrimitives?.colors?.map(rgb).join('~') || '';
                    return value ? `${value.pointSmoothing}|${value.lineSmoothing}|${rgb(value.primaryHighlightRgb)}|${rgb(value.secondaryHighlightRgb)}|${rgb(value.mouseHighlightRgb)}|${value.rasterization}|${value.selectionColors.map(rgb).join('~')}|${highlightColors}|${transientColors}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceActorPresentation(string actorName)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null || string.IsNullOrWhiteSpace(actorName)) return;
            string actorNameJson = SerializeJsonString(actorName);
            await TraceAcceptanceScript("actor presentation", @"(() => {
                try {
                    const actors = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.actorColors;
                    const value = actors?.find(actor => actor.actorName === " + actorNameJson + @");
                    return value ? `${value.actorName}|${value.color.map(x => x.toFixed(6)).join(',')}|${value.opacity.toFixed(2)}|${value.visible}|${value.scalarVisibility}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceActorHighlights(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("actor highlights " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.actorHighlights;
                    return value ? `${value.actorNames.length}|${value.overlayActors}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceTransientSelection(string actorName, string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null || string.IsNullOrWhiteSpace(actorName)) return;
            string actorNameJson = SerializeJsonString(actorName);
            await TraceAcceptanceScript("transient selection " + label, @"(() => {
                try {
                    const diagnostics = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}');
                    const actors = diagnostics?.renderer?.actorColors;
                    const present = !!actors?.some(actor => actor.actorName === " + actorNameJson + @");
                    return `${present}|actors=${diagnostics?.actors ?? -1}`;
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceTransientPrimitives(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("transient primitives " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.transientPrimitives;
                    return value ? `${value.actors}|${value.points}|${value.lines}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceAuxiliaryPrimitives(string label)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("auxiliary primitives " + label, @"(() => {
                try {
                    const value = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')
                        ?.renderer?.transientPrimitives;
                    return value ? `${value.actors}|${value.visibleActors}|${value.points}|${value.lines}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceGenericConstruction(string surfaceName, string pointName)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            string surface = SerializeJsonString(surfaceName);
            string point = SerializeJsonString(pointName);
            await TraceAcceptanceScript("generic construction", @"(() => {
                try {
                    const renderer = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')?.renderer;
                    const actor = renderer?.actorColors?.find(value => value.actorName === " + surface + @");
                    const primitiveNames = renderer?.transientPrimitives?.actorNames || [];
                    return `${actor?.pickable === true}|${primitiveNames.includes(" + point + @")}`;
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceCellClassification(string surfaceName, string polylineName)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            string surface = SerializeJsonString(surfaceName);
            string polyline = SerializeJsonString(polylineName);
            await TraceAcceptanceScript("cell classification", @"(() => {
                try {
                    const renderer = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}')?.renderer;
                    const surfacePresent = renderer?.actorColors?.some(value => value.actorName === " + surface + @");
                    const primitiveNames = renderer?.transientPrimitives?.actorNames || [];
                    return `${surfacePresent === true}|${primitiveNames.includes(" + polyline + @")}`;
                } catch { return 'invalid'; }
            })();");
        }

        public async void TraceAcceptanceSceneCleared()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("scene cleared", @"(() => {
                try {
                    const diagnostics = JSON.parse(document.querySelector('#diagnostics')?.textContent || '{}');
                    const value = diagnostics?.renderer;
                    const status = document.querySelector('#status')?.value || '';
                    const error = document.querySelector('#error')?.textContent || '';
                    return value ? `${value.pipelineChunks}|${value.selection.overlayActors}|${value.actorHighlights.overlayActors}|${value.annotations.count}|${value.sphereSymbols.actors}|${value.coordinateAxes.actors}|${value.captions.count}|${value.directionalGlyphs.actors}|${value.explodedPreview.actorCount}|${value.sectionView.active}|actors=${diagnostics.actors}|status=${status}|error=${error}` : 'missing';
                } catch { return 'invalid'; }
            })();");
        }

        public void SetViewportBackground(bool gradient, Color topColor, Color bottomColor, bool redraw)
        {
            _viewportAppearanceState.Gradient = gradient;
            _viewportAppearanceState.TopRgb = ColorToRgb(topColor);
            _viewportAppearanceState.BottomRgb = ColorToRgb(bottomColor);
            _viewportAppearanceState.Redraw = redraw;
            PublishViewportAppearanceState(false);
        }

        public void SetViewportLighting(double ambient, double diffuse, bool redraw)
        {
            if (!IsUnitValue(ambient))
                throw new ArgumentOutOfRangeException("ambient", "Viewport lighting values must be finite and between zero and one.");
            if (!IsUnitValue(diffuse))
                throw new ArgumentOutOfRangeException("diffuse", "Viewport lighting values must be finite and between zero and one.");
            _viewportAppearanceState.Ambient = ambient;
            _viewportAppearanceState.Diffuse = diffuse;
            _viewportAppearanceState.Redraw = redraw;
            PublishViewportAppearanceState(false);
        }

        public void SetOrientationMarkerVisibility(bool visible)
        {
            _orientationMarkerVisible = visible;
            PublishOrientationMarkerState();
        }

        private void PublishOrientationMarkerState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsOrientationMarkerMessage
                {
                    Type = "prepomax.orientation-marker.updated-v1",
                    Visible = _orientationMarkerVisible
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsOrientationMarkerMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("orientation marker visible=" + _orientationMarkerVisible);
            }
            catch (Exception exception)
            {
                WriteTrace("orientation marker publish failed: " + exception.Message);
            }
        }

        public void SetScaleWidget(bool? visible, string unit)
        {
            if (visible.HasValue) _scaleWidgetVisible = visible.Value;
            if (unit != null)
            {
                if (unit.Length > 64 || unit.Any(character => char.IsControl(character)))
                    throw new ArgumentException("Scale-widget units must be at most 64 printable characters.", "unit");
                _scaleWidgetUnit = unit;
            }
            PublishScaleWidgetState();
        }

        private void PublishScaleWidgetState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsScaleWidgetMessage
                {
                    Type = "prepomax.scale-widget.updated-v1",
                    Visible = _scaleWidgetVisible,
                    Unit = _scaleWidgetUnit
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsScaleWidgetMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("scale widget visible=" + _scaleWidgetVisible + " unit='" + _scaleWidgetUnit + "'");
            }
            catch (Exception exception)
            {
                WriteTrace("scale widget publish failed: " + exception.Message);
            }
        }

        public void SetScalarLegendText(string fieldName, string componentName, string unit,
                                        string complexComponent, string minMaxType)
        {
            _scalarLegendFieldName = ValidateLegendText(fieldName, "fieldName", "PrePoMax values");
            _scalarLegendComponentName = ValidateLegendText(componentName, "componentName", "value");
            _scalarLegendUnit = ValidateLegendText(unit, "unit", "");
            _scalarLegendComplexComponent = ValidateLegendText(complexComponent, "complexComponent", "");
            _scalarLegendMinMaxType = ValidateLegendText(minMaxType, "minMaxType", "Automatic");
            _scalarLegendVisible = true;
            PublishScalarLegendState();
        }

        public void SetScalarLegendVisibility(bool visible)
        {
            _scalarLegendVisible = visible;
            PublishScalarLegendState();
        }

        public void SetScalarLegendNumberFormat(string numberFormat)
        {
            if (string.IsNullOrEmpty(numberFormat) || numberFormat.Length != 2 ||
                (numberFormat[0] != 'G' && numberFormat[0] != 'E') ||
                numberFormat[1] < '1' || numberFormat[1] > '8')
                throw new ArgumentException("Scalar-legend number format must be G1-G8 or E1-E8.", "numberFormat");
            _scalarLegendNumberFormat = numberFormat;
            PublishScalarLegendState();
        }

        public void SetScalarLegendAppearance(bool? drawBackground, bool? drawBorder)
        {
            if (drawBackground.HasValue) _scalarLegendDrawBackground = drawBackground.Value;
            if (drawBorder.HasValue) _scalarLegendDrawBorder = drawBorder.Value;
            PublishScalarLegendState();
        }

        private void PublishScalarLegendState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                double minimum;
                double maximum;
                bool allFramesRange = _useAllFramesScalarRange && _animationScalarRange.Length >= 2 &&
                                      IsFinite(_animationScalarRange[0]) && IsFinite(_animationScalarRange[1]);
                if (allFramesRange)
                {
                    minimum = _animationScalarRange[0];
                    maximum = _animationScalarRange[1];
                }
                else if (!_sceneCapture.TryGetScalarRange(_hiddenActorNames, _actorColorContours,
                                                          out minimum, out maximum) ||
                         double.IsInfinity(minimum) || double.IsInfinity(maximum))
                {
                    minimum = 0;
                    maximum = 1;
                }
                vtkControl.vtkMaxLookupTable lookup = VtkJsExportLookupTable.Create(_colorSpectrum, minimum, maximum);
                var state = new VtkJsScalarLegendMessage
                {
                    Type = "prepomax.scalar-legend.updated-v1", Visible = _scalarLegendVisible,
                    FieldName = _scalarLegendFieldName, ComponentName = _scalarLegendComponentName,
                    Unit = _scalarLegendUnit,
                    ComplexComponent = _scalarLegendComplexComponent, MinMaxType = _scalarLegendMinMaxType,
                    NumberFormat = _scalarLegendNumberFormat,
                    DrawBackground = _scalarLegendDrawBackground, DrawBorder = _scalarLegendDrawBorder,
                    Minimum = lookup.MinimumValue, Maximum = lookup.MaximumValue,
                    Colors = lookup.Colors.Select(color => new double[] {
                        color.R / 255.0, color.G / 255.0, color.B / 255.0 }).ToArray()
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsScalarLegendMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("scalar legend visible=" + state.Visible + " colors=" + state.Colors.Length +
                           " range=" + state.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                           ".." + state.Maximum.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception exception)
            {
                WriteTrace("scalar legend publish failed: " + exception.Message);
            }
        }

        private static string ValidateLegendText(string value, string parameterName, string fallback)
        {
            string text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (text.Length > 256 || text.Any(character =>
                (character < ' ' && character != '\n' && character != '\r' && character != '\t') || character == 127))
                throw new ArgumentException("Scalar-legend text must be at most 256 safe characters.", parameterName);
            return text;
        }

        public void SetColorBarItems(Color[] colors, string[] labels, bool append)
        {
            if (colors == null) throw new ArgumentNullException("colors");
            if (labels == null) throw new ArgumentNullException("labels");
            if (colors.Length != labels.Length)
                throw new ArgumentException("Categorical color-bar colors and labels must have equal lengths.");
            if (colors.Length == 0)
            {
                if (!append)
                {
                    _colorBarColors.Clear();
                    _colorBarLabels.Clear();
                    _colorBarVisible = false;
                    PublishColorBarState();
                }
                return;
            }
            if (colors.Length > 4096 || (append && _colorBarColors.Count > 4096 - colors.Length))
                throw new ArgumentOutOfRangeException("colors", "Categorical color bars are limited to 4096 entries.");
            var normalizedLabels = labels.Select((label, index) =>
                ValidateColorBarLabel(label, "labels[" + index + "]")).ToArray();
            if (!append)
            {
                _colorBarColors.Clear();
                _colorBarLabels.Clear();
            }
            _colorBarColors.AddRange(colors);
            _colorBarLabels.AddRange(normalizedLabels);
            _colorBarVisible = true;
            PublishColorBarState();
        }

        public void SetColorBarAppearance(bool? drawBackground, bool? drawBorder)
        {
            if (drawBackground.HasValue) _colorBarDrawBackground = drawBackground.Value;
            if (drawBorder.HasValue) _colorBarDrawBorder = drawBorder.Value;
            PublishColorBarState();
        }

        public void HideColorBar()
        {
            _colorBarVisible = false;
            _colorBarColors.Clear();
            _colorBarLabels.Clear();
            PublishColorBarState();
        }

        private void PublishColorBarState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsColorBarMessage
                {
                    Type = "prepomax.color-bar.updated-v1",
                    Visible = _colorBarVisible && _colorBarColors.Count > 0,
                    DrawBackground = _colorBarDrawBackground,
                    DrawBorder = _colorBarDrawBorder,
                    Colors = _colorBarColors.Select(color => new double[] {
                        color.R / 255.0, color.G / 255.0, color.B / 255.0 }).ToArray(),
                    Labels = _colorBarLabels.ToArray()
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsColorBarMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("color bar visible=" + state.Visible + " entries=" + state.Labels.Length);
            }
            catch (Exception exception)
            {
                WriteTrace("color bar publish failed: " + exception.Message);
            }
        }

        private static string ValidateColorBarLabel(string value, string parameterName)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            string label = value.Replace('_', ' ').Replace('-', ' ');
            if (label.Length > 256 || label.Any(character => char.IsControl(character)))
                throw new ArgumentException("Color-bar labels must be at most 256 printable characters.", parameterName);
            return label;
        }

        public void InitializeResultWidgetPositions()
        {
            _resultWidgetLayoutState.ScalarLegend.Top = 20;
            _resultWidgetLayoutState.ScalarLegend.Left = 20;
            _resultWidgetLayoutState.StatusBlock.Top = 20;
            _resultWidgetLayoutState.StatusBlock.Right = 20;
            PublishResultWidgetLayoutState();
        }

        public void InitializeColorBarWidgetPosition()
        {
            _resultWidgetLayoutState.ColorBar.Top = 20;
            _resultWidgetLayoutState.ColorBar.Left = 20;
            PublishResultWidgetLayoutState();
        }

        public void SetStatusBlockVisibility(bool visible)
        {
            _statusBlockDrawEnabled = visible;
            if (!visible) _statusBlockVisible = false;
            PublishStatusBlockState();
        }

        public void SetStatusBlockAppearance(bool? drawBackground, bool? drawBorder)
        {
            if (drawBackground.HasValue) _statusBlockDrawBackground = drawBackground.Value;
            if (drawBorder.HasValue) _statusBlockDrawBorder = drawBorder.Value;
            PublishStatusBlockState();
        }

        public void SetStatusBlock(string name, DateTime dateTime, float analysisTime, string unit,
                                   string deformationVariable, float scaleFactor,
                                   vtkControl.vtkMaxFieldDataType fieldType, int stepNumber, int incrementNumber)
        {
            _statusBlockName = ValidateStatusText(name, 512, "name");
            _statusBlockUnit = ValidateStatusText(unit, 64, "unit");
            _statusBlockDeformationVariable = ValidateStatusText(deformationVariable, 256, "deformationVariable");
            if (float.IsNaN(analysisTime) || float.IsInfinity(analysisTime) ||
                float.IsNaN(scaleFactor) || float.IsInfinity(scaleFactor))
                throw new ArgumentOutOfRangeException("Status-block numeric values must be finite.");
            if (!Enum.IsDefined(typeof(vtkControl.vtkMaxFieldDataType), fieldType))
                throw new ArgumentOutOfRangeException("fieldType");
            _statusBlockDateTime = dateTime;
            _statusBlockAnalysisTime = analysisTime;
            _statusBlockDeformationScaleFactor = scaleFactor;
            _statusBlockFieldType = fieldType;
            _statusBlockStepNumber = stepNumber;
            _statusBlockIncrementNumber = incrementNumber;
            _statusBlockAnimationScaleFactor = -1;
            _statusBlockInitialized = true;
            _statusBlockVisible = _statusBlockDrawEnabled;
            PublishStatusBlockState();
        }

        private void UpdateStatusBlockAnimationFrame(int frameNumber)
        {
            if (!_statusBlockInitialized || _animationTime.Length == 0 || frameNumber < 0 ||
                frameNumber >= _animationTime.Length) return;
            _statusBlockStepNumber = _animationStepId[frameNumber];
            _statusBlockIncrementNumber = _animationStepIncrementId[frameNumber];
            _statusBlockAnalysisTime = _animationTime[frameNumber];
            _statusBlockAnimationScaleFactor = _animationScale[frameNumber];
            _statusBlockAnimationType = _animationType;
            PublishStatusBlockState();
        }

        private void HideStatusBlock()
        {
            _statusBlockVisible = false;
            PublishStatusBlockState();
        }

        private void PublishStatusBlockState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsStatusBlockMessage
                {
                    Type = "prepomax.status-block.updated-v1",
                    Visible = _statusBlockVisible && _statusBlockInitialized,
                    DrawBackground = _statusBlockDrawBackground,
                    DrawBorder = _statusBlockDrawBorder,
                    Lines = _statusBlockInitialized ? BuildStatusBlockLines() : new string[0]
                };
                PostBrowserState(state, typeof(VtkJsStatusBlockMessage));
                WriteTrace("status block visible=" + state.Visible + " lines=" + state.Lines.Length);
            }
            catch (Exception exception) { WriteTrace("status block publish failed: " + exception.Message); }
        }

        private string[] BuildStatusBlockLines()
        {
            string dateFormat = System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern;
            string line1 = "Name: " + _statusBlockName + "   Date: " + _statusBlockDateTime.ToString(dateFormat) +
                           "   Time: " + _statusBlockDateTime.ToString("HH:mm:ss");
            string line2;
            switch (_statusBlockFieldType)
            {
                case vtkControl.vtkMaxFieldDataType.Static:
                    line2 = "Step: #" + _statusBlockStepNumber + "   Increment: #" + _statusBlockIncrementNumber +
                            "   Analysis time: " + _statusBlockAnalysisTime + " " + _statusBlockUnit; break;
                case vtkControl.vtkMaxFieldDataType.Frequency:
                case vtkControl.vtkMaxFieldDataType.FrequencySensitivity:
                    line2 = "Step: #" + _statusBlockStepNumber + "   Mode: #" + _statusBlockIncrementNumber +
                            "   Frequency: " + _statusBlockAnalysisTime + " " + _statusBlockUnit; break;
                case vtkControl.vtkMaxFieldDataType.Buckling:
                    line2 = "Step: #" + _statusBlockStepNumber + "   Buckling factor: " + _statusBlockAnalysisTime; break;
                case vtkControl.vtkMaxFieldDataType.SteadyStateDynamic:
                    line2 = "Step: #" + _statusBlockStepNumber + "   Data point: #" + _statusBlockIncrementNumber +
                            "   Frequency: " + _statusBlockAnalysisTime + " " + _statusBlockUnit; break;
                case vtkControl.vtkMaxFieldDataType.LastIterations:
                    line2 = "Increment: #" + _statusBlockStepNumber + "   Iteration: #" + _statusBlockIncrementNumber +
                            "   Analysis time: " + _statusBlockAnalysisTime + " " + _statusBlockUnit; break;
                default: throw new NotSupportedException();
            }
            string line3 = "Deformation variable: " + _statusBlockDeformationVariable +
                           "   Deformation scale factor: " + _statusBlockDeformationScaleFactor;
            var lines = new List<string> { line1, line2, line3 };
            if (_statusBlockAnimationScaleFactor >= 0)
            {
                if (_statusBlockAnimationType == vtkControl.vtkMaxAnimationType.ScaleFactor)
                    lines.Add("Animation scale factor: " + _statusBlockAnimationScaleFactor);
                else if (_statusBlockAnimationType == vtkControl.vtkMaxAnimationType.Harmonic)
                    lines.Add("Complex angle: " + new StringAngleDegConverter().ConvertToString(_statusBlockAnimationScaleFactor));
            }
            return lines.ToArray();
        }

        private void PublishResultWidgetLayoutState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                PostBrowserState(_resultWidgetLayoutState, typeof(VtkJsResultWidgetLayoutMessage));
                WriteTrace("result widget layout published");
            }
            catch (Exception exception) { WriteTrace("result widget layout publish failed: " + exception.Message); }
        }

        private void PostBrowserState(object state, Type type)
        {
            string json;
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(type);
                serializer.WriteObject(stream, state);
                json = Encoding.UTF8.GetString(stream.ToArray());
            }
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private static string ValidateStatusText(string value, int maximumLength, string parameterName)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            if (value.Length > maximumLength || value.Any(character => char.IsControl(character)))
                throw new ArgumentException("Status-block text is too long or contains control characters.", parameterName);
            return value;
        }

        public void SetGraphicsSmoothing(bool pointSmoothing, bool lineSmoothing)
        {
            _pointSmoothing = pointSmoothing;
            _lineSmoothing = lineSmoothing;
            PublishGraphicsInteractionState();
        }

        public async void SetHighlightColors(Color primaryHighlightColor, Color secondaryHighlightColor)
        {
            _primaryHighlightColor = primaryHighlightColor;
            _secondaryHighlightColor = secondaryHighlightColor;
            foreach (VtkJsTransientPrimitiveMessage primitive in _transientPrimitives.Values)
                primitive.Rgba = ColorToRgba(primitive.UseSecondaryHighlightColor
                    ? _secondaryHighlightColor : _primaryHighlightColor);
            if (_transientPrimitives.Count > 0) ScheduleTransientPrimitivePublish();
            foreach (KeyValuePair<string, bool> entry in _transientSelectionSecondary.ToArray())
            {
                Color color = GetSurfaceHighlightColor(entry.Value);
                _sceneCapture.UpdateActor(entry.Key, entry.Key, color);
                if (IsBrowserReady && _started)
                {
                    try { await _host.UpdateActorAsync(entry.Key, entry.Key, color.ToArgb()); }
                    catch (Exception exception) { WriteTrace("selection highlight color update failed: " + exception.Message); }
                }
            }
            PublishGraphicsInteractionState();
        }

        public void SetMouseHighlightColor(Color mouseHighlightColor)
        {
            _mouseHighlightColor = mouseHighlightColor;
            PublishGraphicsInteractionState();
        }

        private Color GetHighlightColor(bool secondary)
        {
            return secondary ? _secondaryHighlightColor : _primaryHighlightColor;
        }

        private Color GetSurfaceHighlightColor(bool secondary)
        {
            Color source = GetHighlightColor(secondary);
            return Color.FromArgb(255, (int)Math.Round(source.R * 0.7), (int)Math.Round(source.G * 0.7),
                                  (int)Math.Round(source.B * 0.7));
        }

        private void PublishGraphicsInteractionState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsGraphicsInteractionMessage
                {
                    Type = "prepomax.graphics-interaction.updated-v1",
                    PointSmoothing = _pointSmoothing,
                    LineSmoothing = _lineSmoothing,
                    PrimaryHighlightRgb = ColorToRgb(_primaryHighlightColor),
                    SecondaryHighlightRgb = ColorToRgb(_secondaryHighlightColor),
                    MouseHighlightRgb = ColorToRgb(_mouseHighlightColor)
                };
                PostBrowserState(state, typeof(VtkJsGraphicsInteractionMessage));
                WriteTrace("graphics interaction smoothing=" + _pointSmoothing + "," + _lineSmoothing);
            }
            catch (Exception exception) { WriteTrace("graphics interaction publish failed: " + exception.Message); }
        }

        private void PublishViewportAppearanceState(bool forceRedraw)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var publishedState = new VtkJsViewportAppearanceMessage
                {
                    Type = _viewportAppearanceState.Type,
                    Gradient = _viewportAppearanceState.Gradient,
                    TopRgb = (double[])_viewportAppearanceState.TopRgb.Clone(),
                    BottomRgb = (double[])_viewportAppearanceState.BottomRgb.Clone(),
                    Ambient = _viewportAppearanceState.Ambient,
                    Diffuse = _viewportAppearanceState.Diffuse,
                    Redraw = forceRedraw || _viewportAppearanceState.Redraw
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsViewportAppearanceMessage));
                    serializer.WriteObject(stream, publishedState);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("viewport appearance gradient=" + _viewportAppearanceState.Gradient +
                           " ambient=" + _viewportAppearanceState.Ambient.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                           " diffuse=" + _viewportAppearanceState.Diffuse.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception exception)
            {
                WriteTrace("viewport appearance publish failed: " + exception.Message);
            }
        }

        private static double[] ColorToRgb(Color color)
        {
            return new[] { color.R / 255.0, color.G / 255.0, color.B / 255.0 };
        }

        private static double[] ColorToRgba(Color color)
        {
            return new[] { color.R / 255.0, color.G / 255.0, color.B / 255.0, 1.0 };
        }

        private static bool IsUnitValue(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 1;
        }

        public async void TraceAcceptanceAnnotationIdentity()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("regular annotation", @"(() => {
                const element = document.querySelector('.annotation-callout[title=""Measure_Annotation""]');
                return String(!!element && !element.hidden && element.textContent === 'VTK.js annotation smoke');
            })();");
        }

        public async void TraceAcceptanceExtrema()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            await TraceAcceptanceScript("extrema", @"(() => {
                const min = document.querySelector('.annotation-callout[title=""Min_Annotation""]');
                const max = document.querySelector('.annotation-callout[title=""Max_Annotation""]');
                const valid = (element, prefix) => !!element && !element.hidden &&
                    (element.textContent || '').startsWith(prefix) && (element.textContent || '').includes('Node id:');
                return String(valid(min, 'Min:') && valid(max, 'Max:'));
            })();");
        }

        public async void TriggerAcceptanceAnnotationInteraction(string name)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null || string.IsNullOrWhiteSpace(name)) return;
            try
            {
                await Task.Delay(250);
                string nameJson = SerializeJsonString(name);
                string script = @"(() => {
                    const element = [...document.querySelectorAll('.annotation-callout')]
                        .find(candidate => candidate.title === " + nameJson + @");
                    if (!element) return 'missing';
                    const pointer = (type, x, y) => element.dispatchEvent(new PointerEvent(type,
                        { bubbles: true, pointerId: 41, button: 0, clientX: x, clientY: y }));
                    pointer('pointerdown', 10, 10);
                    pointer('pointermove', 35, 30);
                    pointer('pointerup', 35, 30);
                    element.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, button: 0 }));
                    element.textContent = 'VTK.js edited annotation';
                    element.dispatchEvent(new FocusEvent('blur'));
                    return 'sent';
                })();";
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
                WriteTrace("acceptance: annotation interaction triggered name='" + name + "'");
                await Task.Delay(1000);
                string verifyScript = @"(() => {
                    const element = [...document.querySelectorAll('.annotation-callout')]
                        .find(candidate => candidate.title === " + nameJson + @");
                    return String(!!element && !element.hidden &&
                        element.textContent === 'VTK.js edited annotation');
                })();";
                string encoded = await _webView.CoreWebView2.ExecuteScriptAsync(verifyScript);
                string value;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(encoded)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(string));
                    value = serializer.ReadObject(stream) as string;
                }
                WriteTrace("acceptance: annotation interaction " + value);
                string actionScriptPrefix = @"window.chrome?.webview?.postMessage(JSON.stringify({
                    type: 'prepomax.annotation.action-v1', name: " + nameJson + @", action: '";
                await _webView.CoreWebView2.ExecuteScriptAsync(actionScriptPrefix + "reset'}));");
                await Task.Delay(500);
                await _webView.CoreWebView2.ExecuteScriptAsync(actionScriptPrefix + "delete'}));");
                await Task.Delay(500);
                string deletedEncoded = await _webView.CoreWebView2.ExecuteScriptAsync(@"(() => String(![...document
                    .querySelectorAll('.annotation-callout')].some(candidate => candidate.title === " + nameJson + @")))();");
                string deleted;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(deletedEncoded)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(string));
                    deleted = serializer.ReadObject(stream) as string;
                }
                WriteTrace("acceptance: annotation context actions " + deleted);
            }
            catch (Exception exception)
            {
                WriteTrace("acceptance: annotation interaction failed: " + exception.Message);
            }
        }

        public async void TriggerAcceptanceAnnotationSettings(string name)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null || string.IsNullOrWhiteSpace(name)) return;
            try
            {
                await Task.Delay(250);
                string nameJson = SerializeJsonString(name);
                string script = @"window.chrome?.webview?.postMessage(JSON.stringify({
                    type: 'prepomax.annotation.action-v1', name: " + nameJson + @", action: 'settings'}));";
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
                WriteTrace("acceptance: annotation settings triggered name='" + name + "'");
            }
            catch (Exception exception)
            {
                WriteTrace("acceptance: annotation settings failed: " + exception.Message);
            }
        }

        private static string SerializeJsonString(string value)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(string));
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private async Task TraceAcceptanceScript(string label, string script)
        {
            try
            {
                await Task.Delay(750);
                string encoded = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                string value;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(encoded)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(string));
                    value = serializer.ReadObject(stream) as string;
                }
                WriteTrace("acceptance: " + label + " " + value);
            }
            catch (Exception exception)
            {
                WriteTrace("acceptance: " + label + " check failed: " + exception.Message);
            }
        }

        private void PublishSelectionState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                if (RequiresSharedSelection(_selectionState))
                {
                    byte[] payload = EncodeSharedSelection(_selectionState);
                    using (CoreWebView2SharedBuffer buffer = _webView.CoreWebView2.Environment.CreateSharedBuffer(
                               checked((ulong)payload.Length)))
                    {
                        using (Stream stream = buffer.OpenStream()) stream.Write(payload, 0, payload.Length);
                        string metadata = "{\"type\":\"prepomax.selection.shared-v1\",\"byteLength\":" +
                                          payload.Length + "}";
                        _webView.CoreWebView2.PostSharedBufferToScript(buffer,
                            CoreWebView2SharedBufferAccess.ReadOnly, metadata);
                    }
                    WriteTrace("selection shared item=" + _selectionState.Item +
                               " ids=" + _selectionState.Ids.Length + " bytes=" + payload.Length);
                    return;
                }
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsSelectionStateMessage));
                    serializer.WriteObject(stream, _selectionState);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("selection state item=" + _selectionState.Item +
                           " ids=" + _selectionState.Ids.Length +
                           " actors=" + _selectionState.ActorNames.Length);
            }
            catch (Exception exception)
            {
                WriteTrace("selection state publish failed: " + exception.Message);
            }
        }

        private static bool RequiresSharedSelection(VtkJsSelectionStateMessage state)
        {
            return state.Ids.Length > 100000 || state.FaceNodeIds.Length > 100000 ||
                   state.EdgeNodeIds.Length > 100000 ||
                   state.FaceNodeIds.Sum(face => (long)face.Length) > 1000000 ||
                   state.EdgeNodeIds.Sum(edge => (long)edge.Length) > 300000;
        }

        private static byte[] EncodeSharedSelection(VtkJsSelectionStateMessage state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
            {
                writer.Write(Encoding.ASCII.GetBytes("PMXSEL1\0"));
                writer.Write(1);
                writer.Write(GetSelectionItemCode(state.Item));
                writer.Write(state.Ids.Length);
                writer.Write(state.ActorNames.Length);
                writer.Write(state.FaceNodeIds.Length);
                writer.Write(checked((int)state.FaceNodeIds.Sum(face => (long)face.Length)));
                writer.Write(state.EdgeNodeIds.Length);
                writer.Write(checked((int)state.EdgeNodeIds.Sum(edge => (long)edge.Length)));
                foreach (int id in state.Ids) writer.Write(id);
                foreach (string actorName in state.ActorNames)
                {
                    byte[] name = new UTF8Encoding(false, true).GetBytes(actorName);
                    if (name.Length == 0 || name.Length > 4096) throw new InvalidDataException("Invalid actor name.");
                    writer.Write(name.Length);
                    writer.Write(name);
                }
                foreach (int[] face in state.FaceNodeIds)
                {
                    writer.Write(face.Length);
                    foreach (int id in face) writer.Write(id);
                }
                foreach (int[] edge in state.EdgeNodeIds)
                {
                    writer.Write(edge.Length);
                    foreach (int id in edge) writer.Write(id);
                }
                writer.Flush();
                if (stream.Length > MaximumSharedSelectionBytes)
                    throw new InvalidDataException("Shared selection exceeds the 64 MiB safety bound.");
                return stream.ToArray();
            }
        }

        private static int GetSelectionItemCode(string item)
        {
            switch (item)
            {
                case "none": return 0;
                case "node": return 1;
                case "element": return 2;
                case "surface": return 3;
                case "geometry-edge": return 4;
                case "geometry": return 5;
                case "part": return 6;
                default: throw new InvalidDataException("Unsupported shared selection item.");
            }
        }

        private void ScheduleAnnotationPublish()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            _annotationPublishTimer.Stop();
            _annotationPublishTimer.Start();
        }

        private void PublishAnnotationState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsAnnotationStateMessage
                {
                    Type = "prepomax.annotations.updated-v1",
                    Annotations = _annotations.Values.OrderBy(annotation => annotation.Name, StringComparer.Ordinal)
                        .ToArray()
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsAnnotationStateMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("annotation state count=" + state.Annotations.Length);
            }
            catch (Exception exception)
            {
                WriteTrace("annotation state publish failed: " + exception.Message);
            }
        }

        public void AddSphereSymbols(vtkControl.vtkMaxActorData actorData, double symbolSize)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000)
                throw new ArgumentException("Invalid sphere-symbol actor data.");
            string name = actorData.Name + ActorNameSeparator + "sphere";
            double[][] points = new double[actorData.Geometry.Nodes.Coor.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                double[] point = actorData.Geometry.Nodes.Coor[i];
                if (!IsFinitePoint(point) || Array.Exists(point, value => Math.Abs(value) > 1e150))
                    throw new ArgumentException("Invalid sphere-symbol coordinates.");
                points[i] = (double[])point.Clone();
            }
            int existingPoints = _sphereSymbols.TryGetValue(name, out VtkJsSphereSymbolMessage existing)
                ? existing.Points.Length : 0;
            long totalPoints = _sphereSymbols.Values.Sum(symbol => (long)symbol.Points.Length) - existingPoints + points.Length;
            if (_sphereSymbols.Count + (existing == null ? 1 : 0) > 4096 || totalPoints > 100000)
                throw new InvalidOperationException("Sphere-symbol state exceeds its safety bound.");
            Color color = actorData.Color;
            _sphereSymbols[name] = new VtkJsSphereSymbolMessage
            {
                Name = name,
                Points = points,
                Rgba = new double[] { color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0 },
                SymbolSize = symbolSize
            };
            ScheduleSphereSymbolPublish();
        }

        public void ClearSphereSymbols()
        {
            if (_sphereSymbols.Count == 0) return;
            _sphereSymbols.Clear();
            ScheduleSphereSymbolPublish();
        }

        private void ScheduleSphereSymbolPublish()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            _sphereSymbolPublishTimer.Stop();
            _sphereSymbolPublishTimer.Start();
        }

        private void PublishSphereSymbolState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsSphereSymbolStateMessage
                {
                    Type = "prepomax.sphere-symbols.updated-v1",
                    Symbols = _sphereSymbols.Values.OrderBy(symbol => symbol.Name, StringComparer.Ordinal).ToArray()
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsSphereSymbolStateMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("sphere symbol state actors=" + state.Symbols.Length +
                           " points=" + state.Symbols.Sum(symbol => symbol.Points.Length));
            }
            catch (Exception exception)
            {
                WriteTrace("sphere symbol state publish failed: " + exception.Message);
            }
        }

        public bool CaptureTransientPrimitive(vtkControl.vtkMaxActorData actorData)
        {
            return CapturePrimitive(actorData, false);
        }

        public bool CaptureAuxiliaryPrimitive(vtkControl.vtkMaxActorData actorData)
        {
            return CapturePrimitive(actorData, true);
        }

        private bool CapturePrimitive(vtkControl.vtkMaxActorData actorData, bool auxiliary)
        {
            try
            {
                if (actorData == null || (!auxiliary && actorData.Layer != vtkControl.vtkRendererLayer.Selection) ||
                    (auxiliary && (actorData.Layer == vtkControl.vtkRendererLayer.Selection || actorData.Pickable)) ||
                    string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                    actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                    actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                    actorData.Geometry.Nodes.Coor.Length > 500000 ||
                    float.IsNaN(actorData.NodeSize) || float.IsInfinity(actorData.NodeSize) ||
                    actorData.NodeSize > 1000 ||
                    float.IsNaN(actorData.LineWidth) || float.IsInfinity(actorData.LineWidth) ||
                    actorData.LineWidth > 1000 ||
                    _sceneCapture.ContainsActor(actorData.Name) ||
                    _transientSelectionActorNames.Contains(actorData.Name) ||
                    (auxiliary ? _transientPrimitives.ContainsKey(actorData.Name) :
                                 _auxiliaryPrimitives.ContainsKey(actorData.Name))) return false;
                Dictionary<string, VtkJsTransientPrimitiveMessage> target =
                    auxiliary ? _auxiliaryPrimitives : _transientPrimitives;
                double[][] points = actorData.Geometry.Nodes.Coor.Select(point =>
                {
                    if (!IsFinitePoint(point) || point.Any(value => Math.Abs(value) > 1e150))
                        throw new ArgumentException("Invalid transient-primitive coordinates.");
                    return (double[])point.Clone();
                }).ToArray();
                var lines = new List<int[]>();
                bool drawPoints = false;
                CaeGlobals.CellsExchangeData cells = actorData.Geometry.Cells;
                if (cells == null || cells.CellNodeIds == null || cells.CellNodeIds.Length == 0)
                    drawPoints = true;
                else
                {
                    if (cells.Types == null || cells.Types.Length != cells.CellNodeIds.Length) return false;
                    for (int cell = 0; cell < cells.CellNodeIds.Length; cell++)
                    {
                        int[] nodes = cells.CellNodeIds[cell];
                        if (nodes == null || nodes.Any(index => index < 0 || index >= points.Length)) return false;
                        switch (cells.Types[cell])
                        {
                            case 1: // VTK_VERTEX
                            case 2: // VTK_POLY_VERTEX
                                drawPoints = true;
                                break;
                            case 3: // VTK_LINE
                                if (nodes.Length != 2) return false;
                                lines.Add(new[] { nodes[0], nodes[1] });
                                break;
                            case 4: // VTK_POLY_LINE
                                if (nodes.Length < 2) return false;
                                for (int index = 0; index < nodes.Length - 1; index++)
                                    lines.Add(new[] { nodes[index], nodes[index + 1] });
                                break;
                            case 21: // VTK_QUADRATIC_EDGE: endpoint, endpoint, midpoint
                                if (nodes.Length != 3) return false;
                                lines.Add(new[] { nodes[0], nodes[2] });
                                lines.Add(new[] { nodes[2], nodes[1] });
                                break;
                            default:
                                return false;
                        }
                    }
                }
                if (!drawPoints && lines.Count == 0) return false;
                int existingPoints = target.TryGetValue(actorData.Name,
                    out VtkJsTransientPrimitiveMessage existing) ? existing.Points.Length : 0;
                int existingLines = existing == null ? 0 : existing.Lines.Length;
                long totalPoints = _transientPrimitives.Values.Concat(_auxiliaryPrimitives.Values)
                    .Sum(item => (long)item.Points.Length) - existingPoints + points.Length;
                long totalLines = _transientPrimitives.Values.Concat(_auxiliaryPrimitives.Values)
                    .Sum(item => (long)item.Lines.Length) - existingLines + lines.Count;
                if (_transientPrimitives.Count + _auxiliaryPrimitives.Count +
                    (existing == null ? 1 : 0) > 4096 ||
                    totalPoints > 500000 || totalLines > 750000) return false;
                Color color = auxiliary
                    ? (actorData.Color.IsEmpty ? Color.Red : actorData.Color)
                    : GetHighlightColor(actorData.UseSecondaryHighlightColor);
                target[actorData.Name] = new VtkJsTransientPrimitiveMessage
                {
                    Name = actorData.Name,
                    Points = points,
                    Lines = lines.ToArray(),
                    DrawPoints = drawPoints,
                    Rgba = new[] { color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0 },
                    PointSize = Math.Max(1, actorData.NodeSize),
                    LineWidth = Math.Max(1, actorData.LineWidth),
                    Visible = true,
                    UseSecondaryHighlightColor = !auxiliary && actorData.UseSecondaryHighlightColor
                };
                ScheduleTransientPrimitivePublish();
                WriteTrace("captured " + (auxiliary ? "auxiliary" : "transient") + " primitive='" +
                           actorData.Name + "' points=" + points.Length +
                           " lines=" + lines.Count + " drawPoints=" + drawPoints);
                return true;
            }
            catch (Exception exception)
            {
                WriteTrace((auxiliary ? "auxiliary" : "transient") +
                           " primitive capture failed: " + exception.Message);
                return false;
            }
        }

        private void ScheduleTransientPrimitivePublish()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            _transientPrimitivePublishTimer.Stop();
            _transientPrimitivePublishTimer.Start();
        }

        private void PublishTransientPrimitiveState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsTransientPrimitiveStateMessage
                {
                    Type = "prepomax.transient-primitives.updated-v1",
                    Primitives = _transientPrimitives.Values.Concat(_auxiliaryPrimitives.Values)
                        .OrderBy(item => item.Name, StringComparer.Ordinal).ToArray()
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsTransientPrimitiveStateMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                if (Encoding.UTF8.GetByteCount(json) > 64 * 1024 * 1024)
                    throw new InvalidOperationException("Transient-primitive state exceeds its 64 MiB wire bound.");
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("transient primitive state actors=" + state.Primitives.Length +
                           " points=" + state.Primitives.Sum(item => item.Points.Length) +
                           " lines=" + state.Primitives.Sum(item => item.Lines.Length));
            }
            catch (Exception exception)
            {
                WriteTrace("transient primitive state publish failed: " + exception.Message);
            }
        }

        public void AddCoordinateAxis(vtkControl.vtkMaxActorData actorData, double symbolSize)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                actorData.Geometry.Nodes.Normals == null || actorData.Geometry.Nodes.Normals.Length == 0 ||
                !IsFinitePoint(actorData.Geometry.Nodes.Coor[0]) ||
                !IsFinitePoint(actorData.Geometry.Nodes.Normals[0]) ||
                actorData.Geometry.Nodes.Normals[0].All(value => Math.Abs(value) <= 1e-12) ||
                actorData.Geometry.Nodes.Coor[0].Any(value => Math.Abs(value) > 1e150) ||
                actorData.Geometry.Nodes.Normals[0].Any(value => Math.Abs(value) > 1e150) ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000)
                throw new ArgumentException("Invalid coordinate-axis actor data.");
            string name = actorData.Name + ActorNameSeparator + "axis";
            if (!_coordinateAxes.ContainsKey(name) && _coordinateAxes.Count >= 4096)
                throw new InvalidOperationException("Coordinate-axis state exceeds its safety bound.");
            _coordinateAxes[name] = new VtkJsCoordinateAxisMessage
            {
                Name = name,
                Point = (double[])actorData.Geometry.Nodes.Coor[0].Clone(),
                Direction = (double[])actorData.Geometry.Nodes.Normals[0].Clone(),
                Rgba = ToRgba(actorData.Color),
                SymbolSize = symbolSize
            };
            ScheduleCoordinateOverlayPublish();
        }

        public void AddCaption(string name, string text, Color color, double[] position, double[] offsetVector,
                               double fontScaleFactor)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 1024 || text == null || text.Length > 16384 ||
                !IsFinitePoint(position) || position.Any(value => Math.Abs(value) > 1e150) ||
                (offsetVector != null && (!IsFinitePoint(offsetVector) || offsetVector.Any(value => Math.Abs(value) > 1e150))) ||
                double.IsNaN(fontScaleFactor) || double.IsInfinity(fontScaleFactor) ||
                fontScaleFactor <= 0 || fontScaleFactor > 100)
                throw new ArgumentException("Invalid caption actor data.");
            if (!_captions.ContainsKey(name) && _captions.Count >= 4096)
                throw new InvalidOperationException("Caption state exceeds its safety bound.");
            _captions[name] = new VtkJsCaptionMessage
            {
                Name = name,
                Text = text,
                Position = (double[])position.Clone(),
                OffsetVector = offsetVector == null ? null : (double[])offsetVector.Clone(),
                Rgba = ToRgba(color),
                FontScaleFactor = fontScaleFactor
            };
            ScheduleCoordinateOverlayPublish();
        }

        public void ClearCoordinateOverlays()
        {
            if (_coordinateAxes.Count == 0 && _captions.Count == 0) return;
            _coordinateAxes.Clear();
            _captions.Clear();
            ScheduleCoordinateOverlayPublish();
            _directionalGlyphs.Clear();
            ScheduleDirectionalGlyphPublish();
        }

        private static double[] ToRgba(Color color)
        {
            return new double[] { color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0 };
        }

        private void ScheduleCoordinateOverlayPublish()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            _coordinateOverlayPublishTimer.Stop();
            _coordinateOverlayPublishTimer.Start();
        }

        private void PublishCoordinateOverlayState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsCoordinateOverlayStateMessage
                {
                    Type = "prepomax.coordinate-overlays.updated-v1",
                    Axes = _coordinateAxes.Values.OrderBy(axis => axis.Name, StringComparer.Ordinal).ToArray(),
                    Captions = _captions.Values.OrderBy(caption => caption.Name, StringComparer.Ordinal).ToArray()
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsCoordinateOverlayStateMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("coordinate overlay state axes=" + state.Axes.Length + " captions=" + state.Captions.Length);
            }
            catch (Exception exception)
            {
                WriteTrace("coordinate overlay state publish failed: " + exception.Message);
            }
        }

        public void AddDisplacementConstraintGlyphs(vtkControl.vtkMaxActorData actorData, double symbolSize)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                actorData.Geometry.Nodes.Normals == null ||
                actorData.Geometry.Nodes.Normals.Length != actorData.Geometry.Nodes.Coor.Length ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000)
                throw new ArgumentException("Invalid displacement-constraint glyph data.");
            double[][] points = new double[actorData.Geometry.Nodes.Coor.Length][];
            double[][] directions = new double[points.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                double[] point = actorData.Geometry.Nodes.Coor[i];
                double[] direction = actorData.Geometry.Nodes.Normals[i];
                if (!IsFinitePoint(point) || !IsFinitePoint(direction) ||
                    point.Any(value => Math.Abs(value) > 1e150) ||
                    direction.Any(value => Math.Abs(value) > 1e150) ||
                    direction.All(value => Math.Abs(value) <= 1e-12))
                    throw new ArgumentException("Invalid displacement-constraint glyph coordinates.");
                points[i] = (double[])point.Clone();
                directions[i] = (double[])direction.Clone();
            }
            string name = actorData.Name + ActorNameSeparator + "cones";
            int existingPoints = _directionalGlyphs.TryGetValue(name, out VtkJsDirectionalGlyphMessage existing)
                ? existing.Points.Length : 0;
            long totalPoints = _directionalGlyphs.Values.Sum(glyph => (long)glyph.Points.Length) -
                               existingPoints + points.Length;
            if (_directionalGlyphs.Count + (existing == null ? 1 : 0) > 4096 || totalPoints > 100000)
                throw new InvalidOperationException("Directional-glyph state exceeds its safety bound.");
            _directionalGlyphs[name] = new VtkJsDirectionalGlyphMessage
            {
                Name = name,
                Kind = "displacement-cone",
                Points = points,
                Directions = directions,
                Rgba = ToRgba(actorData.Color),
                SymbolSize = symbolSize
            };
            ScheduleDirectionalGlyphPublish();
        }

        public void AddGeneralConeGlyphs(vtkControl.vtkMaxActorData actorData, double symbolSize)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                actorData.Geometry.Nodes.Normals == null ||
                actorData.Geometry.Nodes.Normals.Length != actorData.Geometry.Nodes.Coor.Length ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000)
                throw new ArgumentException("Invalid general-cone glyph data.");
            double[][] points = new double[actorData.Geometry.Nodes.Coor.Length][];
            double[][] directions = new double[points.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                double[] point = actorData.Geometry.Nodes.Coor[i];
                double[] direction = actorData.Geometry.Nodes.Normals[i];
                if (!IsFinitePoint(point) || !IsFinitePoint(direction) ||
                    point.Any(value => Math.Abs(value) > 1e150) ||
                    direction.Any(value => Math.Abs(value) > 1e150) ||
                    direction.All(value => Math.Abs(value) <= 1e-12))
                    throw new ArgumentException("Invalid general-cone glyph coordinates.");
                points[i] = (double[])point.Clone();
                directions[i] = (double[])direction.Clone();
            }
            string name = actorData.Name + ActorNameSeparator + "cones";
            int existingPoints = _directionalGlyphs.TryGetValue(name, out VtkJsDirectionalGlyphMessage existing)
                ? existing.Points.Length : 0;
            long totalPoints = _directionalGlyphs.Values.Sum(glyph => (long)glyph.Points.Length) -
                               existingPoints + points.Length;
            if (_directionalGlyphs.Count + (existing == null ? 1 : 0) > 4096 || totalPoints > 100000)
                throw new InvalidOperationException("Directional-glyph state exceeds its safety bound.");
            _directionalGlyphs[name] = new VtkJsDirectionalGlyphMessage
            {
                Name = name,
                Kind = "general-cone",
                Points = points,
                Directions = directions,
                Rgba = ToRgba(actorData.Color),
                SymbolSize = symbolSize
            };
            ScheduleDirectionalGlyphPublish();
        }

        public void AddRotationalConstraintGlyphs(vtkControl.vtkMaxActorData actorData, double symbolSize)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                actorData.Geometry.Nodes.Normals == null ||
                actorData.Geometry.Nodes.Normals.Length != actorData.Geometry.Nodes.Coor.Length ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000)
                throw new ArgumentException("Invalid rotational-constraint glyph data.");
            double[][] points = new double[actorData.Geometry.Nodes.Coor.Length][];
            double[][] directions = new double[points.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                double[] point = actorData.Geometry.Nodes.Coor[i];
                double[] direction = actorData.Geometry.Nodes.Normals[i];
                if (!IsFinitePoint(point) || !IsFinitePoint(direction) ||
                    point.Any(value => Math.Abs(value) > 1e150) ||
                    direction.Any(value => Math.Abs(value) > 1e150) ||
                    direction.All(value => Math.Abs(value) <= 1e-12))
                    throw new ArgumentException("Invalid rotational-constraint glyph coordinates.");
                points[i] = (double[])point.Clone();
                directions[i] = (double[])direction.Clone();
            }
            string name = actorData.Name + ActorNameSeparator + "cylinder";
            int existingPoints = _directionalGlyphs.TryGetValue(name, out VtkJsDirectionalGlyphMessage existing)
                ? existing.Points.Length : 0;
            long totalPoints = _directionalGlyphs.Values.Sum(glyph => (long)glyph.Points.Length) -
                               existingPoints + points.Length;
            if (_directionalGlyphs.Count + (existing == null ? 1 : 0) > 4096 || totalPoints > 100000)
                throw new InvalidOperationException("Directional-glyph state exceeds its safety bound.");
            _directionalGlyphs[name] = new VtkJsDirectionalGlyphMessage
            {
                Name = name,
                Kind = "rotational-constraint",
                Points = points,
                Directions = directions,
                Rgba = ToRgba(actorData.Color),
                SymbolSize = symbolSize
            };
            ScheduleDirectionalGlyphPublish();
        }

        public void AddOrientedArrowGlyphs(vtkControl.vtkMaxActorData actorData, double symbolSize, bool invert,
                                           double relativeSize)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                actorData.Geometry.Nodes.Normals == null ||
                actorData.Geometry.Nodes.Normals.Length != actorData.Geometry.Nodes.Coor.Length ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000 ||
                double.IsNaN(relativeSize) || double.IsInfinity(relativeSize) ||
                relativeSize <= 0 || relativeSize > 1000)
                throw new ArgumentException("Invalid oriented-arrow glyph data.");
            double[][] points = new double[actorData.Geometry.Nodes.Coor.Length][];
            double[][] directions = new double[points.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                double[] point = actorData.Geometry.Nodes.Coor[i];
                double[] direction = actorData.Geometry.Nodes.Normals[i];
                if (!IsFinitePoint(point) || !IsFinitePoint(direction) ||
                    point.Any(value => Math.Abs(value) > 1e150) ||
                    direction.Any(value => Math.Abs(value) > 1e150) ||
                    direction.All(value => Math.Abs(value) <= 1e-12))
                    throw new ArgumentException("Invalid oriented-arrow glyph coordinates.");
                points[i] = (double[])point.Clone();
                directions[i] = (double[])direction.Clone();
            }
            string name = actorData.Name + ActorNameSeparator + "arrow";
            int existingPoints = _directionalGlyphs.TryGetValue(name, out VtkJsDirectionalGlyphMessage existing)
                ? existing.Points.Length : 0;
            long totalPoints = _directionalGlyphs.Values.Sum(glyph => (long)glyph.Points.Length) -
                               existingPoints + points.Length;
            if (_directionalGlyphs.Count + (existing == null ? 1 : 0) > 4096 || totalPoints > 100000)
                throw new InvalidOperationException("Directional-glyph state exceeds its safety bound.");
            _directionalGlyphs[name] = new VtkJsDirectionalGlyphMessage
            {
                Name = name,
                Kind = "oriented-arrow",
                Points = points,
                Directions = directions,
                Rgba = ToRgba(actorData.Color),
                SymbolSize = symbolSize,
                Invert = invert,
                RelativeSize = relativeSize
            };
            ScheduleDirectionalGlyphPublish();
        }

        public void AddDoubleArrowGlyphs(vtkControl.vtkMaxActorData actorData, double symbolSize)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                actorData.Geometry.Nodes.Normals == null ||
                actorData.Geometry.Nodes.Normals.Length != actorData.Geometry.Nodes.Coor.Length ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000)
                throw new ArgumentException("Invalid double-arrow glyph data.");
            double[][] points = new double[actorData.Geometry.Nodes.Coor.Length][];
            double[][] directions = new double[points.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                double[] point = actorData.Geometry.Nodes.Coor[i];
                double[] direction = actorData.Geometry.Nodes.Normals[i];
                if (!IsFinitePoint(point) || !IsFinitePoint(direction) ||
                    point.Any(value => Math.Abs(value) > 1e150) ||
                    direction.Any(value => Math.Abs(value) > 1e150) ||
                    direction.All(value => Math.Abs(value) <= 1e-12))
                    throw new ArgumentException("Invalid double-arrow glyph coordinates.");
                points[i] = (double[])point.Clone();
                directions[i] = (double[])direction.Clone();
            }
            string name = actorData.Name + ActorNameSeparator + "doubleArrow";
            int existingPoints = _directionalGlyphs.TryGetValue(name, out VtkJsDirectionalGlyphMessage existing)
                ? existing.Points.Length : 0;
            long totalPoints = _directionalGlyphs.Values.Sum(glyph => (long)glyph.Points.Length) -
                               existingPoints + points.Length;
            if (_directionalGlyphs.Count + (existing == null ? 1 : 0) > 4096 || totalPoints > 100000)
                throw new InvalidOperationException("Directional-glyph state exceeds its safety bound.");
            _directionalGlyphs[name] = new VtkJsDirectionalGlyphMessage
            {
                Name = name,
                Kind = "double-arrow",
                Points = points,
                Directions = directions,
                Rgba = ToRgba(actorData.Color),
                SymbolSize = symbolSize
            };
            ScheduleDirectionalGlyphPublish();
        }

        public void AddSpringGlyphs(vtkControl.vtkMaxActorData actorData, double symbolSize, bool invert)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                actorData.Geometry.Nodes.Normals == null ||
                actorData.Geometry.Nodes.Normals.Length != actorData.Geometry.Nodes.Coor.Length ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000)
                throw new ArgumentException("Invalid oriented-spring glyph data.");
            double[][] points = new double[actorData.Geometry.Nodes.Coor.Length][];
            double[][] directions = new double[points.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                double[] point = actorData.Geometry.Nodes.Coor[i];
                double[] direction = actorData.Geometry.Nodes.Normals[i];
                if (!IsFinitePoint(point) || !IsFinitePoint(direction) ||
                    point.Any(value => Math.Abs(value) > 1e150) ||
                    direction.Any(value => Math.Abs(value) > 1e150) ||
                    direction.All(value => Math.Abs(value) <= 1e-12))
                    throw new ArgumentException("Invalid oriented-spring glyph coordinates.");
                points[i] = (double[])point.Clone();
                directions[i] = (double[])direction.Clone();
            }
            string name = actorData.Name + ActorNameSeparator + "spring";
            int existingPoints = _directionalGlyphs.TryGetValue(name, out VtkJsDirectionalGlyphMessage existing)
                ? existing.Points.Length : 0;
            long totalPoints = _directionalGlyphs.Values.Sum(glyph => (long)glyph.Points.Length) -
                               existingPoints + points.Length;
            if (_directionalGlyphs.Count + (existing == null ? 1 : 0) > 4096 || totalPoints > 100000)
                throw new InvalidOperationException("Directional-glyph state exceeds its safety bound.");
            _directionalGlyphs[name] = new VtkJsDirectionalGlyphMessage
            {
                Name = name,
                Kind = "oriented-spring",
                Points = points,
                Directions = directions,
                Rgba = ToRgba(actorData.Color),
                SymbolSize = symbolSize,
                Invert = invert
            };
            ScheduleDirectionalGlyphPublish();
        }

        public void AddThermalGlyphs(vtkControl.vtkMaxActorData actorData, double symbolSize, bool invert)
        {
            AddSimpleDirectionalGlyph(actorData, symbolSize, "oriented-thermal", "thermo", invert, null);
        }

        public void AddFluxGlyphs(vtkControl.vtkMaxActorData actorData, double symbolSize, bool center, bool invert)
        {
            AddSimpleDirectionalGlyph(actorData, symbolSize, "oriented-flux", "flux", invert, center);
        }

        private void AddSimpleDirectionalGlyph(vtkControl.vtkMaxActorData actorData, double symbolSize, string kind,
                                               string suffix, bool? invert, bool? center)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Name.Length > 1024 ||
                actorData.Geometry == null || actorData.Geometry.Nodes == null ||
                actorData.Geometry.Nodes.Coor == null || actorData.Geometry.Nodes.Coor.Length == 0 ||
                actorData.Geometry.Nodes.Normals == null ||
                actorData.Geometry.Nodes.Normals.Length != actorData.Geometry.Nodes.Coor.Length ||
                double.IsNaN(symbolSize) || double.IsInfinity(symbolSize) || symbolSize <= 0 || symbolSize > 1000)
                throw new ArgumentException("Invalid " + kind + " glyph data.");
            double[][] points = new double[actorData.Geometry.Nodes.Coor.Length][];
            double[][] directions = new double[points.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                double[] point = actorData.Geometry.Nodes.Coor[i];
                double[] direction = actorData.Geometry.Nodes.Normals[i];
                if (!IsFinitePoint(point) || !IsFinitePoint(direction) ||
                    point.Any(value => Math.Abs(value) > 1e150) ||
                    direction.Any(value => Math.Abs(value) > 1e150) ||
                    direction.All(value => Math.Abs(value) <= 1e-12))
                    throw new ArgumentException("Invalid " + kind + " glyph coordinates.");
                points[i] = (double[])point.Clone();
                directions[i] = (double[])direction.Clone();
            }
            string name = actorData.Name + ActorNameSeparator + suffix;
            int existingPoints = _directionalGlyphs.TryGetValue(name, out VtkJsDirectionalGlyphMessage existing)
                ? existing.Points.Length : 0;
            long totalPoints = _directionalGlyphs.Values.Sum(glyph => (long)glyph.Points.Length) -
                               existingPoints + points.Length;
            if (_directionalGlyphs.Count + (existing == null ? 1 : 0) > 4096 || totalPoints > 100000)
                throw new InvalidOperationException("Directional-glyph state exceeds its safety bound.");
            _directionalGlyphs[name] = new VtkJsDirectionalGlyphMessage
            {
                Name = name,
                Kind = kind,
                Points = points,
                Directions = directions,
                Rgba = ToRgba(actorData.Color),
                SymbolSize = symbolSize,
                Invert = invert,
                Center = center
            };
            ScheduleDirectionalGlyphPublish();
        }

        public void ClearDirectionalGlyphs()
        {
            if (_directionalGlyphs.Count == 0) return;
            _directionalGlyphs.Clear();
            ScheduleDirectionalGlyphPublish();
        }

        private void ScheduleDirectionalGlyphPublish()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            _directionalGlyphPublishTimer.Stop();
            _directionalGlyphPublishTimer.Start();
        }

        private void PublishDirectionalGlyphState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                var state = new VtkJsDirectionalGlyphStateMessage
                {
                    Type = "prepomax.directional-glyphs.updated-v1",
                    Glyphs = _directionalGlyphs.Values.OrderBy(glyph => glyph.Name, StringComparer.Ordinal).ToArray()
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsDirectionalGlyphStateMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("directional glyph state actors=" + state.Glyphs.Length +
                           " points=" + state.Glyphs.Sum(glyph => glyph.Points.Length));
            }
            catch (Exception exception)
            {
                WriteTrace("directional glyph state publish failed: " + exception.Message);
            }
        }

        private void RefreshExtremaAnnotations()
        {
            _annotations.Remove("Min_Annotation");
            _annotations.Remove("Max_Annotation");
            VtkJsActorExtremaState[] candidates = _actorExtrema.Values
                .Where(state => state.Visible && state.ColorContours &&
                                !float.IsNaN(state.Minimum.Value) && !float.IsInfinity(state.Minimum.Value) &&
                                !float.IsNaN(state.Maximum.Value) && !float.IsInfinity(state.Maximum.Value))
                .ToArray();
            if (candidates.Length > 0)
            {
                VtkJsExtremeNode minimum = candidates.OrderBy(state => state.Minimum.Value).First().Minimum;
                VtkJsExtremeNode maximum = candidates.OrderByDescending(state => state.Maximum.Value).First().Maximum;
                AddExtremaAnnotation("Min_Annotation", "Min", minimum);
                AddExtremaAnnotation("Max_Annotation", "Max", maximum);
                WriteTrace("extrema state min=" + minimum.Value + " node=" + minimum.Id +
                           " max=" + maximum.Value + " node=" + maximum.Id);
            }
            ScheduleAnnotationPublish();
        }

        private void AddExtremaAnnotation(string name, string label, VtkJsExtremeNode node)
        {
            if (!_extremaAnnotationSettings.TryGetValue(name, out VtkJsExtremaAnnotationSettings settings) ||
                !settings.Visible) return;
            string formattedValue;
            try { formattedValue = node.Value.ToString(settings.NumberFormat); }
            catch (FormatException)
            {
                WriteTrace("ignored invalid extrema annotation number format");
                return;
            }
            string unit = string.IsNullOrWhiteSpace(_fieldUnits) || _fieldUnits == "/" ? string.Empty : " " + _fieldUnits;
            _annotations[name] = new VtkJsAnnotationMessage
            {
                Name = name,
                Text = label + ": " + formattedValue + unit + Environment.NewLine + "Node id: " + node.Id,
                AnchorPoint = (double[])node.Coordinates.Clone(),
                Offset = GetAnnotationOffset(name),
                DrawBackground = settings.DrawBackground,
                DrawBorder = settings.DrawBorder,
                Visible = true
            };
        }

        private double[] GetAnnotationOffset(string name)
        {
            return _annotationOffsets.TryGetValue(name, out double[] offset)
                ? (double[])offset.Clone() : new double[2];
        }

        private static bool TryReadActorExtrema(NodesExchangeData source, out VtkJsExtremeNode minimum,
                                                out VtkJsExtremeNode maximum)
        {
            minimum = null;
            maximum = null;
            if (source == null || source.Ids == null || source.Ids.Length < 2 ||
                source.Coor == null || source.Coor.Length < 2 ||
                source.Values == null || source.Values.Length < 2) return false;
            if (!IsFinitePoint(source.Coor[0]) || !IsFinitePoint(source.Coor[1])) return false;
            minimum = new VtkJsExtremeNode
            {
                Id = source.Ids[0], Value = source.Values[0], Coordinates = (double[])source.Coor[0].Clone()
            };
            maximum = new VtkJsExtremeNode
            {
                Id = source.Ids[1], Value = source.Values[1], Coordinates = (double[])source.Coor[1].Clone()
            };
            return true;
        }

        private static bool IsFinitePoint(double[] point)
        {
            return point != null && point.Length == 3 &&
                   Array.TrueForAll(point, value => !double.IsNaN(value) && !double.IsInfinity(value));
        }

        public bool CaptureActor(vtkControl.vtkMaxActorData actorData)
        {
            try
            {
                if (actorData != null && actorData.Name != null &&
                    _auxiliaryPrimitives.ContainsKey(actorData.Name))
                {
                    WriteTrace("rejected retained actor='" + actorData.Name +
                               "': name belongs to auxiliary primitive");
                    return false;
                }
                string diagnostic;
                bool captured = _sceneCapture.Capture(actorData, out diagnostic);
                WriteTrace(diagnostic);
                if (!captured) return false;
                _transientSelectionActorNames.Remove(actorData.Name);
                _hiddenActorNames.Remove(actorData.Name);
                _actorColorContours[actorData.Name] = actorData.ColorContours;
                VtkJsActorExtremaState previousExtrema;
                bool hadExtrema = _actorExtrema.TryGetValue(actorData.Name, out previousExtrema);
                if (TryReadActorExtrema(actorData.Geometry == null ? null : actorData.Geometry.ExtremeNodes,
                                        out VtkJsExtremeNode minimum, out VtkJsExtremeNode maximum))
                {
                    _actorExtrema[actorData.Name] = new VtkJsActorExtremaState
                    {
                        Minimum = minimum,
                        Maximum = maximum,
                        Visible = !hadExtrema || previousExtrema.Visible,
                        ColorContours = actorData.ColorContours
                    };
                }
                else _actorExtrema.Remove(actorData.Name);
                RefreshExtremaAnnotations();
                PublishActorContourState();
                _topologyDirty = true;
                _frameDirty = false;
                HasPublishedTopology = false;
                _captureGeneration++;
                SchedulePublish();
                return true;
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax surface could not be converted for VTK.js: " + exception.Message);
                return false;
            }
        }

        public bool CaptureAuxiliaryActor(vtkControl.vtkMaxActorData actorData)
        {
            try
            {
                if (actorData == null || actorData.Layer == vtkControl.vtkRendererLayer.Selection ||
                    string.IsNullOrWhiteSpace(actorData.Name) || _auxiliaryPrimitives.ContainsKey(actorData.Name) ||
                    _transientPrimitives.ContainsKey(actorData.Name) ||
                    _transientSelectionActorNames.Contains(actorData.Name))
                    return false;
                string diagnostic;
                bool captured = _sceneCapture.CaptureAuxiliary(actorData, out diagnostic);
                WriteTrace(diagnostic);
                if (!captured) return false;
                _hiddenActorNames.Remove(actorData.Name);
                _actorColorContours[actorData.Name] = actorData.ColorContours;
                PublishActorContourState();
                _topologyDirty = true;
                _frameDirty = false;
                HasPublishedTopology = false;
                _captureGeneration++;
                SchedulePublish();
                return true;
            }
            catch (Exception exception)
            {
                WriteTrace("auxiliary surface capture failed: " + exception.Message);
                return false;
            }
        }

        public bool CaptureSelectionActor(vtkControl.vtkMaxActorData actorData)
        {
            try
            {
                if (actorData == null || actorData.Layer != vtkControl.vtkRendererLayer.Selection)
                {
                    WriteTrace("rejected transient selection actor: invalid layer");
                    return false;
                }
                if (_sceneCapture.ContainsActor(actorData.Name) &&
                    !_transientSelectionActorNames.Contains(actorData.Name))
                {
                    WriteTrace("rejected transient selection actor='" + actorData.Name +
                               "': name belongs to retained scene actor");
                    return false;
                }
                if (_transientPrimitives.ContainsKey(actorData.Name))
                {
                    WriteTrace("rejected transient selection actor='" + actorData.Name +
                               "': name belongs to transient point/line actor");
                    return false;
                }
                if (_auxiliaryPrimitives.ContainsKey(actorData.Name))
                {
                    WriteTrace("rejected transient selection actor='" + actorData.Name +
                               "': name belongs to auxiliary primitive");
                    return false;
                }
                string diagnostic;
                Color originalColor = actorData.Color;
                actorData.Color = GetSurfaceHighlightColor(actorData.UseSecondaryHighlightColor);
                bool captured;
                try { captured = _sceneCapture.CaptureSelection(actorData, out diagnostic); }
                finally { actorData.Color = originalColor; }
                WriteTrace(diagnostic);
                if (!captured) return false;
                _transientSelectionActorNames.Add(actorData.Name);
                _transientSelectionSecondary[actorData.Name] = actorData.UseSecondaryHighlightColor;
                _hiddenActorNames.Remove(actorData.Name);
                _actorColorContours[actorData.Name] = false;
                _topologyDirty = true;
                _frameDirty = false;
                HasPublishedTopology = false;
                _captureGeneration++;
                SchedulePublish();
                return true;
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax selection surface could not be converted for VTK.js: " + exception.Message);
                return false;
            }
        }

        public void ClearTransientSelectionActors()
        {
            string[] names = _transientSelectionActorNames.ToArray();
            _transientSelectionActorNames.Clear();
            _transientSelectionSecondary.Clear();
            if (names.Length > 0) RemoveActors(names);
            int primitiveCount = _transientPrimitives.Count;
            if (primitiveCount > 0)
            {
                _transientPrimitives.Clear();
                ScheduleTransientPrimitivePublish();
            }
            if (names.Length > 0 || primitiveCount > 0)
                WriteTrace("cleared transient selection actors=" + names.Length +
                           " primitives=" + primitiveCount);
        }

        public bool RegisterAnimation(vtkControl.vtkMaxActorData actorData)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Geometry == null ||
                actorData.Geometry.NodesAnimation == null || actorData.Geometry.NodesAnimation.Length == 0) return false;
            if (!CaptureAnimationFrame(actorData, 0)) return false;
            _animationSources[actorData.Name] = actorData;
            _topologyDirty = true;
            _frameDirty = false;
            HasPublishedTopology = false;
            _captureGeneration++;
            SchedulePublish();
            WriteTrace("registered animation actor='" + actorData.Name + "' frames=" +
                       actorData.Geometry.NodesAnimation.Length);
            return true;
        }

        public int AnimationSourceCount => _animationSources.Count;
        public int AnimationFrameCount => _animationTime.Length;

        public float GetAnimationScaleFactor(int frameNumber)
        {
            return frameNumber >= 0 && frameNumber < _animationScale.Length
                ? _animationScale[frameNumber]
                : float.NaN;
        }

        public void SetAnimationFrame(int frameNumber, bool scalarRangeFromAllFrames)
        {
            _useAllFramesScalarRange = scalarRangeFromAllFrames;
            UpdateStatusBlockAnimationFrame(frameNumber);
            if (_animationSources.Count == 0) return;
            var captureTimer = Stopwatch.StartNew();
            if (_animationTime.Length > 0 && (frameNumber < 0 || frameNumber >= _animationTime.Length))
            {
                WriteTrace("rejected animation frame=" + frameNumber + " type=" + _animationType +
                           ": metadata range=0.." + (_animationTime.Length - 1));
                return;
            }
            string[] diagnostics;
            if (!_sceneCapture.CaptureAnimationFrame(_animationSources.Values, frameNumber, out diagnostics))
            {
                foreach (string diagnostic in diagnostics) WriteTrace(diagnostic);
                return;
            }
            foreach (string diagnostic in diagnostics) WriteTrace(diagnostic);
            foreach (vtkControl.vtkMaxActorData actorData in _animationSources.Values)
                CaptureAnimationExtrema(actorData, frameNumber);
            captureTimer.Stop();
            if (!HasPublishedTopology || _topologyDirty)
            {
                _topologyDirty = true;
                HasPublishedTopology = false;
            }
            else _frameDirty = true;
            _pendingScalarActors.Clear();
            _captureGeneration++;
            string metadata = _animationTime.Length == 0 ? string.Empty :
                " type=" + _animationType + " time=" + _animationTime[frameNumber] +
                " step=" + _animationStepId[frameNumber] +
                " increment=" + _animationStepIncrementId[frameNumber] +
                " scale=" + _animationScale[frameNumber];
            WriteTrace("animation frame=" + frameNumber + metadata + " generation=" + _captureGeneration);
            WriteTrace("frame capture frame=" + frameNumber + " actors=" + _animationSources.Count +
                       " captureMs=" + captureTimer.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ScheduleAnimationPublish();
        }

        public async Task<long> ActivateAnimationFrameAsync(int frameNumber, bool scalarRangeFromAllFrames)
        {
            if (_pendingAnimationFrameActivation != null)
                throw new InvalidOperationException("Another animation frame is still awaiting browser paint.");
            var completion = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingAnimationFrameActivation = completion;
            _pendingAnimationFrameMinimumRevision = Math.Max(1, BrowserAppliedSceneRevision + 1);
            _pendingAnimationFramePriorSceneId = BrowserAppliedSceneId;
            try
            {
                SetAnimationFrame(frameNumber, scalarRangeFromAllFrames);
                Task winner = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30)));
                if (winner != completion.Task)
                    throw new TimeoutException("The browser did not paint the requested animation frame within 30 seconds.");
                return await completion.Task;
            }
            finally
            {
                if (ReferenceEquals(_pendingAnimationFrameActivation, completion))
                    _pendingAnimationFrameActivation = null;
            }
        }

        public async Task<VtkJsPngCaptureResult> CapturePngAsync(string fileName)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null)
                throw new InvalidOperationException("The browser renderer is not ready for image capture.");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("A PNG file name is required.");
            string fullPath = Path.GetFullPath(fileName);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("The PNG output directory does not exist.");
            byte[] png;
            using (var stream = new MemoryStream())
            {
                await _webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
                if (stream.Length < 33 || stream.Length > 64L * 1024 * 1024)
                    throw new InvalidDataException("The browser PNG capture has an invalid size.");
                png = stream.ToArray();
            }
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (!signature.SequenceEqual(png.Take(signature.Length)) ||
                png[12] != (byte)'I' || png[13] != (byte)'H' || png[14] != (byte)'D' || png[15] != (byte)'R')
                throw new InvalidDataException("The browser capture is not a valid PNG image.");
            uint width = ReadBigEndianUInt32(png, 16);
            uint height = ReadBigEndianUInt32(png, 20);
            if (width == 0 || height == 0 || width > 32768 || height > 32768)
                throw new InvalidDataException("The browser PNG dimensions are outside the safety bound.");
            string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." +
                                                  Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                                                   FileShare.None, 81920, true))
                    await output.WriteAsync(png, 0, png.Length);
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, null, true);
                else File.Move(temporaryPath, fullPath);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch { }
            }
            WriteTrace("browser PNG captured bytes=" + png.Length + " width=" + width + " height=" + height);
            return new VtkJsPngCaptureResult((int)width, (int)height, png.Length);
        }

        public async Task<VtkJsJpegCaptureResult> CaptureJpegAsync()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null)
                throw new InvalidOperationException("The browser renderer is not ready for video capture.");
            byte[] jpeg;
            using (var stream = new MemoryStream())
            {
                await _webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, stream);
                if (stream.Length < 32 || stream.Length > 64L * 1024 * 1024)
                    throw new InvalidDataException("The browser JPEG capture has an invalid size.");
                jpeg = stream.ToArray();
            }
            if (jpeg[0] != 0xFF || jpeg[1] != 0xD8 ||
                jpeg[jpeg.Length - 2] != 0xFF || jpeg[jpeg.Length - 1] != 0xD9 ||
                !TryReadJpegDimensions(jpeg, out int width, out int height))
                throw new InvalidDataException("The browser capture is not a valid JPEG image.");
            if (width < 1 || height < 1 || width > short.MaxValue || height > short.MaxValue)
                throw new InvalidDataException("The browser JPEG dimensions are outside the AVI safety bound.");
            WriteTrace("browser JPEG captured bytes=" + jpeg.Length + " width=" + width + " height=" + height);
            return new VtkJsJpegCaptureResult(width, height, jpeg);
        }

        private static uint ReadBigEndianUInt32(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) |
                   ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private static bool TryReadJpegDimensions(byte[] jpeg, out int width, out int height)
        {
            width = 0;
            height = 0;
            int offset = 2;
            while (offset + 3 < jpeg.Length)
            {
                if (jpeg[offset++] != 0xFF) continue;
                while (offset < jpeg.Length && jpeg[offset] == 0xFF) offset++;
                if (offset >= jpeg.Length) return false;
                byte marker = jpeg[offset++];
                if (marker == 0xD8 || marker == 0xD9 || marker == 0x01 ||
                    (marker >= 0xD0 && marker <= 0xD7))
                    continue;
                if (offset + 1 >= jpeg.Length) return false;
                int segmentLength = (jpeg[offset] << 8) | jpeg[offset + 1];
                if (segmentLength < 2 || offset + segmentLength > jpeg.Length) return false;
                bool startOfFrame = marker >= 0xC0 && marker <= 0xCF &&
                                    marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (startOfFrame)
                {
                    if (segmentLength < 7) return false;
                    height = (jpeg[offset + 3] << 8) | jpeg[offset + 4];
                    width = (jpeg[offset + 5] << 8) | jpeg[offset + 6];
                    return width > 0 && height > 0;
                }
                if (marker == 0xDA) return false;
                offset += segmentLength;
            }
            return false;
        }

        public void SetAnimationFrameData(float[] time, int[] stepId, int[] stepIncrementId, float[] scale,
                                           double[] allFramesScalarRange,
                                           vtkControl.vtkMaxAnimationType animationType)
        {
            if (time == null || stepId == null || stepIncrementId == null || scale == null)
                throw new ArgumentNullException("Animation frame metadata arrays must not be null.");
            int count = time.Length;
            if (count == 0 || stepId.Length != count || stepIncrementId.Length != count || scale.Length != count)
                throw new InvalidOperationException("Animation frame metadata arrays must have the same positive length.");
            foreach (vtkControl.vtkMaxActorData actorData in _animationSources.Values)
            {
                int actorFrames = actorData.Geometry == null || actorData.Geometry.NodesAnimation == null
                    ? 0
                    : actorData.Geometry.NodesAnimation.Length;
                if (actorFrames != count)
                    throw new InvalidOperationException("Animation actor '" + actorData.Name + "' has " + actorFrames +
                                                        " frames but the animation metadata has " + count + ".");
                int extremaFrames = actorData.Geometry.ExtremeNodesAnimation == null
                    ? count
                    : actorData.Geometry.ExtremeNodesAnimation.Length;
                int locatorFrames = actorData.CellLocator == null || actorData.CellLocator.NodesAnimation == null
                    ? count
                    : actorData.CellLocator.NodesAnimation.Length;
                if (extremaFrames != count || locatorFrames != count)
                    throw new InvalidOperationException("Animation actor '" + actorData.Name +
                                                        "' has inconsistent geometry, extrema, or locator frame counts.");
            }
            _animationType = animationType;
            _animationTime = (float[])time.Clone();
            _animationStepId = (int[])stepId.Clone();
            _animationStepIncrementId = (int[])stepIncrementId.Clone();
            _animationScale = (float[])scale.Clone();
            _animationScalarRange = allFramesScalarRange == null ? new double[0] : (double[])allFramesScalarRange.Clone();
            WriteTrace("animation metadata type=" + animationType + " frames=" + count);
        }

        private bool CaptureAnimationFrame(vtkControl.vtkMaxActorData actorData, int frameNumber)
        {
            string diagnostic;
            bool captured = _sceneCapture.CaptureAnimationFrame(actorData, frameNumber, out diagnostic);
            WriteTrace(diagnostic);
            if (!captured) return false;
            CaptureAnimationExtrema(actorData, frameNumber);
            return true;
        }

        private void CaptureAnimationExtrema(vtkControl.vtkMaxActorData actorData, int frameNumber)
        {
            NodesExchangeData extrema = actorData.Geometry.ExtremeNodesAnimation != null &&
                                        frameNumber < actorData.Geometry.ExtremeNodesAnimation.Length
                ? actorData.Geometry.ExtremeNodesAnimation[frameNumber]
                : null;
            if (TryReadActorExtrema(extrema, out VtkJsExtremeNode minimum, out VtkJsExtremeNode maximum))
            {
                VtkJsActorExtremaState previous;
                bool hadPrevious = _actorExtrema.TryGetValue(actorData.Name, out previous);
                _actorExtrema[actorData.Name] = new VtkJsActorExtremaState
                {
                    Minimum = minimum,
                    Maximum = maximum,
                    Visible = !hadPrevious || previous.Visible,
                    ColorContours = actorData.ColorContours
                };
            }
            RefreshExtremaAnnotations();
        }

        public void AddSymmetry(int symmetryPlane, double[] symmetryPoint)
        {
            ValidateVector(symmetryPoint, "symmetry point", false);
            if (symmetryPlane < 0 || symmetryPlane > 2)
                throw new NotSupportedException("Only the X, Y, and Z symmetry planes are supported.");
            double[] matrix = IdentityMatrix();
            matrix[symmetryPlane * 4 + symmetryPlane] = -1;
            matrix[symmetryPlane * 4 + 3] = 2 * symmetryPoint[symmetryPlane];
            _transformationSteps.Add(new VtkJsTransformationStep(matrix, 2));
        }

        public void AddLinearPattern(double[] displacement, int numOfItems)
        {
            ValidateVector(displacement, "linear-pattern displacement", false);
            ValidateItemCount(numOfItems);
            double[] matrix = IdentityMatrix();
            matrix[3] = displacement[0];
            matrix[7] = displacement[1];
            matrix[11] = displacement[2];
            _transformationSteps.Add(new VtkJsTransformationStep(matrix, numOfItems));
        }

        public void AddCircularPattern(double[] axisPoint, double[] axisNormal, double angle, int numOfItems)
        {
            ValidateVector(axisPoint, "circular-pattern axis point", false);
            ValidateVector(axisNormal, "circular-pattern axis normal", true);
            ValidateFinite(angle, "circular-pattern angle");
            ValidateItemCount(numOfItems);
            double axisScale = Math.Max(Math.Abs(axisNormal[0]),
                Math.Max(Math.Abs(axisNormal[1]), Math.Abs(axisNormal[2])));
            double scaledX = axisNormal[0] / axisScale;
            double scaledY = axisNormal[1] / axisScale;
            double scaledZ = axisNormal[2] / axisScale;
            double length = axisScale * Math.Sqrt(scaledX * scaledX + scaledY * scaledY + scaledZ * scaledZ);
            double x = axisNormal[0] / length;
            double y = axisNormal[1] / length;
            double z = axisNormal[2] / length;
            double radians = angle * Math.PI / 180.0;
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            double oneMinusC = 1 - c;
            double[] matrix = new double[]
            {
                c + x * x * oneMinusC, x * y * oneMinusC - z * s, x * z * oneMinusC + y * s, 0,
                y * x * oneMinusC + z * s, c + y * y * oneMinusC, y * z * oneMinusC - x * s, 0,
                z * x * oneMinusC - y * s, z * y * oneMinusC + x * s, c + z * z * oneMinusC, 0,
                0, 0, 0, 1
            };
            matrix[3] = axisPoint[0] - (matrix[0] * axisPoint[0] + matrix[1] * axisPoint[1] + matrix[2] * axisPoint[2]);
            matrix[7] = axisPoint[1] - (matrix[4] * axisPoint[0] + matrix[5] * axisPoint[1] + matrix[6] * axisPoint[2]);
            matrix[11] = axisPoint[2] - (matrix[8] * axisPoint[0] + matrix[9] * axisPoint[1] + matrix[10] * axisPoint[2]);
            _transformationSteps.Add(new VtkJsTransformationStep(matrix, numOfItems));
        }

        public void ApplyTransformations()
        {
            var matrices = new List<double[]> { IdentityMatrix() };
            foreach (VtkJsTransformationStep step in _transformationSteps)
            {
                double[][] inputs = matrices.ToArray();
                foreach (double[] input in inputs)
                {
                    double[] current = input;
                    for (int item = 1; item < step.ItemCount; item++)
                    {
                        current = MultiplyMatrices(step.Matrix, current);
                        if (matrices.Count > 256)
                            throw new InvalidOperationException("Transformation copies exceed the 256-matrix safety bound.");
                        foreach (double value in current) ValidateFinite(value, "composed transformation matrix");
                        matrices.Add(current);
                    }
                }
            }
            _transformationState = new VtkJsTransformationStateMessage
            {
                Type = "prepomax.transformations.updated-v1",
                Visible = true,
                Matrices = matrices.Skip(1).ToArray()
            };
            PublishTransformationState();
        }

        public void SetTransformedActorsVisible(bool visible)
        {
            _transformationState.Visible = visible;
            PublishTransformationState();
        }

        private void PublishTransformationState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsTransformationStateMessage));
                    serializer.WriteObject(stream, _transformationState);
                    _webView.CoreWebView2.PostWebMessageAsJson(Encoding.UTF8.GetString(stream.ToArray()));
                }
                WriteTrace("transformation state matrices=" + _transformationState.Matrices.Length +
                           " visible=" + _transformationState.Visible);
            }
            catch (Exception exception)
            {
                WriteTrace("transformation state publish failed: " + exception.Message);
            }
        }

        private static void ValidateVector(double[] vector, string name, bool requireNonZero)
        {
            if (vector == null || vector.Length != 3) throw new ArgumentException("A three-component " + name + " is required.");
            foreach (double value in vector) ValidateFinite(value, name);
            if (requireNonZero && vector[0] == 0 && vector[1] == 0 && vector[2] == 0)
                throw new ArgumentException("The " + name + " must be non-zero.");
        }

        private static void ValidateFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1E150)
                throw new ArgumentOutOfRangeException(name, "Transformation values must be finite and bounded.");
        }

        private static void ValidateItemCount(int count)
        {
            if (count < 2 || count > 257) throw new ArgumentOutOfRangeException("numOfItems");
        }

        private static double[] IdentityMatrix()
        {
            return new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        }

        private static double[] MultiplyMatrices(double[] left, double[] right)
        {
            var result = new double[16];
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    for (int inner = 0; inner < 4; inner++)
                        result[row * 4 + column] += left[row * 4 + inner] * right[inner * 4 + column];
            return result;
        }

        public void ClearCapturedScene()
        {
            CancelPendingAnimationFrameActivation();
            string[] explodedActorNames = _explodedPreviewOffsets.Keys.ToArray();
            var clearedOffsets = explodedActorNames.ToDictionary(name => name,
                name => new double[] { 0, 0, 0 }, StringComparer.Ordinal);
            _sceneCapture.Clear();
            _animationSources.Clear();
            _animationTime = new float[0];
            _animationStepId = new int[0];
            _animationStepIncrementId = new int[0];
            _animationScale = new float[0];
            _animationScalarRange = new double[0];
            _useAllFramesScalarRange = false;
            _actorExtrema.Clear();
            _actorColorContours.Clear();
            _hiddenActorNames.Clear();
            PublishActorContourState();
            _highlightedActorNames.Clear();
            _transientSelectionActorNames.Clear();
            _transientSelectionSecondary.Clear();
            PublishActorHighlightState();
            ClearSelectionState();
            _sphereSymbols.Clear();
            ScheduleSphereSymbolPublish();
            _transientPrimitives.Clear();
            _auxiliaryPrimitives.Clear();
            ScheduleTransientPrimitivePublish();
            _coordinateAxes.Clear();
            _captions.Clear();
            ScheduleCoordinateOverlayPublish();
            _directionalGlyphs.Clear();
            ScheduleDirectionalGlyphPublish();
            ClearAnnotations();
            _followerViewState = VtkJsFollowerViewStateMessage.Empty;
            PublishFollowerViewState();
            if (explodedActorNames.Length > 0)
                PublishExplodedViewState(false, 0, explodedActorNames, clearedOffsets);
            _explodedPreviewOffsets.Clear();
            _sectionViewState = VtkJsSectionViewStateMessage.Disabled;
            PublishSectionViewState();
            _transformationSteps.Clear();
            _transformationState = VtkJsTransformationStateMessage.Empty;
            PublishTransformationState();
            _boxZoomEnabled = false;
            PublishBoxZoomState();
            _topologyDirty = true;
            _frameDirty = false;
            HasPublishedTopology = false;
            _pendingScalarActors.Clear();
            _scalarLegendVisible = false;
            PublishScalarLegendState();
            HideColorBar();
            HideStatusBlock();
            SetFieldMetadata("PrePoMax values", "value", "model-unit");
            _captureGeneration++;
            WriteTrace("cleared captured scene; generation=" + _captureGeneration);
            SchedulePublish();
        }

        public async void SetActorVisibility(string[] actorNames, bool visible)
        {
            if (actorNames == null || actorNames.Length == 0) return;
            var retainedActorNames = new List<string>();
            bool auxiliaryChanged = false;
            foreach (string actorName in actorNames)
            {
                if (actorName == null) continue;
                if (visible) _hiddenActorNames.Remove(actorName);
                else _hiddenActorNames.Add(actorName);
                if (_actorExtrema.TryGetValue(actorName, out VtkJsActorExtremaState state)) state.Visible = visible;
                if (_auxiliaryPrimitives.TryGetValue(actorName, out VtkJsTransientPrimitiveMessage primitive))
                {
                    primitive.Visible = visible;
                    auxiliaryChanged = true;
                }
                else retainedActorNames.Add(actorName);
            }
            if (auxiliaryChanged) ScheduleTransientPrimitivePublish();
            RefreshExtremaAnnotations();
            PublishScalarLegendState();
            if (!_started || IsDisposed || retainedActorNames.Count == 0) return;
            try
            {
                await _host.SetActorVisibilityAsync(retainedActorNames.ToArray(), visible);
                WriteTrace("actor visibility visible=" + visible + " count=" + retainedActorNames.Count);
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax actor visibility update could not be published to VTK.js: " + exception.Message);
            }
        }

        public async void SetCameraView(string view)
        {
            if (!_started || IsDisposed) return;
            try
            {
                await _host.SetCameraViewAsync(view);
                WriteTrace("camera view=" + view);
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax camera update could not be published to VTK.js: " + exception.Message);
            }
        }

        public void StartBoxZoom()
        {
            _boxZoomEnabled = true;
            PublishBoxZoomState();
            WriteTrace("box zoom enabled");
        }

        private void PublishBoxZoomState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            string json = "{\"type\":\"prepomax.box-zoom.mode-v1\",\"enabled\":" +
                          (_boxZoomEnabled ? "true" : "false") + "}";
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        public void SetFollowerView(string[] actorNames, int[] nodeIds)
        {
            if (actorNames == null || nodeIds == null || actorNames.Length != nodeIds.Length ||
                (actorNames.Length != 1 && actorNames.Length != 3) ||
                actorNames.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 1024))
            {
                WriteTrace("ignored invalid follower view state");
                return;
            }
            _followerViewState = new VtkJsFollowerViewStateMessage
            {
                Type = "prepomax.follower-view.updated-v1",
                ActorNames = (string[])actorNames.Clone(),
                NodeIds = (int[])nodeIds.Clone()
            };
            PublishFollowerViewState();
            WriteTrace("animation follower enabled points=" + nodeIds.Length);
        }

        public void ClearFollowerView()
        {
            _followerViewState = VtkJsFollowerViewStateMessage.Empty;
            PublishFollowerViewState();
            WriteTrace("animation follower disabled");
        }

        private void PublishFollowerViewState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsFollowerViewStateMessage));
                    serializer.WriteObject(stream, _followerViewState);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception exception)
            {
                WriteTrace("follower view state publish failed: " + exception.Message);
            }
        }

        public void PreviewExplodedView(Dictionary<string, double[]> partOffsets, bool animate, int timeMs,
                                        Action completed = null)
        {
            if (partOffsets == null || partOffsets.Count > 512 || timeMs < 0 || timeMs > 60000 ||
                partOffsets.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > 1024 ||
                    entry.Value == null || entry.Value.Length != 3 || entry.Value.Any(value =>
                        double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1E150)))
            {
                WriteTrace("ignored invalid exploded view preview");
                return;
            }
            foreach (KeyValuePair<string, double[]> entry in partOffsets)
            {
                if (entry.Value.All(value => Math.Abs(value) <= 1E-12)) _explodedPreviewOffsets.Remove(entry.Key);
                else _explodedPreviewOffsets[entry.Key] = (double[])entry.Value.Clone();
            }
            string requestId = null;
            if (completed != null)
            {
                if (_pendingExplodedPreviewCompletion != null)
                {
                    WriteTrace("exploded preview completion superseded");
                    CompleteExplodedViewPreview(_pendingExplodedPreviewRequestId, "superseded");
                }
                requestId = Guid.NewGuid().ToString("N");
                _pendingExplodedPreviewRequestId = requestId;
                _pendingExplodedPreviewCompletion = completed;
                _explodedPreviewCompletionTimer.Stop();
                _explodedPreviewCompletionTimer.Interval = Math.Max(250, Math.Min(62000, timeMs + 2000));
                _explodedPreviewCompletionTimer.Start();
            }
            PublishExplodedViewState(animate, timeMs, partOffsets.Keys, partOffsets, requestId);
            WriteTrace("exploded preview actors=" + partOffsets.Count + " animate=" + animate +
                       " durationMs=" + timeMs + " request=" + (requestId ?? "none"));
            if (completed != null && (!IsBrowserReady || _webView.CoreWebView2 == null))
                CompleteExplodedViewPreview(requestId, "browser-unavailable");
        }

        public void RemoveExplodedViewPreview(string[] partNames)
        {
            if (partNames == null) return;
            var offsets = new Dictionary<string, double[]>(StringComparer.Ordinal);
            foreach (string partName in partNames.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(partName) || partName.Length > 1024) continue;
                _explodedPreviewOffsets.Remove(partName);
                offsets[partName] = new double[] { 0, 0, 0 };
            }
            PublishExplodedViewState(false, 0, offsets.Keys, offsets);
            WriteTrace("exploded preview removed actors=" + offsets.Count);
        }

        public void CompleteAfterNextSceneApplied(Action completed)
        {
            if (completed == null) return;
            if (_pendingSceneAppliedCompletion != null)
            {
                WriteTrace("scene applied completion superseded");
                CompletePendingSceneApplied("superseded");
            }
            _pendingSceneAppliedCompletion = completed;
            _sceneAppliedCompletionTimer.Stop();
            _sceneAppliedCompletionTimer.Start();
            WriteTrace("waiting for browser scene applied");
        }

        public void SetSectionView(double[] point, double[] normal, bool lightenColors, Color sectionColor)
        {
            if (!IsFiniteSectionVector(point, false) || !IsFiniteSectionVector(normal, true))
            {
                WriteTrace("ignored invalid section view state");
                return;
            }
            double length = Math.Sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
            Color color = sectionColor.IsEmpty ? Color.Transparent : sectionColor;
            _sectionViewState = new VtkJsSectionViewStateMessage
            {
                Type = "prepomax.section-view.updated-v1",
                Enabled = true,
                Point = (double[])point.Clone(),
                Normal = new[] { normal[0] / length, normal[1] / length, normal[2] / length },
                LightenColors = lightenColors,
                SectionColor = new[] { color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0 }
            };
            PublishSectionViewState();
            WriteTrace("section view enabled point=" + string.Join(",", point) +
                       " normal=" + string.Join(",", _sectionViewState.Normal));
        }

        public void RemoveSectionView()
        {
            _sectionViewState = VtkJsSectionViewStateMessage.Disabled;
            PublishSectionViewState();
            WriteTrace("section view disabled");
        }

        private static bool IsFiniteSectionVector(double[] values, bool nonzero)
        {
            return values != null && values.Length == 3 && values.All(value => !double.IsNaN(value) &&
                !double.IsInfinity(value) && Math.Abs(value) <= 1E150) &&
                (!nonzero || values.Any(value => Math.Abs(value) > 1E-12));
        }

        private void PublishSectionViewState()
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsSectionViewStateMessage));
                    serializer.WriteObject(stream, _sectionViewState);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception exception)
            {
                WriteTrace("section view state publish failed: " + exception.Message);
            }
        }

        private void PublishExplodedViewState(bool animate, int timeMs, IEnumerable<string> actorNames = null,
                                               Dictionary<string, double[]> explicitOffsets = null,
                                               string requestId = null)
        {
            if (!IsBrowserReady || _webView.CoreWebView2 == null) return;
            try
            {
                IEnumerable<string> names = actorNames ?? _explodedPreviewOffsets.Keys;
                VtkJsExplodedViewOffsetMessage[] offsets = names.Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .Select(name => new VtkJsExplodedViewOffsetMessage
                    {
                        ActorName = name,
                        Offset = explicitOffsets != null && explicitOffsets.TryGetValue(name, out double[] value)
                            ? (double[])value.Clone()
                            : (double[])_explodedPreviewOffsets[name].Clone()
                    }).ToArray();
                var state = new VtkJsExplodedViewPreviewMessage
                {
                    Type = "prepomax.exploded-view.preview-v1",
                    Offsets = offsets,
                    Animate = animate,
                    DurationMs = timeMs,
                    RequestId = requestId
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsExplodedViewPreviewMessage));
                    serializer.WriteObject(stream, state);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception exception)
            {
                WriteTrace("exploded view state publish failed: " + exception.Message);
            }
        }

        public void SetCustomCamera(double[] position, double[] focalPoint, double[] viewUp, double parallelScale)
        {
            SetCustomCamera(position, focalPoint, viewUp, parallelScale, "set");
        }

        public async void SetCustomCamera(double[] position, double[] focalPoint, double[] viewUp,
                                          double parallelScale, string operation)
        {
            if (!_started || IsDisposed) return;
            try
            {
                await _host.SetCustomCameraAsync(position, focalPoint, viewUp, parallelScale, operation);
                WriteTrace("custom camera updated operation=" + operation);
            }
            catch (Exception exception)
            {
                ShowError("A custom PrePoMax camera update could not be published to VTK.js: " + exception.Message);
            }
        }

        public async void UpdateActor(string oldName, string newName, Color color)
        {
            try
            {
                bool auxiliaryPrimitiveUpdated = false;
                if (_auxiliaryPrimitives.TryGetValue(oldName, out VtkJsTransientPrimitiveMessage auxiliary))
                {
                    _auxiliaryPrimitives.Remove(oldName);
                    auxiliary.Name = newName;
                    auxiliary.Rgba = new[] { color.R / 255.0, color.G / 255.0,
                                             color.B / 255.0, color.A / 255.0 };
                    _auxiliaryPrimitives[newName] = auxiliary;
                    ScheduleTransientPrimitivePublish();
                    auxiliaryPrimitiveUpdated = true;
                }
                _sceneCapture.UpdateActor(oldName, newName, color);
                if (_pendingScalarActors.Remove(oldName)) _pendingScalarActors.Add(newName);
                if (_actorColorContours.TryGetValue(oldName, out bool colorContours))
                {
                    _actorColorContours.Remove(oldName);
                    _actorColorContours[newName] = colorContours;
                }
                if (_hiddenActorNames.Remove(oldName)) _hiddenActorNames.Add(newName);
                if (_actorExtrema.TryGetValue(oldName, out VtkJsActorExtremaState extrema))
                {
                    _actorExtrema.Remove(oldName);
                    _actorExtrema[newName] = extrema;
                    RefreshExtremaAnnotations();
                }
                RenameActorReferences(_selectionState.ActorNames, oldName, newName);
                if (_highlightedActorNames.Remove(oldName)) _highlightedActorNames.Add(newName);
                if (_transientSelectionActorNames.Remove(oldName)) _transientSelectionActorNames.Add(newName);
                if (_transientSelectionSecondary.TryGetValue(oldName, out bool secondary))
                {
                    _transientSelectionSecondary.Remove(oldName);
                    _transientSelectionSecondary[newName] = secondary;
                }
                RenameActorReferences(_followerViewState.ActorNames, oldName, newName);
                RenameDictionaryKey(_explodedPreviewOffsets, oldName, newName);
                if (_animationSources.TryGetValue(oldName, out vtkControl.vtkMaxActorData animationSource))
                {
                    _animationSources.Remove(oldName);
                    animationSource.Name = newName;
                    _animationSources[newName] = animationSource;
                }
                PublishActorContourState();
                PublishActorHighlightState();
                if (!_started || IsDisposed || auxiliaryPrimitiveUpdated)
                {
                    if (auxiliaryPrimitiveUpdated)
                        WriteTrace("auxiliary primitive presentation old='" + oldName + "' new='" + newName + "'");
                    return;
                }
                await _host.UpdateActorAsync(oldName, newName, color.ToArgb());
                WriteTrace("actor presentation old='" + oldName + "' new='" + newName + "'");
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax actor presentation update could not be published to VTK.js: " + exception.Message);
            }
        }

        private static void RenameActorReferences(string[] actorNames, string oldName, string newName)
        {
            if (actorNames == null) return;
            for (int i = 0; i < actorNames.Length; i++)
                if (string.Equals(actorNames[i], oldName, StringComparison.Ordinal)) actorNames[i] = newName;
        }

        private static void RenameDictionaryKey<T>(Dictionary<string, T> dictionary,
                                                    string oldName, string newName)
        {
            if (!dictionary.TryGetValue(oldName, out T value)) return;
            dictionary.Remove(oldName);
            dictionary[newName] = value;
        }

        public async void RemoveActors(IEnumerable<string> actorNames)
        {
            try
            {
                if (actorNames == null) throw new ArgumentNullException("actorNames");
                string[] names = actorNames.Distinct(StringComparer.Ordinal).ToArray();
                if (names.Length == 0) return;
                string[] retainedNames = names.Where(name => _sceneCapture.ContainsActor(name)).ToArray();
                _sceneCapture.RemoveActors(names);
                bool primitiveRemoved = false;
                foreach (string name in names)
                {
                    _pendingScalarActors.Remove(name);
                    _actorExtrema.Remove(name);
                    _actorColorContours.Remove(name);
                    _hiddenActorNames.Remove(name);
                    _highlightedActorNames.Remove(name);
                    _transientSelectionActorNames.Remove(name);
                    _transientSelectionSecondary.Remove(name);
                    _animationSources.Remove(name);
                    primitiveRemoved |= _auxiliaryPrimitives.Remove(name);
                    primitiveRemoved |= _transientPrimitives.Remove(name);
                }
                if (primitiveRemoved) ScheduleTransientPrimitivePublish();
                string[] removedSymbols = _sphereSymbols.Keys.Where(symbolName => names.Any(name =>
                    string.Equals(symbolName, name, StringComparison.Ordinal) ||
                    symbolName.StartsWith(name + ActorNameSeparator, StringComparison.Ordinal)))
                    .ToArray();
                foreach (string symbolName in removedSymbols) _sphereSymbols.Remove(symbolName);
                if (removedSymbols.Length > 0) ScheduleSphereSymbolPublish();
                string[] removedAxes = _coordinateAxes.Keys.Where(axisName => names.Any(name =>
                    string.Equals(axisName, name, StringComparison.Ordinal) ||
                    axisName.StartsWith(name + ActorNameSeparator, StringComparison.Ordinal))).ToArray();
                string[] removedCaptions = _captions.Keys.Where(captionName => names.Any(name =>
                    string.Equals(captionName, name, StringComparison.Ordinal) ||
                    captionName.StartsWith(name + ActorNameSeparator, StringComparison.Ordinal))).ToArray();
                foreach (string axisName in removedAxes) _coordinateAxes.Remove(axisName);
                foreach (string captionName in removedCaptions) _captions.Remove(captionName);
                if (removedAxes.Length > 0 || removedCaptions.Length > 0) ScheduleCoordinateOverlayPublish();
                string[] removedGlyphs = _directionalGlyphs.Keys.Where(glyphName => names.Any(name =>
                    string.Equals(glyphName, name, StringComparison.Ordinal) ||
                    glyphName.StartsWith(name + ActorNameSeparator, StringComparison.Ordinal))).ToArray();
                foreach (string glyphName in removedGlyphs) _directionalGlyphs.Remove(glyphName);
                if (removedGlyphs.Length > 0) ScheduleDirectionalGlyphPublish();
                RefreshExtremaAnnotations();
                PublishActorContourState();
                PublishActorHighlightState();
                if (!_started || IsDisposed || retainedNames.Length == 0) return;
                await _host.RemoveActorsAsync(retainedNames);
                WriteTrace("actor removal actors=" + retainedNames.Length);
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax actor removal could not be published to VTK.js: " + exception.Message);
            }
        }

        public void UpdateActorScalars(string actorName, float[] values)
        {
            try
            {
                if (!_sceneCapture.UpdateScalars(actorName, values)) return;
                _pendingScalarActors.Add(actorName);
                _captureGeneration++;
                WriteTrace("updated actor scalars actor='" + actorName + "' values=" + values.Length);
                SchedulePublish();
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax scalar update could not be captured for VTK.js: " + exception.Message);
            }
        }

        public vtkControl.vtkMaxActorData[] GetExportActorData()
        {
            vtkControl.vtkMaxActorData[] data =
                _sceneCapture.ExportActorData(_hiddenActorNames, _actorColorContours)
                    .Where(actor => !_transientSelectionActorNames.Contains(actor.Name)).ToArray();
            WriteCompatibilityTrace("result-export", "captured-actor-data");
            WriteTrace("prepared retained export actors=" + data.Length);
            return data;
        }

        public void SetColorSpectrum(vtkControl.vtkMaxColorSpectrum colorSpectrum)
        {
            if (colorSpectrum == null) throw new ArgumentNullException("colorSpectrum");
            _colorSpectrum.DeepCopy(colorSpectrum);
            PublishScalarLegendState();
        }

        public vtkControl.vtkMaxLookupTable GetExportLookupTable()
        {
            double minimum = 0;
            double maximum = 1;
            bool allFramesRange = _useAllFramesScalarRange && _animationScalarRange.Length >= 2 &&
                                  IsFinite(_animationScalarRange[0]) && IsFinite(_animationScalarRange[1]);
            if (allFramesRange)
            {
                minimum = _animationScalarRange[0];
                maximum = _animationScalarRange[1];
            }
            else
            {
                _sceneCapture.TryGetScalarRange(_hiddenActorNames, _actorColorContours, out minimum, out maximum);
                if (double.IsInfinity(minimum) || double.IsInfinity(maximum))
                {
                    minimum = 0;
                    maximum = 1;
                }
            }
            vtkControl.vtkMaxLookupTable table = VtkJsExportLookupTable.Create(_colorSpectrum, minimum, maximum);
            WriteCompatibilityTrace("result-export", "lookup-table-managed");
            WriteTrace("prepared retained lookup colors=" + table.Colors.Length +
                       " range=" + table.MinimumValue.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       ".." + table.MaximumValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return table;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public async void SetFieldMetadata(string name, string component, string units)
        {
            _fieldName = string.IsNullOrWhiteSpace(name) ? "PrePoMax values" : name.Trim();
            _fieldComponent = string.IsNullOrWhiteSpace(component) ? "value" : component.Trim();
            _fieldUnits = units == null ? string.Empty : units.Trim();
            RefreshExtremaAnnotations();
            if (!_started || IsDisposed) return;
            try
            {
                await _host.SetFieldMetadataAsync(_fieldName, _fieldComponent, _fieldUnits);
                WriteTrace("field metadata name='" + _fieldName + "' component='" + _fieldComponent + "'");
            }
            catch (Exception exception)
            {
                ShowError("PrePoMax field metadata could not be published to VTK.js: " + exception.Message);
            }
        }

        private void SchedulePublish()
        {
            if (!_started || IsDisposed) return;
            _publishTimer.Stop();
            _publishTimer.Interval = 250;
            _publishTimer.Start();
        }

        private void ScheduleAnimationPublish()
        {
            if (!_started || IsDisposed || _publishTimer.Enabled) return;
            _publishTimer.Interval = 33;
            _publishTimer.Start();
        }

        private async Task PublishCapturedSceneAsync()
        {
            _publishTimer.Stop();
            if (!_started || _publishing || IsDisposed) return;
            _publishing = true;
            int generation = _captureGeneration;
            bool publishTopology = _topologyDirty;
            bool publishFrame = !publishTopology && _frameDirty;
            string[] scalarActors = new List<string>(_pendingScalarActors).ToArray();
            int publishedTopologyActorCount = 0;
            try
            {
                if (publishTopology)
                {
                    VtkJsSceneSnapshot snapshot = _sceneCapture.Snapshot();
                    publishedTopologyActorCount = snapshot.ActorCount;
                    WriteTrace("publishing topology generation=" + generation + " actors=" + snapshot.ActorCount +
                               " bytes=" + snapshot.ContentLength + " locatorPoints=" + snapshot.LocatorPointCount +
                               " locatorCells=" + snapshot.LocatorCellCount);
                    await _host.UploadSceneAsync(snapshot);
                    _explodedPreviewOffsets.Clear();
                    WriteTrace("published topology generation=" + generation + " actors=" + snapshot.ActorCount);
                    if (snapshot.ActorCount > 0 && !_hasBootstrappedNonEmptyScene)
                    {
                        _hasBootstrappedNonEmptyScene = true;
                        WriteTrace("republishing browser bootstrap after first non-empty topology");
                        await PublishSessionBootstrapAsync();
                    }
                }
                else if (publishFrame)
                {
                    VtkJsFrameSnapshot snapshot = _sceneCapture.FrameSnapshot(_animationSources.Keys);
                    WriteTrace("publishing frame generation=" + generation + " actors=" + snapshot.ActorCount +
                               " bytes=" + snapshot.ContentLength);
                    VtkJsFrameUploadMetrics metrics = await _host.UploadFrameAsync(snapshot);
                    WriteTrace("frame transport generation=" + generation +
                               " writeMs=" + metrics.WriteMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " responseMs=" + metrics.ResponseMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " totalMs=" + metrics.TotalMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " serverReadMs=" + metrics.ServerReadMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " serverUpdateMs=" + metrics.ServerUpdateMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " serverTotalMs=" + metrics.ServerTotalMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " serverReadAllocatedBytes=" + metrics.ServerReadAllocatedBytes +
                               " serverUpdateAllocatedBytes=" + metrics.ServerUpdateAllocatedBytes +
                               " validationMs=" + metrics.ValidationMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " validationAllocatedBytes=" + metrics.ValidationAllocatedBytes +
                               " resourcesMs=" + metrics.ResourcesMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " resourcesAllocatedBytes=" + metrics.ResourcesAllocatedBytes +
                               " manifestMs=" + metrics.ManifestMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " manifestAllocatedBytes=" + metrics.ManifestAllocatedBytes +
                               " refitMs=" + metrics.RefitMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                               " refitAllocatedBytes=" + metrics.RefitAllocatedBytes);
                    WriteTrace("published frame generation=" + generation + " actors=" + snapshot.ActorCount);
                }
                else
                {
                    VtkJsScalarSnapshot snapshot = _sceneCapture.ScalarSnapshot(scalarActors);
                    if (snapshot.ActorCount == 0) return;
                    WriteTrace("publishing scalars generation=" + generation + " actors=" + snapshot.ActorCount +
                               " bytes=" + snapshot.ContentLength);
                    await _host.UploadScalarsAsync(snapshot);
                    WriteTrace("published scalars generation=" + generation + " actors=" + snapshot.ActorCount);
                    if (_traceNextStaticScalarUpdate)
                    {
                        _traceNextStaticScalarUpdate = false;
                        WriteAcceptanceTrace("static scalar update applied generation=" + generation);
                    }
                }
                if (generation == _captureGeneration)
                {
                    if (publishTopology)
                    {
                        _topologyDirty = false;
                        HasPublishedTopology = publishedTopologyActorCount > 0;
                    }
                    if (publishFrame) _frameDirty = false;
                    foreach (string actorName in scalarActors) _pendingScalarActors.Remove(actorName);
                }
                PublishScalarLegendState();
            }
            catch (Exception exception)
            {
                ShowError("The PrePoMax scene could not be published to VTK.js: " + exception.Message);
            }
            finally
            {
                _publishing = false;
                if (_started && generation != _captureGeneration) SchedulePublish();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelPendingAnimationFrameActivation();
                _host.Dispose();
                _publishTimer.Dispose();
                _annotationPublishTimer.Dispose();
                _sphereSymbolPublishTimer.Dispose();
                _transientPrimitivePublishTimer.Dispose();
                _coordinateOverlayPublishTimer.Dispose();
                _directionalGlyphPublishTimer.Dispose();
                _explodedPreviewCompletionTimer.Dispose();
                _sceneAppliedCompletionTimer.Dispose();
                _webView.Dispose();
            }
            base.Dispose(disposing);
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs args)
        {
            CancelPendingAnimationFrameActivation();
            Uri target;
            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out target) ||
                _baseUri == null ||
                !string.Equals(target.Scheme, _baseUri.Scheme, StringComparison.Ordinal) ||
                !string.Equals(target.Host, _baseUri.Host, StringComparison.Ordinal) ||
                target.Port != _baseUri.Port)
            {
                args.Cancel = true;
                ShowError("The embedded renderer blocked navigation outside its loopback origin.");
            }
        }

        private void CancelPendingAnimationFrameActivation()
        {
            TaskCompletionSource<long> pending = _pendingAnimationFrameActivation;
            _pendingAnimationFrameActivation = null;
            pending?.TrySetCanceled();
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                Uri source;
                if (_baseUri == null || !Uri.TryCreate(args.Source, UriKind.Absolute, out source) ||
                    !string.Equals(source.Scheme, _baseUri.Scheme, StringComparison.Ordinal) ||
                    !string.Equals(source.Host, _baseUri.Host, StringComparison.Ordinal) ||
                    source.Port != _baseUri.Port)
                {
                    WriteTrace("ignored WebView message from a non-renderer origin");
                    return;
                }

                string json = args.TryGetWebMessageAsString();
                VtkJsWebMessageHeader header;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsWebMessageHeader));
                    header = serializer.ReadObject(stream) as VtkJsWebMessageHeader;
                }
                if (header != null && header.Type == "prepomax.client.ready-v1")
                {
                    _ = PublishSessionBootstrapAsync();
                    return;
                }
                if (header != null && header.Type == "prepomax.frame.activated-v1")
                {
                    HandleFrameActivatedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.follower-view.applied-v1")
                {
                    HandleFollowerViewAppliedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.exploded-view.applied-v1")
                {
                    HandleExplodedViewAppliedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.scene.applied-v1")
                {
                    HandleSceneAppliedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.section-view.applied-v1")
                {
                    HandleSectionViewAppliedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.transformations.applied-v1")
                {
                    HandleTransformationsAppliedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.area.validated-v1")
                {
                    HandleValidatedAreaMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.annotation.edited-v1")
                {
                    HandleAnnotationEditedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.annotation.moved-v1")
                {
                    HandleAnnotationMovedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.annotation.action-v1")
                {
                    HandleAnnotationActionMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.camera.changed-v1")
                {
                    HandleCameraChangedMessage(json);
                    return;
                }
                if (header != null && header.Type == "prepomax.box-zoom.ended-v1")
                {
                    HandleBoxZoomEndedMessage(json);
                    return;
                }
                VtkJsValidatedPickMessage message;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsValidatedPickMessage));
                    message = serializer.ReadObject(stream) as VtkJsValidatedPickMessage;
                }
                if (message == null || message.Type != "prepomax.pick.validated-v1" || !message.Validated ||
                    string.IsNullOrWhiteSpace(message.ActorName) || message.ActorName.Length > 1024 ||
                    message.TriangleSourceNodeIds == null || message.TriangleSourceNodeIds.Length != 3 ||
                    message.WorldPosition == null || message.WorldPosition.Length != 3 ||
                    message.CameraDirection == null || message.CameraDirection.Length != 3 ||
                    double.IsNaN(message.SelectionPrecision) || double.IsInfinity(message.SelectionPrecision) ||
                    message.SelectionPrecision <= 0 ||
                    Array.Exists(message.WorldPosition, value => double.IsNaN(value) || double.IsInfinity(value)) ||
                    Array.Exists(message.CameraDirection, value => double.IsNaN(value) || double.IsInfinity(value)) ||
                    (message.Operation != "replace" && message.Operation != "add" &&
                     message.Operation != "subtract" && message.Operation != "intersect"))
                {
                    WriteTrace("ignored malformed WebView pick message");
                    return;
                }

                WriteTrace("validated browser pick element=" + message.SourceElementId +
                           " node=" + message.SourceNodeId +
                           " triangleNodes=" + string.Join(",", message.TriangleSourceNodeIds) +
                           " operation=" + message.Operation);
                ValidatedPick?.Invoke(this, new VtkJsValidatedPickEventArgs(
                    message.SourceElementId, message.SourceNodeId, message.TriangleSourceNodeIds,
                    message.WorldPosition, message.CameraDirection, message.SelectionPrecision,
                    message.ActorName, message.Operation));
            }
            catch (Exception exception)
            {
                WriteTrace("ignored invalid WebView message: " + exception.Message);
            }
        }

        private async Task PublishSessionBootstrapAsync()
        {
            if (_sessionBootstrapInFlight || _webView.CoreWebView2 == null) return;
            _sessionBootstrapInFlight = true;
            try
            {
                VtkJsBrowserSession session = await _host.CreateBrowserSessionAsync();
                var message = new VtkJsSessionBootstrapMessage
                {
                    Type = "prepomax.session.bootstrap-v1",
                    SessionId = session.SessionId,
                    AccessToken = session.AccessToken,
                    SceneId = session.SceneId
                };
                string json;
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VtkJsSessionBootstrapMessage));
                    serializer.WriteObject(stream, message);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                }
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                WriteTrace("browser session bootstrap published session=" + session.SessionId +
                           " scene=" + session.SceneId);
            }
            catch (Exception exception)
            {
                ShowError("The embedded renderer session could not be initialized. " + exception.Message);
            }
            finally
            {
                _sessionBootstrapInFlight = false;
            }
        }

        private void HandleTransformationsAppliedMessage(string json)
        {
            VtkJsTransformationsAppliedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsTransformationsAppliedMessage));
                message = serializer.ReadObject(stream) as VtkJsTransformationsAppliedMessage;
            }
            if (message == null || message.Type != "prepomax.transformations.applied-v1" ||
                message.Matrices < 0 || message.Matrices > 256 || message.Actors < 0 || message.Actors > 65536 ||
                message.VisibleActors < 0 || message.VisibleActors > message.Actors ||
                message.PickableActors != 0)
            {
                WriteTrace("ignored malformed WebView transformation message");
                return;
            }
            BrowserAppliedTransformationMatrices = message.Matrices;
            BrowserAppliedTransformationActors = message.Actors;
            BrowserVisibleTransformationActors = message.VisibleActors;
            BrowserPickableTransformationActors = message.PickableActors;
            WriteTrace("browser transformations applied matrices=" + message.Matrices +
                       " actors=" + message.Actors + " visible=" + message.VisibleActors +
                       " pickable=" + message.PickableActors);
        }

        private void HandleFrameActivatedMessage(string json)
        {
            VtkJsFrameActivatedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsFrameActivatedMessage));
                message = serializer.ReadObject(stream) as VtkJsFrameActivatedMessage;
            }
            if (message == null || message.Type != "prepomax.frame.activated-v1" ||
                message.SceneRevision < 1 || message.Chunks < 1 || message.Chunks > 512 ||
                message.Bytes < 1 || message.Bytes > 256L * 1024 * 1024 ||
                !IsFiniteDuration(message.DownloadMs) || !IsFiniteDuration(message.ApplyMs) ||
                !IsFiniteDuration(message.StablePaintMs) || !IsFiniteDuration(message.TotalMs) ||
                message.TotalMs + 0.1 < message.DownloadMs + message.ApplyMs + message.StablePaintMs)
            {
                WriteTrace("ignored malformed WebView frame activation message");
                return;
            }
            WriteTrace("browser frame activated revision=" + message.SceneRevision +
                       " chunks=" + message.Chunks + " bytes=" + message.Bytes +
                       " downloadMs=" + message.DownloadMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       " applyMs=" + message.ApplyMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       " paintMs=" + message.StablePaintMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       " totalMs=" + message.TotalMs.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                       " followerAdjusted=" + message.FollowerAdjusted +
                       " cameraAdjusted=" + message.CameraAdjusted);
            BrowserAppliedSceneRevision = Math.Max(BrowserAppliedSceneRevision, message.SceneRevision);
            CompletePendingAnimationFrameActivation(message.SceneRevision, null);
        }

        private void HandleFollowerViewAppliedMessage(string json)
        {
            VtkJsFollowerViewAppliedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsFollowerViewAppliedMessage));
                message = serializer.ReadObject(stream) as VtkJsFollowerViewAppliedMessage;
            }
            if (message == null || message.Type != "prepomax.follower-view.applied-v1" ||
                message.SceneRevision < 1 || _followerViewState.NodeIds.Length == 0)
            {
                WriteTrace("ignored malformed or inactive follower view completion");
                return;
            }
            WriteTrace("browser follower applied revision=" + message.SceneRevision +
                       " followerAdjusted=" + message.FollowerAdjusted +
                       " cameraAdjusted=" + message.CameraAdjusted);
        }

        private void HandleExplodedViewAppliedMessage(string json)
        {
            VtkJsExplodedViewAppliedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsExplodedViewAppliedMessage));
                message = serializer.ReadObject(stream) as VtkJsExplodedViewAppliedMessage;
            }
            if (message == null || message.Type != "prepomax.exploded-view.applied-v1" ||
                message.ActorCount < 0 || message.ActorCount > 512 ||
                (message.RequestId != null && (message.RequestId.Length != 32 ||
                 message.RequestId.Any(value => !Uri.IsHexDigit(value)))))
            {
                WriteTrace("ignored malformed exploded view completion");
                return;
            }
            WriteTrace("browser exploded preview applied actors=" + message.ActorCount +
                       " animated=" + message.Animated + " request=" + (message.RequestId ?? "none"));
            if (message.RequestId != null) CompleteExplodedViewPreview(message.RequestId, "browser-acknowledged");
        }

        private void CompleteExplodedViewPreview(string requestId, string reason)
        {
            if (requestId == null || !string.Equals(requestId, _pendingExplodedPreviewRequestId,
                                                     StringComparison.Ordinal)) return;
            Action completed = _pendingExplodedPreviewCompletion;
            _pendingExplodedPreviewRequestId = null;
            _pendingExplodedPreviewCompletion = null;
            _explodedPreviewCompletionTimer.Stop();
            WriteTrace("exploded preview completion " + reason + " request=" + requestId);
            try
            {
                completed?.Invoke();
            }
            catch (Exception exception)
            {
                WriteTrace("exploded preview completion failed: " + exception.Message);
            }
        }

        private void HandleSceneAppliedMessage(string json)
        {
            VtkJsSceneAppliedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsSceneAppliedMessage));
                message = serializer.ReadObject(stream) as VtkJsSceneAppliedMessage;
            }
            if (message == null || message.Type != "prepomax.scene.applied-v1" ||
                !Guid.TryParseExact(message.SceneId, "D", out Guid sceneId) ||
                message.SceneRevision < 0 || message.ActorCount < 0 || message.ActorCount > 4096 ||
                message.PipelineChunks < 0 || message.PipelineChunks > 65536 ||
                (message.ActorCount > 0 && message.PipelineChunks == 0))
            {
                WriteTrace("ignored malformed scene applied completion");
                return;
            }
            BrowserAppliedActorCount = message.ActorCount;
            BrowserAppliedPipelineChunks = message.PipelineChunks;
            BrowserAppliedSceneRevision = message.SceneRevision;
            BrowserAppliedSceneId = sceneId;
            WriteTrace("browser scene applied scene=" + sceneId.ToString("D") +
                       " revision=" + message.SceneRevision + " actors=" + message.ActorCount +
                       " chunks=" + message.PipelineChunks);
            CompletePendingAnimationFrameActivation(message.SceneRevision, sceneId);
            CompletePendingSceneApplied("browser-acknowledged");
        }

        private void CompletePendingAnimationFrameActivation(long sceneRevision, Guid? sceneId)
        {
            bool replacementScene = sceneId.HasValue && _pendingAnimationFramePriorSceneId != Guid.Empty &&
                                    sceneId.Value != _pendingAnimationFramePriorSceneId;
            if (_pendingAnimationFrameActivation != null &&
                (replacementScene || sceneRevision >= _pendingAnimationFrameMinimumRevision))
                _pendingAnimationFrameActivation.TrySetResult(sceneRevision);
        }

        private void HandleSectionViewAppliedMessage(string json)
        {
            VtkJsSectionViewAppliedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsSectionViewAppliedMessage));
                message = serializer.ReadObject(stream) as VtkJsSectionViewAppliedMessage;
            }
            if (message == null || message.Type != "prepomax.section-view.applied-v1" ||
                message.ClippedChunks < 0 || message.ClippedChunks > 512 ||
                message.CapActors < 0 || message.CapActors > 512 ||
                message.SkippedCells < 0 || message.SkippedCells > 5000000)
            {
                WriteTrace("ignored malformed section view completion");
                return;
            }
            WriteTrace("browser section view applied enabled=" + message.Enabled +
                       " clippedChunks=" + message.ClippedChunks + " capActors=" + message.CapActors +
                       " skippedCells=" + message.SkippedCells);
        }

        private void CompletePendingSceneApplied(string reason)
        {
            Action completed = _pendingSceneAppliedCompletion;
            if (completed == null) return;
            _pendingSceneAppliedCompletion = null;
            _sceneAppliedCompletionTimer.Stop();
            WriteTrace("scene applied completion " + reason);
            try
            {
                completed();
            }
            catch (Exception exception)
            {
                WriteTrace("scene applied completion failed: " + exception.Message);
            }
        }

        private void HandleCameraChangedMessage(string json)
        {
            VtkJsCameraChangedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsCameraChangedMessage));
                message = serializer.ReadObject(stream) as VtkJsCameraChangedMessage;
            }
            if (message == null || message.Type != "prepomax.camera.changed-v1" ||
                !IsFiniteCameraVector(message.Position) || !IsFiniteCameraVector(message.FocalPoint) ||
                !IsFiniteCameraVector(message.ViewUp) || double.IsNaN(message.ParallelScale) ||
                double.IsInfinity(message.ParallelScale) || message.ParallelScale <= 0 ||
                VectorLengthSquared(message.Position, message.FocalPoint) <= 1E-24 ||
                VectorLengthSquared(message.ViewUp, null) <= 1E-24 ||
                !AreCameraAxesNonDegenerate(message.Position, message.FocalPoint, message.ViewUp))
            {
                WriteTrace("ignored malformed WebView camera message");
                return;
            }
            WriteTrace("camera state received scale=" +
                       message.ParallelScale.ToString(System.Globalization.CultureInfo.InvariantCulture));
            CameraChanged?.Invoke(this, new VtkJsCameraChangedEventArgs(
                message.Position, message.FocalPoint, message.ViewUp, message.ParallelScale));
        }

        private void HandleBoxZoomEndedMessage(string json)
        {
            VtkJsBoxZoomEndedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsBoxZoomEndedMessage));
                message = serializer.ReadObject(stream) as VtkJsBoxZoomEndedMessage;
            }
            if (message == null || message.Type != "prepomax.box-zoom.ended-v1" || !_boxZoomEnabled)
            {
                WriteTrace("ignored malformed or inactive WebView box zoom message");
                return;
            }
            _boxZoomEnabled = false;
            WriteTrace("box zoom ended applied=" + message.Applied);
            BoxZoomEnded?.Invoke(this, new VtkJsBoxZoomEndedEventArgs(message.Applied));
        }

        private static bool IsFiniteCameraVector(double[] vector)
        {
            return vector != null && vector.Length == 3 &&
                   !Array.Exists(vector, value => double.IsNaN(value) || double.IsInfinity(value) ||
                                                 Math.Abs(value) > 1E150);
        }

        private static double VectorLengthSquared(double[] first, double[] second)
        {
            double x = first[0] - (second == null ? 0 : second[0]);
            double y = first[1] - (second == null ? 0 : second[1]);
            double z = first[2] - (second == null ? 0 : second[2]);
            return x * x + y * y + z * z;
        }

        private static bool AreCameraAxesNonDegenerate(double[] position, double[] focalPoint, double[] viewUp)
        {
            double x = focalPoint[0] - position[0];
            double y = focalPoint[1] - position[1];
            double z = focalPoint[2] - position[2];
            double crossX = y * viewUp[2] - z * viewUp[1];
            double crossY = z * viewUp[0] - x * viewUp[2];
            double crossZ = x * viewUp[1] - y * viewUp[0];
            double crossSquared = crossX * crossX + crossY * crossY + crossZ * crossZ;
            double product = VectorLengthSquared(position, focalPoint) * VectorLengthSquared(viewUp, null);
            return crossSquared > 1E-24 * product;
        }

        private static bool IsFiniteDuration(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 600000;
        }

        private void HandleAnnotationEditedMessage(string json)
        {
            VtkJsAnnotationEditedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsAnnotationEditedMessage));
                message = serializer.ReadObject(stream) as VtkJsAnnotationEditedMessage;
            }
            if (message == null || message.Type != "prepomax.annotation.edited-v1" ||
                string.IsNullOrWhiteSpace(message.Name) || message.Name.Length > 1024 ||
                string.IsNullOrEmpty(message.Text) || message.Text.Length > 65536 ||
                message.Name == "Measure_Annotation" || message.Name == "Min_Annotation" ||
                message.Name == "Max_Annotation")
            {
                WriteTrace("ignored malformed or reserved WebView annotation edit");
                return;
            }
            WriteTrace("annotation edit requested name='" + message.Name + "'");
            AnnotationEdited?.Invoke(this, new VtkJsAnnotationEditedEventArgs(message.Name, message.Text));
        }

        private void HandleAnnotationMovedMessage(string json)
        {
            VtkJsAnnotationMovedMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsAnnotationMovedMessage));
                message = serializer.ReadObject(stream) as VtkJsAnnotationMovedMessage;
            }
            if (message == null || message.Type != "prepomax.annotation.moved-v1" ||
                string.IsNullOrWhiteSpace(message.Name) || message.Name.Length > 1024 ||
                message.Offset == null || message.Offset.Length != 2 ||
                Array.Exists(message.Offset, value => double.IsNaN(value) || double.IsInfinity(value) ||
                                                      Math.Abs(value) > 100000) ||
                (!_annotations.ContainsKey(message.Name) && !_extremaAnnotationSettings.ContainsKey(message.Name)))
            {
                WriteTrace("ignored malformed or unknown WebView annotation move");
                return;
            }
            _annotationOffsets[message.Name] = (double[])message.Offset.Clone();
            if (_annotations.TryGetValue(message.Name, out VtkJsAnnotationMessage annotation))
                annotation.Offset = (double[])message.Offset.Clone();
            WriteTrace("annotation moved name='" + message.Name + "' offset=" +
                       message.Offset[0] + "," + message.Offset[1]);
            ScheduleAnnotationPublish();
        }

        private void HandleAnnotationActionMessage(string json)
        {
            VtkJsAnnotationActionMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsAnnotationActionMessage));
                message = serializer.ReadObject(stream) as VtkJsAnnotationActionMessage;
            }
            bool reserved = message != null &&
                (message.Name == "Measure_Annotation" || message.Name == "Min_Annotation" ||
                 message.Name == "Max_Annotation");
            if (message == null || message.Type != "prepomax.annotation.action-v1" ||
                string.IsNullOrWhiteSpace(message.Name) || message.Name.Length > 1024 ||
                (message.Action != "reset" && message.Action != "delete" && message.Action != "settings") ||
                !_annotations.ContainsKey(message.Name) ||
                (reserved && message.Action != "settings"))
            {
                WriteTrace("ignored malformed, unknown, or reserved WebView annotation action");
                return;
            }
            WriteTrace("annotation action requested name='" + message.Name + "' action=" + message.Action);
            AnnotationActionRequested?.Invoke(this, new VtkJsAnnotationActionEventArgs(message.Name, message.Action));
        }

        private void HandleValidatedAreaMessage(string json)
        {
            VtkJsValidatedAreaMessage message;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VtkJsValidatedAreaMessage));
                message = serializer.ReadObject(stream) as VtkJsValidatedAreaMessage;
            }
            if (message == null || message.Type != "prepomax.area.validated-v1" || !message.Validated ||
                message.Triangles == null || message.Triangles.Length > 100000 ||
                (message.LocatorValidated && (message.SourceNodeIds == null || message.SourceElementIds == null ||
                 message.ActorNames == null || message.SourceNodeIds.Length > 100000 ||
                 message.SourceElementIds.Length > 100000 || message.ActorNames.Length > 4096 ||
                 Array.Exists(message.ActorNames, name => string.IsNullOrWhiteSpace(name) || name.Length > 1024))) ||
                message.CameraDirection == null || message.CameraDirection.Length != 3 ||
                Array.Exists(message.CameraDirection, value => double.IsNaN(value) || double.IsInfinity(value)) ||
                Array.Exists(message.Triangles, triangle => triangle == null ||
                    string.IsNullOrWhiteSpace(triangle.ActorName) || triangle.ActorName.Length > 1024 ||
                    triangle.TriangleSourceNodeIds == null || triangle.TriangleSourceNodeIds.Length != 3) ||
                (message.Operation != "replace" && message.Operation != "add" &&
                 message.Operation != "subtract" && message.Operation != "intersect"))
            {
                WriteTrace("ignored malformed WebView area message");
                return;
            }
            WriteTrace("validated browser area triangles=" + message.Triangles.Length +
                       " inside=" + message.CompletelyInside + " operation=" + message.Operation);
            ValidatedArea?.Invoke(this, new VtkJsValidatedAreaEventArgs(message.Triangles,
                message.CameraDirection, message.CompletelyInside, message.Operation, message.LocatorValidated,
                message.SourceNodeIds, message.SourceElementIds, message.ActorNames));
        }

        private void ShowError(string message)
        {
            WriteTrace("error: " + message);
            _webView.Visible = false;
            _status.Text = message;
            _status.Visible = true;
            _status.BringToFront();
        }

        private static void WriteTrace(string message)
        {
            string path = Environment.GetEnvironmentVariable("PREPOMAX_RENDER_TRACE_FILE");
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(message)) return;
            try
            {
                File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never affect rendering or the legacy fallback.
            }
        }
    }

    internal sealed class VtkJsCameraChangedEventArgs : EventArgs
    {
        public VtkJsCameraChangedEventArgs(double[] position, double[] focalPoint, double[] viewUp,
                                           double parallelScale)
        {
            Position = (double[])position.Clone();
            FocalPoint = (double[])focalPoint.Clone();
            ViewUp = (double[])viewUp.Clone();
            ParallelScale = parallelScale;
        }

        public double[] Position { get; private set; }
        public double[] FocalPoint { get; private set; }
        public double[] ViewUp { get; private set; }
        public double ParallelScale { get; private set; }
    }

    internal sealed class VtkJsBoxZoomEndedEventArgs : EventArgs
    {
        public VtkJsBoxZoomEndedEventArgs(bool applied)
        {
            Applied = applied;
        }

        public bool Applied { get; private set; }
    }

    [DataContract]
    internal sealed class VtkJsAnnotationStateMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "annotations")]
        public VtkJsAnnotationMessage[] Annotations { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsSphereSymbolStateMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "symbols")]
        public VtkJsSphereSymbolMessage[] Symbols { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsSphereSymbolMessage
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "points")]
        public double[][] Points { get; set; }
        [DataMember(Name = "rgba")]
        public double[] Rgba { get; set; }
        [DataMember(Name = "symbolSize")]
        public double SymbolSize { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsTransientPrimitiveStateMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "primitives")]
        public VtkJsTransientPrimitiveMessage[] Primitives { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsTransientPrimitiveMessage
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "points")]
        public double[][] Points { get; set; }
        [DataMember(Name = "lines")]
        public int[][] Lines { get; set; }
        [DataMember(Name = "drawPoints")]
        public bool DrawPoints { get; set; }
        [DataMember(Name = "rgba")]
        public double[] Rgba { get; set; }
        [DataMember(Name = "pointSize")]
        public double PointSize { get; set; }
        [DataMember(Name = "lineWidth")]
        public double LineWidth { get; set; }
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }
        [IgnoreDataMember]
        public bool UseSecondaryHighlightColor { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsCoordinateOverlayStateMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "axes")]
        public VtkJsCoordinateAxisMessage[] Axes { get; set; }
        [DataMember(Name = "captions")]
        public VtkJsCaptionMessage[] Captions { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsCoordinateAxisMessage
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "point")]
        public double[] Point { get; set; }
        [DataMember(Name = "direction")]
        public double[] Direction { get; set; }
        [DataMember(Name = "rgba")]
        public double[] Rgba { get; set; }
        [DataMember(Name = "symbolSize")]
        public double SymbolSize { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsCaptionMessage
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "text")]
        public string Text { get; set; }
        [DataMember(Name = "position")]
        public double[] Position { get; set; }
        [DataMember(Name = "offsetVector")]
        public double[] OffsetVector { get; set; }
        [DataMember(Name = "rgba")]
        public double[] Rgba { get; set; }
        [DataMember(Name = "fontScaleFactor")]
        public double FontScaleFactor { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsDirectionalGlyphStateMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "glyphs")]
        public VtkJsDirectionalGlyphMessage[] Glyphs { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsActorContourStateMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "actors")]
        public VtkJsActorContourMessage[] Actors { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsActorContourMessage
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "colorContours")]
        public bool ColorContours { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsActorHighlightStateMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "actorNames")]
        public string[] ActorNames { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsViewportAppearanceMessage
    {
        public static VtkJsViewportAppearanceMessage Default
        {
            get
            {
                return new VtkJsViewportAppearanceMessage
                {
                    Type = "prepomax.viewport-appearance.updated-v1",
                    Gradient = false,
                    TopRgb = new[] { 0.87, 0.91, 0.95 },
                    BottomRgb = new[] { 0.87, 0.91, 0.95 },
                    Ambient = 0,
                    Diffuse = 1,
                    Redraw = false
                };
            }
        }

        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "gradient")]
        public bool Gradient { get; set; }
        [DataMember(Name = "topRgb")]
        public double[] TopRgb { get; set; }
        [DataMember(Name = "bottomRgb")]
        public double[] BottomRgb { get; set; }
        [DataMember(Name = "ambient")]
        public double Ambient { get; set; }
        [DataMember(Name = "diffuse")]
        public double Diffuse { get; set; }
        [DataMember(Name = "redraw")]
        public bool Redraw { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsOrientationMarkerMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsGraphicsInteractionMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "pointSmoothing")]
        public bool PointSmoothing { get; set; }
        [DataMember(Name = "lineSmoothing")]
        public bool LineSmoothing { get; set; }
        [DataMember(Name = "primaryHighlightRgb")]
        public double[] PrimaryHighlightRgb { get; set; }
        [DataMember(Name = "secondaryHighlightRgb")]
        public double[] SecondaryHighlightRgb { get; set; }
        [DataMember(Name = "mouseHighlightRgb")]
        public double[] MouseHighlightRgb { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsScaleWidgetMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }
        [DataMember(Name = "unit")]
        public string Unit { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsScalarLegendMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }
        [DataMember(Name = "fieldName")]
        public string FieldName { get; set; }
        [DataMember(Name = "componentName")]
        public string ComponentName { get; set; }
        [DataMember(Name = "unit")]
        public string Unit { get; set; }
        [DataMember(Name = "complexComponent")]
        public string ComplexComponent { get; set; }
        [DataMember(Name = "minMaxType")]
        public string MinMaxType { get; set; }
        [DataMember(Name = "numberFormat")]
        public string NumberFormat { get; set; }
        [DataMember(Name = "drawBackground")]
        public bool DrawBackground { get; set; }
        [DataMember(Name = "drawBorder")]
        public bool DrawBorder { get; set; }
        [DataMember(Name = "minimum")]
        public double Minimum { get; set; }
        [DataMember(Name = "maximum")]
        public double Maximum { get; set; }
        [DataMember(Name = "colors")]
        public double[][] Colors { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsColorBarMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }
        [DataMember(Name = "drawBackground")]
        public bool DrawBackground { get; set; }
        [DataMember(Name = "drawBorder")]
        public bool DrawBorder { get; set; }
        [DataMember(Name = "colors")]
        public double[][] Colors { get; set; }
        [DataMember(Name = "labels")]
        public string[] Labels { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsStatusBlockMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }
        [DataMember(Name = "drawBackground")]
        public bool DrawBackground { get; set; }
        [DataMember(Name = "drawBorder")]
        public bool DrawBorder { get; set; }
        [DataMember(Name = "lines")]
        public string[] Lines { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsWidgetOffset
    {
        [DataMember(Name = "top")]
        public int Top { get; set; }
        [DataMember(Name = "left", EmitDefaultValue = false)]
        public int? Left { get; set; }
        [DataMember(Name = "right", EmitDefaultValue = false)]
        public int? Right { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsResultWidgetLayoutMessage
    {
        public static VtkJsResultWidgetLayoutMessage Default => new VtkJsResultWidgetLayoutMessage
        {
            Type = "prepomax.result-widget-layout.updated-v1",
            ScalarLegend = new VtkJsWidgetOffset { Top = 20, Left = 20 },
            ColorBar = new VtkJsWidgetOffset { Top = 20, Left = 20 },
            StatusBlock = new VtkJsWidgetOffset { Top = 20, Right = 20 }
        };

        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "scalarLegend")]
        public VtkJsWidgetOffset ScalarLegend { get; set; }
        [DataMember(Name = "colorBar")]
        public VtkJsWidgetOffset ColorBar { get; set; }
        [DataMember(Name = "statusBlock")]
        public VtkJsWidgetOffset StatusBlock { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsDirectionalGlyphMessage
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "kind")]
        public string Kind { get; set; }
        [DataMember(Name = "points")]
        public double[][] Points { get; set; }
        [DataMember(Name = "directions")]
        public double[][] Directions { get; set; }
        [DataMember(Name = "rgba")]
        public double[] Rgba { get; set; }
        [DataMember(Name = "symbolSize")]
        public double SymbolSize { get; set; }
        [DataMember(Name = "invert", EmitDefaultValue = false)]
        public bool? Invert { get; set; }
        [DataMember(Name = "center", EmitDefaultValue = false)]
        public bool? Center { get; set; }
        [DataMember(Name = "relativeSize", EmitDefaultValue = false)]
        public double? RelativeSize { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsAnnotationMessage
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "text")]
        public string Text { get; set; }
        [DataMember(Name = "anchorPoint")]
        public double[] AnchorPoint { get; set; }
        [DataMember(Name = "offset")]
        public double[] Offset { get; set; }
        [DataMember(Name = "drawBackground")]
        public bool DrawBackground { get; set; }
        [DataMember(Name = "drawBorder")]
        public bool DrawBorder { get; set; }
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }
    }

    internal sealed class VtkJsExtremaAnnotationSettings
    {
        public string NumberFormat { get; set; }
        public bool DrawBackground { get; set; }
        public bool DrawBorder { get; set; }
        public bool Visible { get; set; }
    }

    internal sealed class VtkJsActorExtremaState
    {
        public VtkJsExtremeNode Minimum { get; set; }
        public VtkJsExtremeNode Maximum { get; set; }
        public bool Visible { get; set; }
        public bool ColorContours { get; set; }
    }

    internal sealed class VtkJsExtremeNode
    {
        public int Id { get; set; }
        public float Value { get; set; }
        public double[] Coordinates { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsWebMessageHeader
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsFrameActivatedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "sceneRevision")]
        public long SceneRevision { get; set; }
        [DataMember(Name = "chunks")]
        public int Chunks { get; set; }
        [DataMember(Name = "bytes")]
        public long Bytes { get; set; }
        [DataMember(Name = "followerAdjusted")]
        public bool FollowerAdjusted { get; set; }
        [DataMember(Name = "cameraAdjusted")]
        public bool CameraAdjusted { get; set; }
        [DataMember(Name = "downloadMs")]
        public double DownloadMs { get; set; }
        [DataMember(Name = "applyMs")]
        public double ApplyMs { get; set; }
        [DataMember(Name = "stablePaintMs")]
        public double StablePaintMs { get; set; }
        [DataMember(Name = "totalMs")]
        public double TotalMs { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsFollowerViewAppliedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "sceneRevision")]
        public long SceneRevision { get; set; }
        [DataMember(Name = "followerAdjusted")]
        public bool FollowerAdjusted { get; set; }
        [DataMember(Name = "cameraAdjusted")]
        public bool CameraAdjusted { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsSceneAppliedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "sceneId")]
        public string SceneId { get; set; }
        [DataMember(Name = "sceneRevision")]
        public long SceneRevision { get; set; }
        [DataMember(Name = "actorCount")]
        public int ActorCount { get; set; }
        [DataMember(Name = "pipelineChunks")]
        public int PipelineChunks { get; set; }
    }

    internal sealed class VtkJsTransformationStep
    {
        public double[] Matrix { get; }
        public int ItemCount { get; }

        public VtkJsTransformationStep(double[] matrix, int itemCount)
        {
            Matrix = matrix;
            ItemCount = itemCount;
        }
    }

    [DataContract]
    internal sealed class VtkJsTransformationStateMessage
    {
        public static VtkJsTransformationStateMessage Empty => new VtkJsTransformationStateMessage
        {
            Type = "prepomax.transformations.updated-v1",
            Visible = true,
            Matrices = new double[0][]
        };

        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }
        [DataMember(Name = "matrices")]
        public double[][] Matrices { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsTransformationsAppliedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "matrices")]
        public int Matrices { get; set; }
        [DataMember(Name = "actors")]
        public int Actors { get; set; }
        [DataMember(Name = "visibleActors")]
        public int VisibleActors { get; set; }
        [DataMember(Name = "pickableActors")]
        public int PickableActors { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsSectionViewStateMessage
    {
        public static VtkJsSectionViewStateMessage Disabled => new VtkJsSectionViewStateMessage
        {
            Type = "prepomax.section-view.updated-v1",
            Enabled = false,
            Point = new double[] { 0, 0, 0 },
            Normal = new double[] { 1, 0, 0 },
            LightenColors = true,
            SectionColor = new double[] { 0, 0, 0, 0 }
        };

        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; }
        [DataMember(Name = "point")]
        public double[] Point { get; set; }
        [DataMember(Name = "normal")]
        public double[] Normal { get; set; }
        [DataMember(Name = "lightenColors")]
        public bool LightenColors { get; set; }
        [DataMember(Name = "sectionColor")]
        public double[] SectionColor { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsSectionViewAppliedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; }
        [DataMember(Name = "clippedChunks")]
        public int ClippedChunks { get; set; }
        [DataMember(Name = "capActors")]
        public int CapActors { get; set; }
        [DataMember(Name = "skippedCells", IsRequired = false)]
        public int SkippedCells { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsExplodedViewPreviewMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "offsets")]
        public VtkJsExplodedViewOffsetMessage[] Offsets { get; set; }
        [DataMember(Name = "animate")]
        public bool Animate { get; set; }
        [DataMember(Name = "durationMs")]
        public int DurationMs { get; set; }
        [DataMember(Name = "requestId", EmitDefaultValue = false)]
        public string RequestId { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsExplodedViewOffsetMessage
    {
        [DataMember(Name = "actorName")]
        public string ActorName { get; set; }
        [DataMember(Name = "offset")]
        public double[] Offset { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsExplodedViewAppliedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "actorCount")]
        public int ActorCount { get; set; }
        [DataMember(Name = "animated")]
        public bool Animated { get; set; }
        [DataMember(Name = "requestId", EmitDefaultValue = false)]
        public string RequestId { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsCameraChangedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "position")]
        public double[] Position { get; set; }
        [DataMember(Name = "focalPoint")]
        public double[] FocalPoint { get; set; }
        [DataMember(Name = "viewUp")]
        public double[] ViewUp { get; set; }
        [DataMember(Name = "parallelScale")]
        public double ParallelScale { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsBoxZoomEndedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "applied")]
        public bool Applied { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsAnnotationEditedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "text")]
        public string Text { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsAnnotationMovedMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "offset")]
        public double[] Offset { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsAnnotationActionMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "action")]
        public string Action { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsValidatedPickMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "validated")]
        public bool Validated { get; set; }

        [DataMember(Name = "sourceElementId")]
        public int SourceElementId { get; set; }

        [DataMember(Name = "sourceNodeId")]
        public int SourceNodeId { get; set; }

        [DataMember(Name = "triangleSourceNodeIds")]
        public int[] TriangleSourceNodeIds { get; set; }

        [DataMember(Name = "worldPosition")]
        public double[] WorldPosition { get; set; }

        [DataMember(Name = "cameraDirection")]
        public double[] CameraDirection { get; set; }

        [DataMember(Name = "selectionPrecision")]
        public double SelectionPrecision { get; set; }

        [DataMember(Name = "actorName")]
        public string ActorName { get; set; }

        [DataMember(Name = "operation")]
        public string Operation { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsSessionBootstrapMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "sessionId")]
        public string SessionId { get; set; }

        [DataMember(Name = "accessToken")]
        public string AccessToken { get; set; }

        [DataMember(Name = "sceneId")]
        public string SceneId { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsValidatedAreaMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
        [DataMember(Name = "validated")]
        public bool Validated { get; set; }
        [DataMember(Name = "triangles")]
        public VtkJsValidatedAreaTriangle[] Triangles { get; set; }
        [DataMember(Name = "cameraDirection")]
        public double[] CameraDirection { get; set; }
        [DataMember(Name = "completelyInside")]
        public bool CompletelyInside { get; set; }
        [DataMember(Name = "operation")]
        public string Operation { get; set; }
        [DataMember(Name = "locatorValidated")]
        public bool LocatorValidated { get; set; }
        [DataMember(Name = "sourceNodeIds")]
        public int[] SourceNodeIds { get; set; }
        [DataMember(Name = "sourceElementIds")]
        public int[] SourceElementIds { get; set; }
        [DataMember(Name = "actorNames")]
        public string[] ActorNames { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsValidatedAreaTriangle
    {
        [DataMember(Name = "actorName")]
        public string ActorName { get; set; }
        [DataMember(Name = "sourceElementId")]
        public int SourceElementId { get; set; }
        [DataMember(Name = "triangleSourceNodeIds")]
        public int[] TriangleSourceNodeIds { get; set; }
    }

    internal sealed class VtkJsValidatedAreaEventArgs : EventArgs
    {
        public VtkJsValidatedAreaEventArgs(VtkJsValidatedAreaTriangle[] triangles, double[] cameraDirection,
                                            bool completelyInside, string operation, bool locatorValidated,
                                            int[] sourceNodeIds, int[] sourceElementIds, string[] actorNames)
        {
            Triangles = triangles;
            CameraDirection = cameraDirection;
            CompletelyInside = completelyInside;
            Operation = operation;
            LocatorValidated = locatorValidated;
            SourceNodeIds = sourceNodeIds;
            SourceElementIds = sourceElementIds;
            ActorNames = actorNames;
        }
        public VtkJsValidatedAreaTriangle[] Triangles { get; private set; }
        public double[] CameraDirection { get; private set; }
        public bool CompletelyInside { get; private set; }
        public string Operation { get; private set; }
        public bool LocatorValidated { get; private set; }
        public int[] SourceNodeIds { get; private set; }
        public int[] SourceElementIds { get; private set; }
        public string[] ActorNames { get; private set; }
    }

    internal sealed class VtkJsAnnotationEditedEventArgs : EventArgs
    {
        public VtkJsAnnotationEditedEventArgs(string name, string text)
        {
            Name = name;
            Text = text;
        }
        public string Name { get; private set; }
        public string Text { get; private set; }
    }

    internal sealed class VtkJsAnnotationActionEventArgs : EventArgs
    {
        public VtkJsAnnotationActionEventArgs(string name, string action)
        {
            Name = name;
            Action = action;
        }
        public string Name { get; private set; }
        public string Action { get; private set; }
    }

    internal sealed class VtkJsValidatedPickEventArgs : EventArgs
    {
        public VtkJsValidatedPickEventArgs(int sourceElementId, int sourceNodeId, int[] triangleSourceNodeIds,
                                           double[] worldPosition, double[] cameraDirection,
                                           double selectionPrecision, string actorName, string operation)
        {
            SourceElementId = sourceElementId;
            SourceNodeId = sourceNodeId;
            TriangleSourceNodeIds = triangleSourceNodeIds;
            WorldPosition = worldPosition;
            CameraDirection = cameraDirection;
            SelectionPrecision = selectionPrecision;
            ActorName = actorName;
            Operation = operation;
        }

        public int SourceElementId { get; private set; }
        public int SourceNodeId { get; private set; }
        public int[] TriangleSourceNodeIds { get; private set; }
        public double[] WorldPosition { get; private set; }
        public double[] CameraDirection { get; private set; }
        public double SelectionPrecision { get; private set; }
        public string ActorName { get; private set; }
        public string Operation { get; private set; }
    }

    [DataContract]
    internal sealed class VtkJsFollowerViewStateMessage
    {
        public static readonly VtkJsFollowerViewStateMessage Empty = new VtkJsFollowerViewStateMessage
        {
            Type = "prepomax.follower-view.updated-v1",
            ActorNames = new string[0],
            NodeIds = new int[0]
        };

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "actorNames")]
        public string[] ActorNames { get; set; }

        [DataMember(Name = "nodeIds")]
        public int[] NodeIds { get; set; }
    }

    [DataContract]
    internal sealed class VtkJsSelectionStateMessage
    {
        public static VtkJsSelectionStateMessage Empty => CreateEmpty();

        public static VtkJsSelectionStateMessage CreateEmpty()
        {
            return new VtkJsSelectionStateMessage
            {
                Type = "prepomax.selection.updated-v1",
                Item = "none",
                Ids = new int[0],
                ActorNames = new string[0],
                FaceNodeIds = new int[0][],
                EdgeNodeIds = new int[0][]
            };
        }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "item")]
        public string Item { get; set; }

        [DataMember(Name = "ids")]
        public int[] Ids { get; set; }

        [DataMember(Name = "actorNames")]
        public string[] ActorNames { get; set; }

        [DataMember(Name = "faceNodeIds")]
        public int[][] FaceNodeIds { get; set; }

        [DataMember(Name = "edgeNodeIds")]
        public int[][] EdgeNodeIds { get; set; }
    }
}
