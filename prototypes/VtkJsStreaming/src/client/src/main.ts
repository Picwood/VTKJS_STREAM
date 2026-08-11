import './style.css';
import { ChunkLoader, type LoadProgress } from './chunkLoader';
import type {
  ActorPresentationUpdatedPayload, ActorRemovedPayload, ActorVisibilityUpdatedPayload, AreaCompletedPayload, CameraUpdatedPayload, FieldUpdatedPayload, FrameUpdatedPayload, FrustumCompletedPayload,
  HostAnnotation, HostAnnotationAction, HostAnnotationActionMessage, HostSelectionOperation, HostSelectionStateMessage, HostValidatedAreaMessage, HostValidatedPickMessage, PickCompletedPayload, ProtocolMessage,
  RenderActor, ResolvedPick, SceneManifest, SceneReplacedPayload,
} from './protocol';
import { RenderApi } from './renderApi';
import { normalizeHostAnnotations } from './hostAnnotations';
import { decodeHostSelection } from './hostSelectionBinary';
import { VtkSceneView, type CameraParameters, type CameraView } from './vtkSceneView';

const elements = {
  serverUrl: requireElement<HTMLInputElement>('server-url'),
  connect: requireElement<HTMLButtonElement>('connect'),
  cancel: requireElement<HTMLButtonElement>('cancel'),
  unload: requireElement<HTMLButtonElement>('unload'),
  resetCamera: requireElement<HTMLButtonElement>('reset-camera'),
  cameraView: requireElement<HTMLSelectElement>('camera-view'),
  orthographic: requireElement<HTMLInputElement>('orthographic'),
  viewport: requireElement<HTMLDivElement>('viewport'),
  selectionRectangle: requireElement<HTMLDivElement>('selection-rectangle'),
  pickMarker: requireElement<HTMLDivElement>('pick-marker'),
  pickInspector: requireElement<HTMLPreElement>('pick-inspector'),
  actors: requireElement<HTMLDivElement>('actors'),
  palette: requireElement<HTMLSelectElement>('palette'),
  contours: requireElement<HTMLInputElement>('contours'),
  switchField: requireElement<HTMLButtonElement>('switch-field'),
  legend: requireElement<HTMLDivElement>('legend'),
  progress: requireElement<HTMLProgressElement>('progress'),
  status: requireElement<HTMLOutputElement>('status'),
  diagnostics: requireElement<HTMLPreElement>('diagnostics'),
  error: requireElement<HTMLDivElement>('error'),
  copyDiagnostics: requireElement<HTMLButtonElement>('copy-diagnostics'),
  downloadDiagnostics: requireElement<HTMLButtonElement>('download-diagnostics'),
};

const annotationInteractions = {
  edited: notifyAnnotationEdited,
  moved: notifyAnnotationMoved,
  action: notifyAnnotationAction,
};
let view = new VtkSceneView(elements.viewport, showError, annotationInteractions, notifyCameraChanged);
view.setParallelProjection(elements.orthographic.checked);
let hostSelection: HostSelectionStateMessage = {
  type: 'prepomax.selection.updated-v1',
  item: 'none',
  ids: [],
  actorNames: [],
  faceNodeIds: [],
  edgeNodeIds: [],
};
let hostAnnotations: readonly HostAnnotation[] = [];
let hostFollowerView: { actorNames: readonly string[]; nodeIds: readonly number[] } = { actorNames: [], nodeIds: [] };
let hostExplodedPreview = new Map<string, readonly [number, number, number]>();
let hostSectionView: { enabled: boolean; point: readonly [number, number, number];
  normal: readonly [number, number, number]; lightenColors: boolean;
  sectionColor: readonly [number, number, number, number] } = {
    enabled: false, point: [0, 0, 0], normal: [1, 0, 0], lightenColors: true, sectionColor: [0, 0, 0, 0],
  };
let activeLoad: AbortController | null = null;
let manifest: SceneManifest | null = null;
let firstGeometryAt: number | null = null;
let loadStartedAt: number | null = null;
let latestProgress: LoadProgress | null = null;
let currentApi: RenderApi | null = null;
let eventSocket: WebSocket | null = null;
let fieldUpdateInProgress = false;
let scalarUpdateBytes = 0;
let frameUpdateBytes = 0;
let frameActivationSamples: FrameActivationSample[] = [];
let pickInProgress = false;
let pickLatencies: number[] = [];
let activePick: AbortController | null = null;
let activeSection: AbortController | null = null;
let sectionGeneration = 0;
let areaDrag: { pointerId: number; startX: number; startY: number; operation: HostSelectionOperation;
  mode: 'area' | 'box-zoom' } | null = null;
let boxZoomEnabled = false;
let suppressNextClick = false;
let sceneGeneration = 0;
const resourceConcurrency = readResourceConcurrency();
const longTasks: number[] = [];
let manifestReceivedAt: number | null = null;
let allChunksLoadedAt: number | null = null;
let firstStableFrameAt: number | null = null;
let scalarActivationMs: number | null = null;
let fieldUpdateStartedAt: number | null = null;
const launchParameters = new URLSearchParams(window.location.search);
const embeddedWebView = (window as unknown as {
  chrome?: { webview?: {
    addEventListener?(type: string, listener: (event: {
      data?: unknown; additionalData?: unknown; getBuffer?(): ArrayBuffer;
    }) => void): void;
    releaseBuffer?(buffer: ArrayBuffer): void;
    postMessage?(message: string): void;
  } };
}).chrome?.webview;

const requestedServer = launchParameters.get('server');
if (requestedServer) {
  elements.serverUrl.value = requestedServer;
} else if (launchParameters.get('embedded') === '1') {
  elements.serverUrl.value = window.location.origin;
}
if (launchParameters.get('embedded') === '1') {
  document.documentElement.dataset.embedded = 'true';
}

if (PerformanceObserver.supportedEntryTypes.includes('longtask')) {
  new PerformanceObserver((list) => {
    for (const entry of list.getEntries()) {
      longTasks.push(entry.duration);
    }
  }).observe({ entryTypes: ['longtask'] });
}

