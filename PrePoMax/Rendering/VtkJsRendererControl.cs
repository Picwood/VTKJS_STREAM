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
    internal sealed class VtkJsRendererControl : UserControl
    {
        private const int MaximumSharedSelectionIds = 8000000;
        private const long MaximumSharedSelectionBytes = 64L * 1024 * 1024;
        private readonly WebView2 _webView;
        private readonly Label _status;
        private readonly VtkJsRendererHost _host;
        private readonly VtkJsSceneCapture _sceneCapture;
        private readonly Timer _publishTimer;
        private readonly Timer _annotationPublishTimer;
        private readonly Timer _explodedPreviewCompletionTimer;
        private readonly Timer _sceneAppliedCompletionTimer;
        private Uri _baseUri;
        private bool _started;
        private bool _publishing;
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
        private string _fieldName = "PrePoMax values";
        private string _fieldComponent = "value";
        private string _fieldUnits = "model-unit";
        private VtkJsSelectionStateMessage _selectionState = VtkJsSelectionStateMessage.Empty;
        private readonly Dictionary<string, VtkJsAnnotationMessage> _annotations =
            new Dictionary<string, VtkJsAnnotationMessage>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsActorExtremaState> _actorExtrema =
            new Dictionary<string, VtkJsActorExtremaState>(StringComparer.Ordinal);
        private readonly Dictionary<string, VtkJsExtremaAnnotationSettings> _extremaAnnotationSettings =
            new Dictionary<string, VtkJsExtremaAnnotationSettings>(StringComparer.Ordinal);
        private readonly Dictionary<string, double[]> _annotationOffsets =
            new Dictionary<string, double[]>(StringComparer.Ordinal);
        private bool _boxZoomEnabled;
        private VtkJsFollowerViewStateMessage _followerViewState = VtkJsFollowerViewStateMessage.Empty;
        private readonly Dictionary<string, double[]> _explodedPreviewOffsets =
            new Dictionary<string, double[]>(StringComparer.Ordinal);
        private VtkJsSectionViewStateMessage _sectionViewState = VtkJsSectionViewStateMessage.Disabled;
        private string _pendingExplodedPreviewRequestId;
        private Action _pendingExplodedPreviewCompletion;
        private Action _pendingSceneAppliedCompletion;

        public bool HasPublishedTopology { get; private set; }
        public bool IsBrowserReady { get; private set; }
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
            return _sceneCapture.GetActorNames();
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
                    if (args.IsSuccess)
                    {
                        PublishSelectionState();
                        PublishAnnotationState();
                        PublishBoxZoomState();
                        PublishFollowerViewState();
                        PublishExplodedViewState(false, 0);
                        PublishSectionViewState();
                    }
                    if (!args.IsSuccess) ShowError("The embedded renderer failed to load: " + args.WebErrorStatus + ".");
                };
                _webView.Source = new Uri(_baseUri, "/?autoload=1&embedded=1");
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
            _started = false;
            IsBrowserReady = false;
            _publishTimer.Stop();
            _annotationPublishTimer.Stop();
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
                if (actorName != null && _actorExtrema.TryGetValue(actorName, out VtkJsActorExtremaState state))
                    state.ColorContours = colorContours;
            RefreshExtremaAnnotations();
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

        public void CaptureActor(vtkControl.vtkMaxActorData actorData)
        {
            try
            {
                string diagnostic;
                bool captured = _sceneCapture.Capture(actorData, out diagnostic);
                WriteTrace(diagnostic);
                if (!captured) return;
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
                _topologyDirty = true;
                _frameDirty = false;
                HasPublishedTopology = false;
                _captureGeneration++;
                SchedulePublish();
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax surface could not be converted for VTK.js: " + exception.Message);
            }
        }

        public void RegisterAnimation(vtkControl.vtkMaxActorData actorData)
        {
            if (actorData == null || string.IsNullOrWhiteSpace(actorData.Name) || actorData.Geometry == null ||
                actorData.Geometry.NodesAnimation == null || actorData.Geometry.NodesAnimation.Length == 0) return;
            _animationSources[actorData.Name] = actorData;
            if (CaptureAnimationFrame(actorData, 0))
            {
                _topologyDirty = true;
                _frameDirty = false;
                HasPublishedTopology = false;
                _captureGeneration++;
                SchedulePublish();
            }
            WriteTrace("registered animation actor='" + actorData.Name + "' frames=" +
                       actorData.Geometry.NodesAnimation.Length);
        }

        public void SetAnimationFrame(int frameNumber)
        {
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

        public void SetAnimationFrameData(float[] time, int[] stepId, int[] stepIncrementId, float[] scale,
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

        public void ClearCapturedScene()
        {
            _sceneCapture.Clear();
            _animationSources.Clear();
            _animationTime = new float[0];
            _animationStepId = new int[0];
            _animationStepIncrementId = new int[0];
            _animationScale = new float[0];
            _actorExtrema.Clear();
            ClearAnnotations();
            _topologyDirty = true;
            _frameDirty = false;
            HasPublishedTopology = false;
            _pendingScalarActors.Clear();
            SetFieldMetadata("PrePoMax values", "value", "model-unit");
            _captureGeneration++;
            WriteTrace("cleared captured scene; generation=" + _captureGeneration);
            SchedulePublish();
        }

        public async void SetActorVisibility(string[] actorNames, bool visible)
        {
            if (actorNames == null || actorNames.Length == 0) return;
            foreach (string actorName in actorNames)
                if (actorName != null && _actorExtrema.TryGetValue(actorName, out VtkJsActorExtremaState state))
                    state.Visible = visible;
            RefreshExtremaAnnotations();
            if (!_started || IsDisposed) return;
            try
            {
                await _host.SetActorVisibilityAsync(actorNames, visible);
                WriteTrace("actor visibility visible=" + visible + " count=" + actorNames.Length);
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
                _sceneCapture.UpdateActor(oldName, newName, color);
                if (_pendingScalarActors.Remove(oldName)) _pendingScalarActors.Add(newName);
                if (_actorExtrema.TryGetValue(oldName, out VtkJsActorExtremaState extrema))
                {
                    _actorExtrema.Remove(oldName);
                    _actorExtrema[newName] = extrema;
                    RefreshExtremaAnnotations();
                }
                if (!_started || IsDisposed) return;
                await _host.UpdateActorAsync(oldName, newName, color.ToArgb());
                WriteTrace("actor presentation old='" + oldName + "' new='" + newName + "'");
            }
            catch (Exception exception)
            {
                ShowError("A PrePoMax actor presentation update could not be published to VTK.js: " + exception.Message);
            }
        }

        public async void RemoveActors(IEnumerable<string> actorNames)
        {
            try
            {
                if (actorNames == null) throw new ArgumentNullException("actorNames");
                string[] names = actorNames.Distinct(StringComparer.Ordinal).ToArray();
                if (names.Length == 0) return;
                _sceneCapture.RemoveActors(names);
                foreach (string name in names)
                {
                    _pendingScalarActors.Remove(name);
                    _actorExtrema.Remove(name);
                }
                RefreshExtremaAnnotations();
                if (!_started || IsDisposed) return;
                await _host.RemoveActorsAsync(names);
                WriteTrace("actor removal actors=" + names.Length);
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
                _host.Dispose();
                _publishTimer.Dispose();
                _annotationPublishTimer.Dispose();
                _explodedPreviewCompletionTimer.Dispose();
                _sceneAppliedCompletionTimer.Dispose();
                _webView.Dispose();
            }
            base.Dispose(disposing);
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs args)
        {
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
            if (message == null || message.Type != "prepomax.scene.applied-v1" || message.SceneRevision < 1)
            {
                WriteTrace("ignored malformed scene applied completion");
                return;
            }
            WriteTrace("browser scene applied revision=" + message.SceneRevision);
            CompletePendingSceneApplied("browser-acknowledged");
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
        [DataMember(Name = "sceneRevision")]
        public long SceneRevision { get; set; }
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
        public static readonly VtkJsSelectionStateMessage Empty = new VtkJsSelectionStateMessage
        {
            Type = "prepomax.selection.updated-v1",
            Item = "none",
            Ids = new int[0],
            ActorNames = new string[0],
            FaceNodeIds = new int[0][],
            EdgeNodeIds = new int[0][]
        };

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