elements.connect.addEventListener('click', () => void loadScene());
elements.cancel.addEventListener('click', () => activeLoad?.abort(new DOMException('Load cancelled.', 'AbortError')));
elements.unload.addEventListener('click', unloadScene);
elements.resetCamera.addEventListener('click', () => view.resetCamera());
elements.cameraView.addEventListener('change', () => view.setCameraView(elements.cameraView.value as CameraView));
elements.orthographic.addEventListener('change', () => view.setParallelProjection(elements.orthographic.checked));
elements.contours.addEventListener('change', () => view.setContoursVisible(elements.contours.checked));
elements.palette.addEventListener('change', () => {
  view.setColorMap(elements.palette.value);
  document.querySelector('.color-ramp')?.classList.toggle('rainbow', elements.palette.value.includes('Rainbow'));
});
elements.copyDiagnostics.addEventListener('click', () => void navigator.clipboard.writeText(elements.diagnostics.textContent ?? ''));
elements.downloadDiagnostics.addEventListener('click', downloadDiagnostics);
elements.switchField.addEventListener('click', () => void switchField());
elements.viewport.addEventListener('click', (event) => {
  if (suppressNextClick) { suppressNextClick = false; return; }
  void resolvePick(event);
});
elements.viewport.addEventListener('pointerdown', beginAreaDrag, true);
elements.viewport.addEventListener('pointermove', updateAreaDrag, true);
elements.viewport.addEventListener('pointerup', endAreaDrag, true);
elements.viewport.addEventListener('pointercancel', cancelAreaDrag, true);
window.addEventListener('keydown', (event) => {
  if (event.key === 'Escape' && boxZoomEnabled) {
    areaDrag = null;
    elements.selectionRectangle.hidden = true;
    finishBoxZoom(false);
    event.preventDefault();
  }
});
embeddedWebView?.addEventListener?.('message', (event) => applyHostMessage(event.data));
embeddedWebView?.addEventListener?.('sharedbufferreceived', (event) => {
  const buffer = event.getBuffer?.();
  if (!buffer) { showError('The embedded host sent an invalid shared selection.'); return; }
  try {
    const metadata = event.additionalData as { type?: unknown; byteLength?: unknown } | null;
    if (!metadata || metadata.type !== 'prepomax.selection.shared-v1'
      || metadata.byteLength !== buffer.byteLength) throw new Error('The shared selection metadata is invalid.');
    applyHostSelection(decodeHostSelection(buffer), true);
  } catch (error) {
    showError(error instanceof Error ? error.message : String(error));
  } finally {
    embeddedWebView.releaseBuffer?.(buffer);
  }
});
window.addEventListener('beforeunload', () => {
  activeLoad?.abort();
  activePick?.abort();
  activeSection?.abort();
  eventSocket?.close();
  view.delete();
});

if (launchParameters.get('autoload') === '1') {
  void loadScene();
}

async function loadScene(): Promise<void> {
  const followerBaseline = view.getFollowerPoints();
  activeLoad?.abort();
  activePick?.abort();
  activeSection?.abort();
  activePick = null;
  activeSection = null;
  sectionGeneration += 1;
  sceneGeneration += 1;
  eventSocket?.close();
  eventSocket = null;
  currentApi = null;
  activeLoad = new AbortController();
  const { signal } = activeLoad;
  clearError();
  view.delete();
  elements.viewport.replaceChildren();
  view = new VtkSceneView(elements.viewport, showError, annotationInteractions, notifyCameraChanged);
  view.setParallelProjection(elements.orthographic.checked);
  view.setSelection(hostSelection.item, hostSelection.ids, hostSelection.actorNames,
    hostSelection.faceNodeIds, hostSelection.edgeNodeIds);
  view.setAnnotations(hostAnnotations);
  view.setFollowerView(hostFollowerView.actorNames, hostFollowerView.nodeIds, followerBaseline);
  void view.setExplodedViewPreview(hostExplodedPreview, false, 0);
  manifest = null;
  firstGeometryAt = null;
  loadStartedAt = performance.now();
  latestProgress = null;
  scalarUpdateBytes = 0;
  frameUpdateBytes = 0;
  frameActivationSamples = [];
  scalarActivationMs = null;
  fieldUpdateStartedAt = null;
  manifestReceivedAt = null;
  allChunksLoadedAt = null;
  firstStableFrameAt = null;
  longTasks.length = 0;
  pickLatencies = [];
  clearPick('No selection');
  elements.unload.disabled = true;
  setLoading(true);
  setStatus('Creating authenticated local session…');

  try {
    const api = new RenderApi(elements.serverUrl.value);
    currentApi = api;
    const session = await api.createSession(signal);
    setStatus('Fetching scene manifest…');
    manifest = await api.getManifest(session.sceneId, signal);
    manifestReceivedAt = performance.now();
    view.setWorldOrigin(manifest.worldOrigin);
    if (hostSectionView.enabled) view.setSectionView(hostSectionView.point, hostSectionView.normal);
    let socket: WebSocket;
    socket = api.connectEvents(
      session.sceneId,
      (message) => void handleSceneEvent(message),
      () => { elements.switchField.disabled = false; },
      showError,
      () => {
        if (eventSocket === socket && manifest) {
          eventSocket = null;
          setStatus('Event stream disconnected; reloading scene snapshot…');
          void loadScene();
        }
      },
    );
    eventSocket = socket;
    view.setField(manifest.field);
    renderActorControls(manifest.actors);
    renderLegend(manifest);

    const loader = new ChunkLoader(api, resourceConcurrency);
    await loader.load(manifest, {
      onGeometry: (geometry) => {
        view.addGeometry(geometry);
        if (firstGeometryAt === null) {
          firstGeometryAt = performance.now();
          view.resetCamera();
          elements.resetCamera.disabled = false;
          elements.cameraView.disabled = false;
        }
        updateDiagnostics();
      },
      onAttributes: (attributes) => {
        view.addAttributes(attributes);
        updateDiagnostics();
      },
      onProgress: updateProgress,
    }, signal);
    allChunksLoadedAt = performance.now();
    if (hostSectionView.enabled) await resolveSectionGeometry();
    await nextStableFrame();
    firstStableFrameAt = performance.now();
    notifyCameraChanged(view.getCameraParameters());
    updateDiagnostics();
    setStatus(`Loaded ${manifest.actors.length} actors.`);
    elements.unload.disabled = false;
  } catch (error) {
    if (signal.aborted) {
      setStatus('Load cancelled.');
    } else {
      showError(error instanceof Error ? error.message : String(error));
      setStatus('Load failed.');
    }
  } finally {
    if (activeLoad?.signal === signal) {
      activeLoad = null;
      setLoading(false);
    }
  }
}

function updateProgress(progress: LoadProgress): void {
  latestProgress = progress;
  elements.progress.max = progress.totalResources;
  elements.progress.value = progress.completedResources;
  setStatus(
    `Resources ${progress.completedResources}/${progress.totalResources} · ${formatBytes(progress.transferredBytes)}/${formatBytes(progress.totalBytes)}`,
  );
  updateDiagnostics(progress);
}

function renderActorControls(actors: readonly RenderActor[]): void {
  elements.actors.replaceChildren(...actors.map((actor) => {
    const row = document.createElement('div');
    row.className = 'actor-control';
    const visibilityLabel = document.createElement('label');
    visibilityLabel.className = 'actor-row';
    const visibility = document.createElement('input');
    visibility.type = 'checkbox';
    visibility.checked = actor.visible;
    visibility.setAttribute('aria-label', actor.name);
    visibility.addEventListener('change', () => view.setActorVisibility(actor.actorId, visibility.checked));
    visibilityLabel.append(visibility, document.createTextNode(actor.name));
    const pickableLabel = document.createElement('label');
    pickableLabel.className = 'pickable-control';
    const pickable = document.createElement('input');
    pickable.type = 'checkbox';
    pickable.checked = actor.pickable;
    pickable.setAttribute('aria-label', `${actor.name} pickable`);
    pickable.addEventListener('change', () => view.setActorPickable(actor.actorId, pickable.checked));
    pickableLabel.append(pickable, document.createTextNode('Pick'));
    row.append(visibilityLabel, pickableLabel);
    return row;
  }));
}

function renderLegend(scene: SceneManifest): void {
  const [minimum, maximum] = scene.field.displayedRange;
  elements.legend.className = 'legend';
  elements.legend.replaceChildren();
  const title = document.createElement('strong');
  title.textContent = `${scene.field.name} · ${scene.field.component}`;
  const ramp = document.createElement('div');
  ramp.className = 'color-ramp';
  const values = document.createElement('div');
  values.className = 'legend-values';
  for (const value of [minimum.toPrecision(4), scene.field.units, maximum.toPrecision(4)]) {
    const span = document.createElement('span');
    span.textContent = value;
    values.append(span);
  }
  elements.legend.append(title, ramp, values);
}

function updateDiagnostics(progress: LoadProgress | null = latestProgress): void {
  const now = performance.now();
  const diagnostics = {
    protocol: '1.0',
    sceneId: manifest?.sceneId ?? null,
    sceneRevision: manifest?.sceneRevision ?? null,
    actors: manifest?.actors.length ?? 0,
    chunks: manifest?.actors.reduce((sum, actor) => sum + actor.chunks.length, 0) ?? 0,
    resources: manifest?.resources.length ?? 0,
    resourceConcurrency,
    transferredBytes: progress?.transferredBytes ?? null,
    firstGeometryMs: firstGeometryAt !== null && loadStartedAt !== null ? Math.round(firstGeometryAt - loadStartedAt) : null,
    elapsedMs: loadStartedAt !== null ? Math.round(now - loadStartedAt) : null,
    devicePixelRatio: window.devicePixelRatio,
    scalarUpdateBytes,
    frameUpdateBytes,
    frameActivation: {
      count: frameActivationSamples.length,
      latest: frameActivationSamples.at(-1) ?? null,
      samples: frameActivationSamples,
    },
    timing: {
      manifestMs: manifestReceivedAt !== null && loadStartedAt !== null ? roundMetric(manifestReceivedAt - loadStartedAt) : null,
      firstVisibleChunkMs: firstGeometryAt !== null && manifestReceivedAt !== null ? roundMetric(firstGeometryAt - manifestReceivedAt) : null,
      completeLoadMs: allChunksLoadedAt !== null && manifestReceivedAt !== null ? roundMetric(allChunksLoadedAt - manifestReceivedAt) : null,
      firstStableFrameMs: firstStableFrameAt !== null && manifestReceivedAt !== null ? roundMetric(firstStableFrameAt - manifestReceivedAt) : null,
      longestTaskMs: longTasks.length > 0 ? roundMetric(Math.max(...longTasks)) : 0,
      scalarActivationMs,
    },
    browserMemory: readBrowserMemory(),
    picks: {
      count: pickLatencies.length,
      lastMs: pickLatencies.length > 0 ? roundMetric(pickLatencies.at(-1)!) : null,
      p50Ms: percentile(pickLatencies, 0.5),
      p95Ms: percentile(pickLatencies, 0.95),
    },
    renderer: view.getDiagnostics(),
  };
  elements.diagnostics.textContent = JSON.stringify(diagnostics, null, 2);
}

async function resolvePick(event: MouseEvent): Promise<void> {
  if (!currentApi || !manifest || pickInProgress) {
    return;
  }
  const api = currentApi;
  const scene = manifest;
  const generation = sceneGeneration;
  const controller = new AbortController();
  activePick = controller;
  pickInProgress = true;
  const startedAt = performance.now();
  const selectionOperation = getSelectionOperation(event);
  try {
    const candidate = await view.pick(event.clientX, event.clientY);
    if (generation !== sceneGeneration || controller.signal.aborted) {
      return;
    }
    if (!candidate) {
      clearPick('No rendered primitive at this point');
      setStatus('Pick missed.');
      return;
    }
    const response = await api.sendCommand<PickCompletedPayload>(
      scene.sceneId,
      scene.sceneRevision,
      'pick.resolve',
      candidate,
      controller.signal,
    );
    if (generation !== sceneGeneration || controller.signal.aborted) {
      return;
    }
    if (response.type !== 'command.completed' || response.payload.commandType !== 'pick.resolve') {
      throw new Error('The server returned an unexpected pick response.');
    }
    showPick(response.payload.result);
    const actorName = scene.actors.find((actor) => actor.actorId === response.payload.result.actorId)?.name;
    if (actorName) notifyPrePoMaxHost(response.payload.result, actorName, selectionOperation);
    setStatus(`Selected element ${response.payload.result.sourceElementId}, node ${response.payload.result.candidateSourceNodeId}.`);
  } catch (error) {
    if (controller.signal.aborted) {
      return;
    }
    clearPick('Selection failed');
    showError(error instanceof Error ? error.message : String(error));
    setStatus('Pick failed.');
  } finally {
    if (generation === sceneGeneration && !controller.signal.aborted) {
      pickLatencies.push(performance.now() - startedAt);
      updateDiagnostics();
    }
    pickInProgress = false;
    if (activePick === controller) {
      activePick = null;
    }
  }
}

function getSelectionOperation(event: MouseEvent): HostSelectionOperation {
  if (event.shiftKey && event.ctrlKey) return 'intersect';
  if (event.shiftKey) return 'add';
  if (event.ctrlKey) return 'subtract';
  return 'replace';
}

function notifyPrePoMaxHost(pick: ResolvedPick, actorName: string, operation: HostSelectionOperation): void {
  const message: HostValidatedPickMessage = {
    type: 'prepomax.pick.validated-v1',
    validated: true,
    sourceElementId: pick.sourceElementId,
    sourceNodeId: pick.candidateSourceNodeId,
    triangleSourceNodeIds: pick.triangleSourceNodeIds,
    worldPosition: pick.worldPosition,
    cameraDirection: pick.cameraDirection,
    selectionPrecision: pick.selectionPrecision,
    actorName,
    operation,
  };
  const hostWindow = window as unknown as {
    chrome?: { webview?: { postMessage(value: string): void } };
  };
  hostWindow.chrome?.webview?.postMessage(JSON.stringify(message));
}

function beginAreaDrag(event: PointerEvent): void {
  if ((!boxZoomEnabled && !event.altKey) || event.button !== 0 || areaDrag || !manifest) return;
  areaDrag = { pointerId: event.pointerId, startX: event.clientX, startY: event.clientY,
    operation: getSelectionOperation(event), mode: boxZoomEnabled ? 'box-zoom' : 'area' };
  try { elements.viewport.setPointerCapture(event.pointerId); } catch { /* Synthetic acceptance events have no active pointer. */ }
  elements.selectionRectangle.hidden = false;
  updateSelectionRectangle(event.clientX, event.clientY);
  event.preventDefault();
  event.stopImmediatePropagation();
}

function updateAreaDrag(event: PointerEvent): void {
  if (!areaDrag || event.pointerId !== areaDrag.pointerId) return;
  updateSelectionRectangle(event.clientX, event.clientY);
  event.preventDefault();
  event.stopImmediatePropagation();
}

function endAreaDrag(event: PointerEvent): void {
  if (!areaDrag || event.pointerId !== areaDrag.pointerId) return;
  const drag = areaDrag;
  areaDrag = null;
  elements.selectionRectangle.hidden = true;
  if (elements.viewport.hasPointerCapture(event.pointerId)) elements.viewport.releasePointerCapture(event.pointerId);
  const moved = Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY) >= 6;
  suppressNextClick = true;
  event.preventDefault();
  event.stopImmediatePropagation();
  if (drag.mode === 'box-zoom') finishBoxZoom(moved && view.boxZoom(
    drag.startX, drag.startY, event.clientX, event.clientY));
  else if (moved) void resolveArea(drag.startX, drag.startY, event.clientX, event.clientY, drag.operation);
}

function cancelAreaDrag(event: PointerEvent): void {
  if (!areaDrag || event.pointerId !== areaDrag.pointerId) return;
  areaDrag = null;
  elements.selectionRectangle.hidden = true;
  if (boxZoomEnabled) finishBoxZoom(false);
  event.preventDefault();
  event.stopImmediatePropagation();
}

function updateSelectionRectangle(clientX: number, clientY: number): void {
  if (!areaDrag) return;
  const bounds = elements.viewport.getBoundingClientRect();
  const left = Math.min(areaDrag.startX, clientX) - bounds.left;
  const top = Math.min(areaDrag.startY, clientY) - bounds.top;
  elements.selectionRectangle.style.left = `${left}px`;
  elements.selectionRectangle.style.top = `${top}px`;
  elements.selectionRectangle.style.width = `${Math.abs(clientX - areaDrag.startX)}px`;
  elements.selectionRectangle.style.height = `${Math.abs(clientY - areaDrag.startY)}px`;
  elements.selectionRectangle.classList.toggle('crossing', areaDrag.mode === 'area' && clientX < areaDrag.startX);
}

function finishBoxZoom(applied: boolean): void {
  boxZoomEnabled = false;
  elements.viewport.dataset.boxZoom = 'false';
  notifyHost({ type: 'prepomax.box-zoom.ended-v1', applied });
  setStatus(applied ? 'Box zoom applied.' : 'Box zoom cancelled.');
}

async function resolveArea(startX: number, startY: number, endX: number, endY: number,
  operation: HostSelectionOperation): Promise<void> {
  if (!currentApi || !manifest || pickInProgress) return;
  if (view.hasExplodedPreview()) {
    setStatus('Rectangle selection is unavailable during an exploded-view preview.');
    return;
  }
  const api = currentApi;
  const scene = manifest;
  const generation = sceneGeneration;
  const controller = new AbortController();
  activePick = controller;
  pickInProgress = true;
  try {
    const completelyInside = endX > startX;
    const frustum = view.createFrustum(startX, startY, endX, endY, completelyInside);
    try {
      const frustumResponse = await api.sendCommand<FrustumCompletedPayload>(scene.sceneId, scene.sceneRevision,
        'frustum.resolve', frustum, controller.signal);
      if (frustumResponse.type !== 'command.completed' || frustumResponse.payload.commandType !== 'frustum.resolve')
        throw new Error('The server returned an unexpected frustum response.');
      if (generation !== sceneGeneration || controller.signal.aborted) return;
      const result = frustumResponse.payload.result;
      const message: HostValidatedAreaMessage = {
        type: 'prepomax.area.validated-v1', validated: true, triangles: [], cameraDirection: result.cameraDirection,
        completelyInside: result.completelyInside, operation, locatorValidated: true,
        sourceNodeIds: result.sourceNodeIds, sourceElementIds: result.sourceElementIds, actorNames: result.actorNames,
      };
      const hostWindow = window as unknown as { chrome?: { webview?: { postMessage(value: string): void } } };
      hostWindow.chrome?.webview?.postMessage(JSON.stringify(message));
      setStatus(`Frustum selected ${result.sourceNodeIds.length} nodes and ${result.sourceElementIds.length} elements.`);
      return;
    } catch (error) {
      if ((error as { code?: string }).code !== 'frustum-unavailable') throw error;
    }
    const candidate = await view.selectArea(startX, startY, endX, endY, completelyInside);
    if (generation !== sceneGeneration || controller.signal.aborted) return;
    const response = await api.sendCommand<AreaCompletedPayload>(scene.sceneId, scene.sceneRevision,
      'area.resolve', candidate, controller.signal);
    if (response.type !== 'command.completed' || response.payload.commandType !== 'area.resolve')
      throw new Error('The server returned an unexpected area response.');
    const message: HostValidatedAreaMessage = {
      type: 'prepomax.area.validated-v1', validated: true, triangles: response.payload.result.triangles,
      cameraDirection: response.payload.result.cameraDirection,
      completelyInside: response.payload.result.completelyInside, operation,
    };
    const hostWindow = window as unknown as { chrome?: { webview?: { postMessage(value: string): void } } };
    hostWindow.chrome?.webview?.postMessage(JSON.stringify(message));
    setStatus(`Area selected ${response.payload.result.triangles.length} rendered triangles (${completelyInside ? 'inside' : 'crossing'}).`);
  } catch (error) {
    if (!controller.signal.aborted) {
      showError(error instanceof Error ? error.message : String(error));
      setStatus('Area selection failed.');
    }
  } finally {
    pickInProgress = false;
    if (activePick === controller) activePick = null;
  }
}

function applyHostMessage(value: unknown): void {
  const type = (value as { type?: unknown } | null)?.type;
  if (type === 'prepomax.selection.updated-v1') applyHostSelection(value);
  else if (type === 'prepomax.annotations.updated-v1') applyHostAnnotations(value);
  else if (type === 'prepomax.box-zoom.mode-v1') applyBoxZoomMode(value);
  else if (type === 'prepomax.follower-view.updated-v1') applyFollowerView(value);
  else if (type === 'prepomax.exploded-view.preview-v1') void applyExplodedViewPreview(value);
  else if (type === 'prepomax.section-view.updated-v1') void applySectionView(value);
}

async function applySectionView(value: unknown): Promise<void> {
  const message = value as { type?: unknown; enabled?: unknown; point?: unknown; normal?: unknown;
    lightenColors?: unknown; sectionColor?: unknown } | null;
  const validVector = (candidate: unknown, nonzero: boolean) => Array.isArray(candidate) && candidate.length === 3
    && candidate.every((component) => typeof component === 'number' && Number.isFinite(component)
      && Math.abs(component) <= 1e150)
    && (!nonzero || candidate.some((component) => Math.abs(component as number) > 1e-12));
  if (!message || message.type !== 'prepomax.section-view.updated-v1' || typeof message.enabled !== 'boolean'
    || !validVector(message.point, false) || !validVector(message.normal, true)
    || typeof message.lightenColors !== 'boolean' || !Array.isArray(message.sectionColor)
    || message.sectionColor.length !== 4 || message.sectionColor.some((component) => typeof component !== 'number'
      || !Number.isFinite(component) || component < 0 || component > 1)) {
    showError('The embedded host sent an invalid section-view state.');
    return;
  }
  hostSectionView = {
    enabled: message.enabled,
    point: [...message.point as number[]] as [number, number, number],
    normal: [...message.normal as number[]] as [number, number, number],
    lightenColors: message.lightenColors,
    sectionColor: [...message.sectionColor] as [number, number, number, number],
  };
  if (message.enabled) {
    if (manifest) view.setSectionView(hostSectionView.point, hostSectionView.normal);
    await resolveSectionGeometry();
  } else {
    sectionGeneration += 1;
    activeSection?.abort();
    activeSection = null;
    view.removeSectionGeometry();
    view.removeSectionView();
    notifySectionApplied(0);
  }
  updateDiagnostics();
}

async function resolveSectionGeometry(): Promise<void> {
  if (!hostSectionView.enabled || !manifest || !currentApi) return;
  activeSection?.abort();
  const controller = new AbortController();
  activeSection = controller;
  const generation = ++sectionGeneration;
  const sceneId = manifest.sceneId;
  try {
    const result = await currentApi.resolveSection(sceneId, {
      point: hostSectionView.point,
      normal: hostSectionView.normal,
      lightenColors: hostSectionView.lightenColors,
      sectionColor: hostSectionView.sectionColor,
      actorOffsets: [...hostExplodedPreview].map(([actorName, offset]) => ({ actorName, offset })),
    }, controller.signal);
    if (controller.signal.aborted || generation !== sectionGeneration || manifest?.sceneId !== sceneId) return;
    view.replaceSectionGeometry(result.actors);
    notifySectionApplied(result.actors.length, result.skippedCells);
    updateDiagnostics();
    if (result.skippedCells > 0) setStatus(`Section applied; ${result.skippedCells} unsupported cells were skipped.`);
  } catch (error) {
    if (controller.signal.aborted) return;
    view.removeSectionGeometry();
    showError(error instanceof Error ? error.message : String(error));
    notifySectionApplied(0);
  } finally {
    if (activeSection === controller) activeSection = null;
  }
}

function notifySectionApplied(capActors: number, skippedCells = 0): void {
  notifyHost({ type: 'prepomax.section-view.applied-v1', enabled: hostSectionView.enabled,
    clippedChunks: hostSectionView.enabled && manifest
      ? manifest.actors.reduce((sum, actor) => sum + actor.chunks.length, 0) : 0,
    capActors, skippedCells });
}

async function applyExplodedViewPreview(value: unknown): Promise<void> {
  const message = value as { type?: unknown; offsets?: unknown; animate?: unknown; durationMs?: unknown;
    requestId?: unknown } | null;
  const offsets = message?.offsets;
  if (!message || message.type !== 'prepomax.exploded-view.preview-v1' || !Array.isArray(offsets)
    || offsets.length > 512 || typeof message.animate !== 'boolean'
    || !Number.isInteger(message.durationMs) || (message.durationMs as number) < 0 || (message.durationMs as number) > 60_000
    || (message.requestId !== undefined && (typeof message.requestId !== 'string'
      || !/^[0-9a-f]{32}$/i.test(message.requestId)))
    || offsets.some((entry) => {
      const item = entry as { actorName?: unknown; offset?: unknown } | null;
      return !item || typeof item.actorName !== 'string' || item.actorName.length === 0 || item.actorName.length > 1024
        || !Array.isArray(item.offset) || item.offset.length !== 3
        || item.offset.some((component) => typeof component !== 'number' || !Number.isFinite(component)
          || Math.abs(component) > 1e150);
    })) {
    showError('The embedded host sent an invalid exploded-view preview.');
    return;
  }
  const update = new Map<string, readonly [number, number, number]>();
  for (const entry of offsets as Array<{ actorName: string; offset: [number, number, number] }>) {
    if (update.has(entry.actorName)) {
      showError('The embedded host sent duplicate exploded-view actors.');
      return;
    }
    update.set(entry.actorName, entry.offset);
    if (entry.offset.every((component) => Math.abs(component) <= 1e-12)) hostExplodedPreview.delete(entry.actorName);
    else hostExplodedPreview.set(entry.actorName, [...entry.offset]);
  }
  const applied = await view.setExplodedViewPreview(update, message.animate, message.durationMs as number);
  if (applied) notifyHost({ type: 'prepomax.exploded-view.applied-v1', actorCount: update.size,
    animated: message.animate, ...(message.requestId === undefined ? {} : { requestId: message.requestId }) });
  if (applied && hostSectionView.enabled) await resolveSectionGeometry();
  updateDiagnostics();
}

function applyFollowerView(value: unknown): void {
  const message = value as { type?: unknown; actorNames?: unknown; nodeIds?: unknown } | null;
  if (!message || message.type !== 'prepomax.follower-view.updated-v1'
    || !Array.isArray(message.actorNames) || !Array.isArray(message.nodeIds)
    || message.actorNames.length !== message.nodeIds.length
    || ![0, 1, 3].includes(message.nodeIds.length)
    || message.actorNames.some((name) => typeof name !== 'string' || name.length === 0 || name.length > 1024)
    || message.nodeIds.some((id) => !Number.isInteger(id))) {
    showError('The embedded host sent an invalid follower-view state.');
    return;
  }
  hostFollowerView = { actorNames: [...message.actorNames] as string[], nodeIds: [...message.nodeIds] as number[] };
  view.setFollowerView(hostFollowerView.actorNames, hostFollowerView.nodeIds);
}

function applyBoxZoomMode(value: unknown): void {
  const message = value as { type?: unknown; enabled?: unknown } | null;
  if (!message || message.type !== 'prepomax.box-zoom.mode-v1' || typeof message.enabled !== 'boolean') {
    showError('The embedded host sent an invalid box-zoom mode.');
    return;
  }
  boxZoomEnabled = message.enabled;
  if (!boxZoomEnabled) {
    areaDrag = null;
    elements.selectionRectangle.hidden = true;
  }
  elements.viewport.dataset.boxZoom = boxZoomEnabled ? 'true' : 'false';
}

function applyHostSelection(value: unknown, allowLarge = false): void {
  const message = value as Partial<HostSelectionStateMessage> | null;
  const maximumIds = allowLarge ? 8_000_000 : 100_000;
  const maximumCells = allowLarge ? 250_000 : 100_000;
  const maximumFaceNodes = allowLarge ? 2_000_000 : 1_000_000;
  const maximumEdgeNodes = allowLarge ? 750_000 : 300_000;
  const supportedItems = new Set(['none', 'node', 'element', 'surface', 'geometry-edge', 'geometry', 'part', 'unsupported']);
  if (!message || message.type !== 'prepomax.selection.updated-v1'
    || !message.item || !supportedItems.has(message.item)
    || !Array.isArray(message.ids) || message.ids.length > maximumIds
    || message.ids.some((id) => !Number.isInteger(id))
    || !Array.isArray(message.actorNames) || message.actorNames.length > 4096
    || message.actorNames.some((name) => typeof name !== 'string' || name.length === 0 || name.length > 1024)
    || !Array.isArray(message.faceNodeIds) || message.faceNodeIds.length > maximumCells
    || message.faceNodeIds.some((face) => !Array.isArray(face) || face.length < 3 || face.length > 32
      || face.some((id) => !Number.isInteger(id)))
    || message.faceNodeIds.reduce((count, face) => count + face.length, 0) > maximumFaceNodes
    || !Array.isArray(message.edgeNodeIds) || message.edgeNodeIds.length > maximumCells
    || message.edgeNodeIds.some((edge) => !Array.isArray(edge) || (edge.length !== 2 && edge.length !== 3)
      || edge.some((id) => !Number.isInteger(id)))
    || message.edgeNodeIds.reduce((count, edge) => count + edge.length, 0) > maximumEdgeNodes) {
    showError('The embedded host sent an invalid selection update.');
    return;
  }
  hostSelection = {
    type: 'prepomax.selection.updated-v1',
    item: message.item,
    ids: [...message.ids],
    actorNames: [...message.actorNames],
    faceNodeIds: message.faceNodeIds.map((face) => [...face]),
    edgeNodeIds: message.edgeNodeIds.map((edge) => [...edge]),
  };
  view.setSelection(hostSelection.item, hostSelection.ids, hostSelection.actorNames,
    hostSelection.faceNodeIds, hostSelection.edgeNodeIds);
  updateDiagnostics();
}

function applyHostAnnotations(value: unknown): void {
  try { hostAnnotations = normalizeHostAnnotations(value); }
  catch (error) { showError(error instanceof Error ? error.message : String(error)); return; }
  view.setAnnotations(hostAnnotations);
  updateDiagnostics();
}

function notifyAnnotationEdited(name: string, text: string): void {
  notifyHost({ type: 'prepomax.annotation.edited-v1', name, text });
}

function notifyAnnotationMoved(name: string, offset: readonly [number, number]): void {
  notifyHost({ type: 'prepomax.annotation.moved-v1', name, offset });
}

function notifyAnnotationAction(name: string, action: HostAnnotationAction): void {
  const message: HostAnnotationActionMessage = { type: 'prepomax.annotation.action-v1', name, action };
  notifyHost(message);
}

function notifyCameraChanged(camera: CameraParameters): void {
  notifyHost({ type: 'prepomax.camera.changed-v1', ...camera });
}

function notifyHost(message: unknown): void {
  const hostWindow = window as unknown as { chrome?: { webview?: { postMessage(value: string): void } } };
  hostWindow.chrome?.webview?.postMessage(JSON.stringify(message));
}

function showPick(pick: ResolvedPick): void {
  elements.pickMarker.hidden = false;
  elements.pickMarker.style.left = `${pick.displayPosition[0]}px`;
  elements.pickMarker.style.top = `${pick.displayPosition[1]}px`;
  elements.pickInspector.textContent = JSON.stringify({
    actorId: pick.actorId,
    chunkId: pick.chunkId,
    triangle: pick.primitiveIndex,
    sourceElementId: pick.sourceElementId,
    candidateSourceNodeId: pick.candidateSourceNodeId,
    worldPosition: pick.worldPosition.map((value) => Number(value.toFixed(7))),
    validated: pick.validated,
  }, null, 2);
}

function clearPick(message: string): void {
  elements.pickMarker.hidden = true;
  elements.pickInspector.textContent = message;
}

function percentile(values: readonly number[], fraction: number): number | null {
  if (values.length === 0) {
    return null;
  }
  const sorted = [...values].sort((left, right) => left - right);
  const index = Math.ceil(fraction * sorted.length) - 1;
  return roundMetric(sorted[Math.max(0, index)]!);
}

function roundMetric(value: number): number {
  return Math.round(value * 10) / 10;
}

function setLoading(loading: boolean): void {
  elements.connect.disabled = loading;
  elements.cancel.disabled = !loading;
  elements.serverUrl.disabled = loading;
}

async function switchField(): Promise<void> {
  if (!currentApi || !manifest || fieldUpdateInProgress) {
    return;
  }
  fieldUpdateInProgress = true;
  fieldUpdateStartedAt = performance.now();
  elements.switchField.disabled = true;
  setStatus('Requesting scalar field update…');
  try {
    await currentApi.sendCommand(manifest.sceneId, manifest.sceneRevision, 'field.toggle');
  } catch (error) {
    fieldUpdateInProgress = false;
    elements.switchField.disabled = eventSocket?.readyState === WebSocket.OPEN ? false : true;
    showError(error instanceof Error ? error.message : String(error));
    setStatus('Scalar update failed.');
  }
}

async function handleSceneEvent(message: ProtocolMessage): Promise<void> {
  if (message.type === 'scene.replaced') {
    const payload = message.payload as SceneReplacedPayload;
    hostExplodedPreview.clear();
    view.cancelExplodedViewPreview();
    setStatus('PrePoMax published a replacement scene; loading its snapshot…');
    await loadScene();
    if (!manifest || manifest.sceneId !== payload.sceneId) {
      showError('The replacement scene snapshot did not match the announced scene.');
      return;
    }
    applyCamera(payload.camera);
    const followerAdjusted = view.updateFollowerView();
    const cameraAdjusted = followerAdjusted && view.adjustCameraDistanceAndClipping();
    await nextStableFrame();
    notifyHost({ type: 'prepomax.follower-view.applied-v1', sceneRevision: manifest?.sceneRevision ?? 0,
      followerAdjusted, cameraAdjusted });
    notifyHost({ type: 'prepomax.scene.applied-v1', sceneRevision: manifest.sceneRevision });
    return;
  }
  if (message.type === 'camera.updated') {
    applyCamera(message.payload as CameraUpdatedPayload);
    return;
  }
  if (message.type === 'actor.visibility.updated') {
    if (!manifest || message.sceneRevision <= manifest.sceneRevision) return;
    if (message.sceneRevision !== manifest.sceneRevision + 1) {
      showError('Scene events were missed; reloading the manifest snapshot.');
      await loadScene();
      return;
    }
    const payload = message.payload as ActorVisibilityUpdatedPayload;
    const visibility = new Map(payload.actors.map((actor) => [actor.actorId, actor.visible]));
    for (const actor of payload.actors) view.setActorVisibility(actor.actorId, actor.visible);
    view.adjustCameraDistanceAndClipping();
    manifest = {
      ...manifest,
      sceneRevision: message.sceneRevision,
      actors: manifest.actors.map((actor) => visibility.has(actor.actorId)
        ? { ...actor, visible: visibility.get(actor.actorId)! }
        : actor),
    };
    renderActorControls(manifest.actors);
    updateDiagnostics();
    if (hostSectionView.enabled) await resolveSectionGeometry();
    setStatus(`Updated visibility for ${payload.actors.length} actor${payload.actors.length === 1 ? '' : 's'}.`);
    return;
  }
  if (message.type === 'actor.presentation.updated') {
    if (!manifest || message.sceneRevision <= manifest.sceneRevision) return;
    if (message.sceneRevision !== manifest.sceneRevision + 1) {
      showError('Scene events were missed; reloading the manifest snapshot.');
      await loadScene();
      return;
    }
    const payload = message.payload as ActorPresentationUpdatedPayload;
    const presentations = new Map(payload.actors.map((actor) => [actor.actorId, actor]));
    for (const actor of payload.actors) view.setActorColor(actor.actorId, actor.frontRgba);
    manifest = {
      ...manifest,
      sceneRevision: message.sceneRevision,
      actors: manifest.actors.map((actor) => {
        const presentation = presentations.get(actor.actorId);
        return presentation
          ? { ...actor, name: presentation.name, material: { ...actor.material, frontRgba: presentation.frontRgba } }
          : actor;
      }),
    };
    renderActorControls(manifest.actors);
    updateDiagnostics();
    if (hostSectionView.enabled) await resolveSectionGeometry();
    setStatus(`Updated presentation for ${payload.actors.length} actor${payload.actors.length === 1 ? '' : 's'}.`);
    return;
  }
  if (message.type === 'actor.removed') {
    if (!manifest || message.sceneRevision <= manifest.sceneRevision) return;
    if (message.sceneRevision !== manifest.sceneRevision + 1) {
      showError('Scene events were missed; reloading the manifest snapshot.');
      await loadScene();
      return;
    }
    const payload = message.payload as ActorRemovedPayload;
    const removedIds = new Set(payload.actorIds);
    const removedResourceIds = new Set(manifest.actors
      .filter((actor) => removedIds.has(actor.actorId))
      .flatMap((actor) => actor.chunks.flatMap((chunk) => [
        chunk.resources.positions.resourceId,
        chunk.resources.triangleConnectivity.resourceId,
        chunk.resources.sourcePointIds.resourceId,
        chunk.resources.sourceElementIds.resourceId,
        chunk.resources.pointScalars.resourceId,
        ...(chunk.resources.normals ? [chunk.resources.normals.resourceId] : []),
      ])));
    view.removeActors(payload.actorIds);
    view.adjustCameraDistanceAndClipping();
    manifest = {
      ...manifest,
      sceneRevision: message.sceneRevision,
      actors: manifest.actors.filter((actor) => !removedIds.has(actor.actorId)),
      resources: manifest.resources.filter((resource) => !removedResourceIds.has(resource.resourceId)),
    };
    renderActorControls(manifest.actors);
    updateDiagnostics();
    if (hostSectionView.enabled) await resolveSectionGeometry();
    setStatus(`Removed ${payload.actorIds.length} actor${payload.actorIds.length === 1 ? '' : 's'}.`);
    return;
  }
  if (message.type === 'frame.updated') {
    if (!manifest || !currentApi || message.sceneRevision <= manifest.sceneRevision) return;
    if (message.sceneRevision !== manifest.sceneRevision + 1) {
      showError('Scene events were missed; reloading the manifest snapshot.');
      await loadScene();
      return;
    }
    const payload = message.payload as FrameUpdatedPayload;
    const api = currentApi;
    const receivedAt = performance.now();
    const frameBytes = payload.chunks.reduce((sum, chunk) =>
      sum + chunk.positions.byteLength + chunk.pointScalars.byteLength, 0);
    setStatus(`Downloading ${payload.chunks.length} animation frame update${payload.chunks.length === 1 ? '' : 's'}…`);
    try {
      const replacements = await Promise.all(payload.chunks.map(async (chunk) => {
        const [positions, scalars] = await Promise.all([
          api.getResource(chunk.positions),
          api.getResource(chunk.pointScalars),
        ]);
        if (!(positions instanceof Float32Array) || !(scalars instanceof Float32Array))
          throw new Error(`Animation resources for chunk ${chunk.chunkId} are not Float32.`);
        return { chunk, positions, scalars };
      }));
      const downloadedAt = performance.now();
      const oldResourceIds = new Set<string>();
      const replacementResources = new Map<string, FrameUpdatedPayload['chunks'][number]>();
      for (const update of payload.chunks) replacementResources.set(update.chunkId, update);
      const actorBounds = new Map(payload.actors.map((actor) => [actor.actorId, actor.bounds]));
      for (const actor of manifest.actors) for (const chunk of actor.chunks) {
        if (!replacementResources.has(chunk.chunkId)) continue;
        oldResourceIds.add(chunk.resources.positions.resourceId);
        oldResourceIds.add(chunk.resources.pointScalars.resourceId);
      }
      view.setField(payload.field);
      for (const replacement of replacements)
        view.updateFrame(replacement.chunk.chunkId, replacement.positions, replacement.scalars, replacement.chunk.bounds);
      const followerAdjusted = view.updateFollowerView();
      const cameraAdjusted = view.adjustCameraDistanceAndClipping();
      const appliedAt = performance.now();
      frameUpdateBytes += frameBytes;
      manifest = {
        ...manifest,
        sceneRevision: message.sceneRevision,
        bounds: payload.bounds,
        field: payload.field,
        actors: manifest.actors.map((actor) => ({
          ...actor,
          bounds: actorBounds.get(actor.actorId) ?? actor.bounds,
          chunks: actor.chunks.map((chunk) => {
            const update = replacementResources.get(chunk.chunkId);
            return update ? { ...chunk, bounds: update.bounds, resources: {
              ...chunk.resources,
              positions: { resourceId: update.positions.resourceId },
              pointScalars: { resourceId: update.pointScalars.resourceId },
            } } : chunk;
          }),
        })),
        resources: [
          ...manifest.resources.filter((resource) => !oldResourceIds.has(resource.resourceId)),
          ...payload.chunks.flatMap((chunk) => [chunk.positions, chunk.pointScalars]),
        ],
      };
      renderLegend(manifest!);
      if (hostSectionView.enabled) await resolveSectionGeometry();
      await nextStableFrame();
      const stableAt = performance.now();
      const sample: FrameActivationSample = {
        sceneRevision: message.sceneRevision,
        chunks: payload.chunks.length,
        bytes: frameBytes,
        followerAdjusted,
        cameraAdjusted,
        downloadMs: roundMetric(downloadedAt - receivedAt),
        applyMs: roundMetric(appliedAt - downloadedAt),
        stablePaintMs: roundMetric(stableAt - appliedAt),
        totalMs: roundMetric(stableAt - receivedAt),
      };
      frameActivationSamples.push(sample);
      if (frameActivationSamples.length > 32) frameActivationSamples.shift();
      updateDiagnostics();
      embeddedWebView?.postMessage?.(JSON.stringify({ type: 'prepomax.frame.activated-v1', ...sample }));
      setStatus(`Activated animation frame without replacing topology.`);
    } catch (error) {
      showError(error instanceof Error ? error.message : String(error));
      setStatus('Animation frame update failed.');
    }
    return;
  }
  if (message.type !== 'field.updated') {
    return;
  }
  if (!manifest || !currentApi || message.sceneRevision <= manifest.sceneRevision) {
    return;
  }
  if (message.sceneRevision !== manifest.sceneRevision + 1) {
    showError('Scene events were missed; reloading the manifest snapshot.');
    fieldUpdateInProgress = false;
    await loadScene();
    return;
  }

  const payload = message.payload as FieldUpdatedPayload;
  setStatus(`Downloading ${payload.chunks.length} scalar resources…`);
  try {
    const replacements = await Promise.all(payload.chunks.map(async (chunk) => {
      const resource = await currentApi?.getResource(chunk.resource);
      if (!(resource instanceof Float32Array)) {
        throw new Error(`Field resource for chunk ${chunk.chunkId} is not Float32.`);
      }
      return { chunkId: chunk.chunkId, values: resource };
    }));
    scalarUpdateBytes += payload.chunks.reduce((sum, chunk) => sum + chunk.resource.byteLength, 0);
    view.setField(payload.field);
    for (const replacement of replacements) {
      view.updateScalars(replacement.chunkId, replacement.values);
    }
    view.render();
    scalarActivationMs = fieldUpdateStartedAt === null ? null : performance.now() - fieldUpdateStartedAt;
    manifest = { ...manifest, sceneRevision: message.sceneRevision, field: payload.field };
    renderLegend(manifest);
    if (hostSectionView.enabled) await resolveSectionGeometry();
    updateDiagnostics();
    setStatus(`Activated ${payload.field.name} without replacing topology.`);
  } catch (error) {
    showError(error instanceof Error ? error.message : String(error));
    setStatus('Scalar update failed.');
  } finally {
    fieldUpdateInProgress = false;
    fieldUpdateStartedAt = null;
    elements.switchField.disabled = eventSocket?.readyState !== WebSocket.OPEN;
  }
}

interface FrameActivationSample {
  readonly sceneRevision: number;
  readonly chunks: number;
  readonly bytes: number;
  readonly followerAdjusted: boolean;
  readonly cameraAdjusted: boolean;
  readonly downloadMs: number;
  readonly applyMs: number;
  readonly stablePaintMs: number;
  readonly totalMs: number;
}

function applyCamera(camera: CameraUpdatedPayload): void {
  if (camera.kind === 'custom') {
    view.setCameraParameters(camera.position, camera.focalPoint, camera.viewUp, camera.parallelScale,
      camera.operation ?? 'set');
  } else if (camera.view === 'reset') {
    view.resetCamera();
  } else {
    view.setCameraView(camera.view);
    elements.cameraView.value = camera.view;
  }
  notifyCameraChanged(view.getCameraParameters());
  updateDiagnostics();
}

function unloadScene(): void {
  activeLoad?.abort();
  activePick?.abort();
  activeSection?.abort();
  eventSocket?.close();
  activeLoad = null;
  activePick = null;
  activeSection = null;
  sectionGeneration += 1;
  eventSocket = null;
  currentApi = null;
  manifest = null;
  sceneGeneration += 1;
  view.delete();
  elements.viewport.replaceChildren();
  view = new VtkSceneView(elements.viewport, showError, annotationInteractions, notifyCameraChanged);
  view.setParallelProjection(elements.orthographic.checked);
  elements.actors.replaceChildren(Object.assign(document.createElement('span'), { className: 'muted', textContent: 'No scene loaded' }));
  elements.legend.className = 'legend muted';
  elements.legend.textContent = 'Scalars not loaded';
  elements.unload.disabled = true;
  elements.resetCamera.disabled = true;
  elements.cameraView.disabled = true;
  elements.switchField.disabled = true;
  clearPick('No selection');
  setStatus('Scene unloaded.');
  updateDiagnostics();
}

function nextStableFrame(): Promise<void> {
  return new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
}

function readBrowserMemory(): object | null {
  const memory = (performance as Performance & { memory?: {
    usedJSHeapSize: number;
    totalJSHeapSize: number;
    jsHeapSizeLimit: number;
  } }).memory;
  return memory ? {
    usedJSHeapSize: memory.usedJSHeapSize,
    totalJSHeapSize: memory.totalJSHeapSize,
    jsHeapSizeLimit: memory.jsHeapSizeLimit,
  } : null;
}

function downloadDiagnostics(): void {
  const blob = new Blob([elements.diagnostics.textContent ?? '{}'], { type: 'application/json' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = `prepomax-render-diagnostics-${new Date().toISOString().replaceAll(':', '-')}.json`;
  link.click();
  URL.revokeObjectURL(link.href);
}

function setStatus(message: string): void {
  elements.status.value = message;
}

function showError(message: string): void {
  elements.error.textContent = message;
  elements.error.hidden = false;
}

function clearError(): void {
  elements.error.hidden = true;
  elements.error.textContent = '';
}

function formatBytes(bytes: number): string {
  return bytes < 1024 ? `${bytes} B` : `${(bytes / 1024).toFixed(1)} KiB`;
}

function requireElement<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Missing required UI element #${id}.`);
  }
  return element as T;
}

function readResourceConcurrency(): number {
  const parsed = Number.parseInt(new URLSearchParams(window.location.search).get('concurrency') ?? '3', 10);
  return Number.isInteger(parsed) && parsed >= 1 && parsed <= 16 ? parsed : 3;
}

declare global {
  interface Window {
    __prepomaxBenchmark?: {
      measureOrbit(frameCount?: number): Promise<number[]>;
      unload(): void;
      disconnectEvents(): void;
    };
  }
}

if (['1'].includes(new URLSearchParams(window.location.search).get('benchmark') ?? '')
  || ['1'].includes(new URLSearchParams(window.location.search).get('testHooks') ?? '')) {
  window.__prepomaxBenchmark = {
    measureOrbit: (frameCount) => view.measureOrbit(frameCount),
    unload: unloadScene,
    disconnectEvents: () => eventSocket?.close(),
  };
}
