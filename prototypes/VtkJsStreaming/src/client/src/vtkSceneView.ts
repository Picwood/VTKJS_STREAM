import '@kitware/vtk.js/Rendering/Profiles/Geometry';
import vtkDataArray from '@kitware/vtk.js/Common/Core/DataArray';
import vtkPlane, { type vtkPlane as VtkPlane } from '@kitware/vtk.js/Common/DataModel/Plane';
import vtkPolyData from '@kitware/vtk.js/Common/DataModel/PolyData';
import { FieldAssociations } from '@kitware/vtk.js/Common/DataModel/DataSet/Constants';
import vtkActor, { type vtkActor as VtkActor } from '@kitware/vtk.js/Rendering/Core/Actor';
import vtkColorTransferFunction from '@kitware/vtk.js/Rendering/Core/ColorTransferFunction';
import vtkMapper, { type vtkMapper as VtkMapper } from '@kitware/vtk.js/Rendering/Core/Mapper';
import type { vtkHardwareSelector as VtkHardwareSelector } from '@kitware/vtk.js/Rendering/Core/HardwareSelector';
import vtkGenericRenderWindow, { type vtkGenericRenderWindow as VtkGenericRenderWindow } from '@kitware/vtk.js/Rendering/Misc/GenericRenderWindow';
import type { vtkPolyData as VtkPolyData } from '@kitware/vtk.js/Common/DataModel/PolyData';
import type { LoadedAttributes, LoadedGeometry } from './chunkLoader';
import type { AreaResolveRequest, FrustumResolveRequest, HostAnnotation, HostAnnotationAction, PickResolveRequest, ScalarFieldMetadata, SelectionSectionPlane } from './protocol';
import { toVtkLineCells, toVtkTriangleCells } from './resourceDecoder';
import type { SectionGeometryActor } from './sectionGeometryBinary';
import { resolveTriangleSource } from './pickResolver';
import { getNodeOverlayStride } from './selectionOverlay';
import { clipPolygonToSection, type SectionSelectionPoint } from './sectionSelection';

interface ChunkPipeline {
  readonly actorId: string;
  readonly actorName: string;
  readonly chunkId: string;
  readonly actor: VtkActor;
  readonly mapper: VtkMapper;
  readonly polyData: VtkPolyData;
  readonly topologyToken: string;
  readonly connectivity: Uint32Array;
  readonly positions: Float32Array;
  relativeBounds: readonly [number, number, number, number, number, number];
  pickable: boolean;
  sourcePointIds: Int32Array | null;
  sourceElementIds: Int32Array | null;
  hasScalars: boolean;
}

interface SelectionOverlay {
  readonly actorId: string;
  readonly actor: VtkActor;
  readonly mapper: VtkMapper;
  readonly polyData: VtkPolyData | null;
}

interface SectionPipeline {
  readonly actorName: string;
  readonly actor: VtkActor;
  readonly mapper: VtkMapper;
  readonly polyData: VtkPolyData;
}

interface AnnotationOverlay {
  readonly state: HostAnnotation;
  readonly element: HTMLDivElement;
}

interface FollowerViewState {
  readonly actorNames: readonly string[];
  readonly nodeIds: readonly number[];
  previousPoints: [number, number, number][] | null;
}

export interface AnnotationInteractionCallbacks {
  readonly edited: (name: string, text: string) => void;
  readonly moved: (name: string, offset: readonly [number, number]) => void;
  readonly action: (name: string, action: HostAnnotationAction) => void;
}

export interface CameraParameters {
  readonly position: readonly [number, number, number];
  readonly focalPoint: readonly [number, number, number];
  readonly viewUp: readonly [number, number, number];
  readonly parallelScale: number;
}

export type CameraOperation = 'set' | 'fit' | 'adjust';

export type HostSelectionItem = 'none' | 'node' | 'element' | 'surface' | 'geometry-edge' | 'geometry' | 'part' | 'unsupported';

interface ConcreteHardwareSelector extends VtkHardwareSelector {
  beginSelection(): void;
  endSelection(): void;
  releasePixBuffers(): void;
}

export type CameraView = 'isometric' | 'front' | 'back' | 'left' | 'right' | 'top' | 'bottom';

type Vector3 = readonly [number, number, number];
type Matrix3 = readonly [Vector3, Vector3, Vector3];

function add(first: ArrayLike<number>, second: ArrayLike<number>): [number, number, number] {
  return [first[0]! + second[0]!, first[1]! + second[1]!, first[2]! + second[2]!];
}

function subtract(first: ArrayLike<number>, second: ArrayLike<number>): [number, number, number] {
  return [first[0]! - second[0]!, first[1]! - second[1]!, first[2]! - second[2]!];
}

function scale(vector: ArrayLike<number>, factor: number): [number, number, number] {
  return [vector[0]! * factor, vector[1]! * factor, vector[2]! * factor];
}

function dot(first: ArrayLike<number>, second: ArrayLike<number>): number {
  return first[0]! * second[0]! + first[1]! * second[1]! + first[2]! * second[2]!;
}

function lengthSquared(vector: ArrayLike<number>): number {
  return dot(vector, vector);
}

function normalize(vector: ArrayLike<number>): [number, number, number] | null {
  const magnitude = Math.sqrt(lengthSquared(vector));
  return magnitude > 1e-12 ? scale(vector, 1 / magnitude) : null;
}

function cross(first: ArrayLike<number>, second: ArrayLike<number>): [number, number, number] {
  return [first[1]! * second[2]! - first[2]! * second[1]!,
    first[2]! * second[0]! - first[0]! * second[2]!,
    first[0]! * second[1]! - first[1]! * second[0]!];
}

function transposeMatrix(matrix: Matrix3): Matrix3 {
  return [[matrix[0][0], matrix[1][0], matrix[2][0]],
    [matrix[0][1], matrix[1][1], matrix[2][1]],
    [matrix[0][2], matrix[1][2], matrix[2][2]]];
}

function multiplyMatrix(first: Matrix3, second: Matrix3): Matrix3 {
  return [0, 1, 2].map((row) => [0, 1, 2].map((column) =>
    first[row]![0] * second[0][column]! + first[row]![1] * second[1][column]!
    + first[row]![2] * second[2][column]!) as [number, number, number]) as unknown as Matrix3;
}

function multiplyMatrixVector(matrix: Matrix3, vector: ArrayLike<number>): [number, number, number] {
  return [dot(matrix[0], vector), dot(matrix[1], vector), dot(matrix[2], vector)];
}

function followerBasis(points: readonly Vector3[]): { center: [number, number, number]; matrix: Matrix3 } | null {
  const edge = normalize(subtract(points[1]!, points[0]!));
  const normal = normalize(cross(subtract(points[1]!, points[0]!), subtract(points[2]!, points[0]!)));
  if (!edge || !normal) return null;
  const second = cross(normal, edge);
  const center = scale(add(add(points[0]!, points[1]!), points[2]!), 1 / 3);
  return { center, matrix: [[edge[0], second[0], normal[0]], [edge[1], second[1], normal[1]],
    [edge[2], second[2], normal[2]]] };
}

export class VtkSceneView {
  readonly #window: VtkGenericRenderWindow;
  readonly #lookupTable = vtkColorTransferFunction.newInstance();
  readonly #chunks = new Map<string, ChunkPipeline>();
  readonly #chunksByProp = new Map<VtkActor, ChunkPipeline>();
  readonly #selector: ConcreteHardwareSelector;
  readonly #selectionOverlays: SelectionOverlay[] = [];
  readonly #sectionPipelines: SectionPipeline[] = [];
  readonly #container: HTMLElement;
  readonly #annotationLayer: HTMLDivElement;
  readonly #annotationOverlays = new Map<string, AnnotationOverlay>();
  readonly #interactionSubscriptions: { unsubscribe(): void }[];
  readonly #resizeObserver: ResizeObserver;
  readonly #annotationInteractions: AnnotationInteractionCallbacks | null;
  readonly #cameraChanged: ((camera: CameraParameters) => void) | null;
  #annotationContextMenu: HTMLDivElement | null = null;
  #field: ScalarFieldMetadata | null = null;
  #worldOrigin: readonly [number, number, number] = [0, 0, 0];
  #colorMapName = 'Cool to Warm';
  #contextLost = false;
  #selectionItem: HostSelectionItem = 'none';
  #selectedIds = new Set<number>();
  #selectedActorNames = new Set<string>();
  #selectedFaceNodeIds: readonly (readonly number[])[] = [];
  #selectedEdgeNodeIds: readonly (readonly number[])[] = [];
  #selectionOverlayOverflow = false;
  #followerView: FollowerViewState | null = null;
  readonly #explodedOffsets = new Map<string, [number, number, number]>();
  #explodedAnimationGeneration = 0;
  #sectionPlane: VtkPlane | null = null;
  #sectionPoint: [number, number, number] | null = null;
  #sectionNormal: [number, number, number] | null = null;

  public constructor(container: HTMLElement, onContextError: (message: string) => void,
                     annotationInteractions: AnnotationInteractionCallbacks | null = null,
                     cameraChanged: ((camera: CameraParameters) => void) | null = null) {
    this.#container = container;
    this.#annotationInteractions = annotationInteractions;
    this.#cameraChanged = cameraChanged;
    this.#window = vtkGenericRenderWindow.newInstance({ background: [0.87, 0.91, 0.95] });
    this.#window.setContainer(container);
    this.#window.resize();
    this.#selector = this.#window.getInteractor().getView().getSelector() as ConcreteHardwareSelector;
    this.#selector.setFieldAssociation(FieldAssociations.FIELD_ASSOCIATION_CELLS);
    this.#selector.setCaptureZValues(true);
    this.#annotationLayer = document.createElement('div');
    this.#annotationLayer.className = 'annotation-layer';
    container.appendChild(this.#annotationLayer);
    const interactor = this.#window.getInteractor();
    this.#interactionSubscriptions = [
      interactor.onAnimation(() => this.#updateAnnotationPositions()),
      interactor.onEndAnimation(() => this.#notifyCameraChanged()),
    ];
    this.#resizeObserver = new ResizeObserver(() => {
      this.#window.resize();
      this.#updateAnnotationPositions();
    });
    this.#resizeObserver.observe(container);
    const canvas = container.querySelector('canvas');
    canvas?.addEventListener('webglcontextlost', (event) => {
      event.preventDefault();
      this.#contextLost = true;
      onContextError('WebGL context lost. Reload the scene or reduce model/chunk size.');
    });
    canvas?.addEventListener('webglcontextrestored', () => {
      this.#contextLost = false;
      this.render();
    });
  }

  public setField(field: ScalarFieldMetadata): void {
    this.#field = field;
    this.#updateLookupTable();
  }

  public setWorldOrigin(origin: readonly [number, number, number]): void {
    this.#worldOrigin = [...origin];
  }

  public setColorMap(name: string): void {
    this.#colorMapName = name;
    this.#updateLookupTable();
    for (const pipeline of this.#chunks.values()) {
      pipeline.mapper.setLookupTable(this.#lookupTable);
    }
    for (const pipeline of this.#sectionPipelines) pipeline.mapper.setLookupTable(this.#lookupTable);
    this.render();
  }

  public addGeometry(geometry: LoadedGeometry): void {
    const polyData = vtkPolyData.newInstance();
    polyData.getPoints().setData(geometry.positions, 3);
    polyData.getPolys().setData(toVtkTriangleCells(geometry.connectivity));

    const mapper = vtkMapper.newInstance({ scalarVisibility: false });
    mapper.setInputData(polyData);
    if (this.#sectionPlane) mapper.addClippingPlane(this.#sectionPlane);
    const actor = vtkActor.newInstance();
    actor.setMapper(mapper);
    actor.setVisibility(geometry.actor.visible);
    actor.setPickable(geometry.actor.pickable);
    actor.setPosition(...(this.#explodedOffsets.get(geometry.actor.name) ?? [0, 0, 0]));
    const [red, green, blue, opacity] = geometry.actor.material.frontRgba;
    actor.getProperty().setColor(red, green, blue);
    actor.getProperty().setOpacity(opacity);
    this.#window.getRenderer().addActor(actor);
    const pipeline: ChunkPipeline = {
      actorId: geometry.actor.actorId,
      actorName: geometry.actor.name,
      chunkId: geometry.chunk.chunkId,
      actor,
      mapper,
      polyData,
      topologyToken: `${geometry.chunk.chunkId}:topology-v1`,
      connectivity: geometry.connectivity,
      positions: geometry.positions,
      relativeBounds: [
        geometry.chunk.bounds[0]! - this.#worldOrigin[0], geometry.chunk.bounds[1]! - this.#worldOrigin[0],
        geometry.chunk.bounds[2]! - this.#worldOrigin[1], geometry.chunk.bounds[3]! - this.#worldOrigin[1],
        geometry.chunk.bounds[4]! - this.#worldOrigin[2], geometry.chunk.bounds[5]! - this.#worldOrigin[2],
      ],
      pickable: geometry.actor.pickable,
      sourcePointIds: null,
      sourceElementIds: null,
      hasScalars: false,
    };
    this.#chunks.set(geometry.chunk.chunkId, pipeline);
    this.#chunksByProp.set(actor, pipeline);
    this.render();
  }

  public addAttributes(attributes: LoadedAttributes): void {
    const pipeline = this.#chunks.get(attributes.chunkId);
    if (!pipeline) {
      throw new Error(`Cannot apply attributes before geometry for chunk ${attributes.chunkId}.`);
    }
    pipeline.sourcePointIds = attributes.sourcePointIds;
    pipeline.sourceElementIds = attributes.sourceElementIds;
    this.#captureFollowerBaseline();
    const scalars = vtkDataArray.newInstance({
      name: 'pointScalars',
      numberOfComponents: 1,
      values: attributes.scalars,
    });
    pipeline.polyData.getPointData().setScalars(scalars);
    pipeline.hasScalars = attributes.scalars.some((value) => Number.isFinite(value));
    if (this.#field) {
      pipeline.mapper.setScalarRange([...this.#field.displayedRange]);
    }
    pipeline.mapper.setLookupTable(this.#lookupTable);
    pipeline.mapper.setUseLookupTableScalarRange(true);
    pipeline.mapper.setScalarModeToUsePointData();
    pipeline.mapper.setScalarVisibility(pipeline.hasScalars);
    pipeline.polyData.modified();
    this.#rebuildSelectionOverlays();
    this.render();
  }

  public replaceSectionGeometry(actors: readonly SectionGeometryActor[]): void {
    this.#clearSectionGeometry();
    const renderer = this.#window.getRenderer();
    for (const geometry of actors) {
      const polyData = vtkPolyData.newInstance();
      polyData.getPoints().setData(geometry.positions, 3);
      polyData.getPolys().setData(toVtkTriangleCells(geometry.triangleConnectivity));
      polyData.getLines().setData(toVtkLineCells(geometry.lineConnectivity));
      const scalars = vtkDataArray.newInstance({ name: 'pointScalars', numberOfComponents: 1,
        values: geometry.pointScalars });
      polyData.getPointData().setScalars(scalars);
      const mapper = vtkMapper.newInstance({ scalarVisibility: geometry.pointScalars.some(Number.isFinite) });
      mapper.setInputData(polyData);
      mapper.setLookupTable(this.#lookupTable);
      mapper.setUseLookupTableScalarRange(true);
      mapper.setScalarModeToUsePointData();
      if (this.#field) mapper.setScalarRange([...this.#field.displayedRange]);
      const actor = vtkActor.newInstance();
      actor.setMapper(mapper);
      actor.setPickable(false);
      actor.setPosition(...(this.#explodedOffsets.get(geometry.name) ?? [0, 0, 0]));
      actor.getProperty().setColor(geometry.rgba[0], geometry.rgba[1], geometry.rgba[2]);
      actor.getProperty().setOpacity(geometry.rgba[3]);
      actor.getProperty().setLineWidth(geometry.lineWidthPx);
      renderer.addActor(actor);
      this.#sectionPipelines.push({ actorName: geometry.name, actor, mapper, polyData });
    }
    this.render();
  }

  public removeSectionGeometry(): void {
    this.#clearSectionGeometry();
    this.render();
  }

  public setSelection(
    item: HostSelectionItem,
    ids: readonly number[],
    actorNames: readonly string[],
    faceNodeIds: readonly (readonly number[])[],
    edgeNodeIds: readonly (readonly number[])[],
  ): void {
    this.#selectionItem = item;
    this.#selectedIds = new Set(ids);
    this.#selectedActorNames = new Set(actorNames);
    this.#selectedFaceNodeIds = faceNodeIds.map((face) => [...face]);
    this.#selectedEdgeNodeIds = edgeNodeIds.map((edge) => [...edge]);
    this.#rebuildSelectionOverlays();
    this.render();
  }

  public setAnnotations(annotations: readonly HostAnnotation[]): void {
    this.#closeAnnotationContextMenu();
    this.#annotationLayer.replaceChildren();
    this.#annotationOverlays.clear();
    for (const state of annotations) {
      const element = document.createElement('div');
      element.className = 'annotation-callout';
      element.classList.toggle('with-background', state.drawBackground);
      element.classList.toggle('with-border', state.drawBorder);
      element.textContent = state.text;
      element.title = state.name;
      element.tabIndex = 0;
      this.#bindAnnotationInteraction(element, state);
      this.#annotationLayer.appendChild(element);
      this.#annotationOverlays.set(state.name, { state, element });
    }
    this.#updateAnnotationPositions();
  }

  #bindAnnotationInteraction(element: HTMLDivElement, state: HostAnnotation): void {
    let drag: { pointerId: number; x: number; y: number; offset: [number, number] } | null = null;
    let editing = false;
    let cancelEdit = false;
    element.addEventListener('pointerdown', (event) => {
      if (event.button !== 0 || editing) return;
      drag = { pointerId: event.pointerId, x: event.clientX, y: event.clientY,
        offset: [state.offset[0], state.offset[1]] };
      try { element.setPointerCapture(event.pointerId); } catch { /* Synthetic acceptance events have no active pointer. */ }
      element.classList.add('dragging');
      event.preventDefault();
      event.stopPropagation();
    });
    element.addEventListener('pointermove', (event) => {
      if (!drag || event.pointerId !== drag.pointerId) return;
      const offset: [number, number] = [
        Math.max(-100_000, Math.min(100_000, drag.offset[0] + event.clientX - drag.x)),
        Math.max(-100_000, Math.min(100_000, drag.offset[1] + event.clientY - drag.y)),
      ];
      this.#positionAnnotation(state, element, offset);
      event.preventDefault();
      event.stopPropagation();
    });
    element.addEventListener('pointerup', (event) => {
      if (!drag || event.pointerId !== drag.pointerId) return;
      const offset: [number, number] = [
        Math.max(-100_000, Math.min(100_000, drag.offset[0] + event.clientX - drag.x)),
        Math.max(-100_000, Math.min(100_000, drag.offset[1] + event.clientY - drag.y)),
      ];
      drag = null;
      element.classList.remove('dragging');
      if (element.hasPointerCapture(event.pointerId)) element.releasePointerCapture(event.pointerId);
      this.#annotationInteractions?.moved(state.name, offset);
      event.preventDefault();
      event.stopPropagation();
    });
    element.addEventListener('click', (event) => event.stopPropagation());
    element.addEventListener('contextmenu', (event) => {
      if (!this.#annotationInteractions) return;
      this.#showAnnotationContextMenu(state, event.clientX, event.clientY);
      event.preventDefault();
      event.stopPropagation();
    });
    element.addEventListener('dblclick', (event) => {
      if (isReservedAnnotation(state.name) || !this.#annotationInteractions) return;
      editing = true;
      cancelEdit = false;
      element.contentEditable = 'plaintext-only';
      element.classList.add('editing');
      element.focus();
      const selection = window.getSelection();
      const range = document.createRange();
      range.selectNodeContents(element);
      selection?.removeAllRanges();
      selection?.addRange(range);
      event.preventDefault();
      event.stopPropagation();
    });
    element.addEventListener('keydown', (event) => {
      if (!editing) return;
      if (event.key === 'Escape') {
        cancelEdit = true;
        element.textContent = state.text;
        element.blur();
      } else if (event.key === 'Enter' && event.ctrlKey) {
        event.preventDefault();
        element.blur();
      }
      event.stopPropagation();
    });
    element.addEventListener('blur', () => {
      if (!editing) return;
      editing = false;
      element.contentEditable = 'false';
      element.classList.remove('editing');
      const text = (element.innerText || element.textContent || '').replace(/\r\n/g, '\n');
      if (!cancelEdit && text.length > 0 && text.length <= 65_536 && text !== state.text)
        this.#annotationInteractions?.edited(state.name, text);
      else element.textContent = state.text;
    });
  }

  #showAnnotationContextMenu(state: HostAnnotation, clientX: number, clientY: number): void {
    this.#closeAnnotationContextMenu();
    const menu = document.createElement('div');
    menu.className = 'annotation-context-menu';
    menu.setAttribute('role', 'menu');
    menu.setAttribute('aria-label', `Actions for ${state.name}`);
    const actions: readonly { action: HostAnnotationAction; label: string }[] = isReservedAnnotation(state.name)
      ? [{ action: 'settings', label: 'Settings' }]
      : [
        { action: 'reset', label: 'Reset' },
        { action: 'settings', label: 'Settings' },
        { action: 'delete', label: 'Delete' },
      ];
    for (const { action, label } of actions) {
      const button = document.createElement('button');
      button.type = 'button';
      button.setAttribute('role', 'menuitem');
      button.dataset.action = action;
      button.textContent = label;
      const activate = () => {
        this.#annotationInteractions?.action(state.name, action);
        this.#closeAnnotationContextMenu();
      };
      button.addEventListener('pointerdown', (event) => {
        if (event.button !== 0) return;
        activate();
        event.preventDefault();
        event.stopPropagation();
      });
      button.addEventListener('keydown', (event) => {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        activate();
        event.preventDefault();
        event.stopPropagation();
      });
      button.addEventListener('click', (event) => event.stopPropagation());
      menu.appendChild(button);
    }
    const bounds = this.#container.getBoundingClientRect();
    menu.style.left = `${Math.max(0, clientX - bounds.left)}px`;
    menu.style.top = `${Math.max(0, clientY - bounds.top)}px`;
    this.#annotationLayer.appendChild(menu);
    this.#annotationContextMenu = menu;
    const left = Math.min(Math.max(0, clientX - bounds.left), Math.max(0, bounds.width - menu.offsetWidth));
    const top = Math.min(Math.max(0, clientY - bounds.top), Math.max(0, bounds.height - menu.offsetHeight));
    menu.style.left = `${left}px`;
    menu.style.top = `${top}px`;
    menu.querySelector<HTMLButtonElement>('button')?.focus();
    const dismiss = (event: PointerEvent) => {
      if (!menu.contains(event.target as Node)) this.#closeAnnotationContextMenu();
    };
    queueMicrotask(() => document.addEventListener('pointerdown', dismiss, { once: true }));
    menu.addEventListener('keydown', (event) => {
      if (event.key === 'Escape') {
        this.#closeAnnotationContextMenu();
        event.preventDefault();
      }
    });
  }

  #closeAnnotationContextMenu(): void {
    this.#annotationContextMenu?.remove();
    this.#annotationContextMenu = null;
  }

  public setActorVisibility(actorId: string, visible: boolean): void {
    for (const pipeline of this.#chunks.values()) {
      if (pipeline.actorId === actorId) {
        pipeline.actor.setVisibility(visible);
      }
    }
    for (const overlay of this.#selectionOverlays) {
      if (overlay.actorId === actorId) overlay.actor.setVisibility(visible);
    }
    this.render();
  }

  public adjustCameraDistanceAndClipping(): boolean {
    const renderer = this.#window.getRenderer();
    const bounds = renderer.computeVisiblePropBounds();
    const validBounds = bounds.every((value) => Number.isFinite(value))
      && bounds[0]! <= bounds[1]! && bounds[2]! <= bounds[3]! && bounds[4]! <= bounds[5]!;
    if (!validBounds) {
      this.#notifyCameraChanged();
      return false;
    }
    this.#adjustCameraDistance(bounds);
    renderer.resetCameraClippingRange(bounds);
    this.render();
    this.#notifyCameraChanged();
    return true;
  }

  public setActorColor(actorId: string, rgba: readonly [number, number, number, number]): void {
    for (const pipeline of this.#chunks.values()) {
      if (pipeline.actorId === actorId) {
        pipeline.actor.getProperty().setColor(rgba[0], rgba[1], rgba[2]);
        pipeline.actor.getProperty().setOpacity(rgba[3]);
      }
    }
    this.render();
  }

  public removeActors(actorIds: readonly string[]): void {
    const removedIds = new Set(actorIds);
    const removedNames = new Set<string>();
    const renderer = this.#window.getRenderer();
    for (const [chunkId, pipeline] of [...this.#chunks]) {
      if (!removedIds.has(pipeline.actorId)) continue;
      removedNames.add(pipeline.actorName);
      renderer.removeActor(pipeline.actor);
      this.#chunks.delete(chunkId);
      this.#chunksByProp.delete(pipeline.actor);
      pipeline.actor.delete();
      pipeline.mapper.delete();
      pipeline.polyData.delete();
      pipeline.sourcePointIds = null;
      pipeline.sourceElementIds = null;
    }
    for (const name of removedNames) this.#selectedActorNames.delete(name);
    this.#rebuildSelectionOverlays();
    this.render();
  }

  public setActorPickable(actorId: string, pickable: boolean): void {
    for (const pipeline of this.#chunks.values()) {
      if (pipeline.actorId === actorId) {
        pipeline.pickable = pickable;
        pipeline.actor.setPickable(pickable);
      }
    }
  }

  public updateFrame(
    chunkId: string,
    positions: Float32Array,
    scalars: Float32Array,
    bounds: readonly [number, number, number, number, number, number],
  ): void {
    const pipeline = this.#chunks.get(chunkId);
    if (!pipeline || positions.length !== pipeline.polyData.getNumberOfPoints() * 3 ||
        scalars.length !== pipeline.polyData.getNumberOfPoints())
      throw new Error(`Animation frame counts do not match chunk ${chunkId}.`);
    pipeline.polyData.getPoints().setData(positions, 3);
    pipeline.positions.set(positions);
    pipeline.relativeBounds = [
      bounds[0] - this.#worldOrigin[0], bounds[1] - this.#worldOrigin[0],
      bounds[2] - this.#worldOrigin[1], bounds[3] - this.#worldOrigin[1],
      bounds[4] - this.#worldOrigin[2], bounds[5] - this.#worldOrigin[2],
    ];
    this.updateScalars(chunkId, scalars);
    pipeline.polyData.modified();
  }

  public setFollowerView(actorNames: readonly string[], nodeIds: readonly number[],
                         previousPoints: readonly Vector3[] | null = null): void {
    this.#followerView = actorNames.length === 0 ? null : {
      actorNames: [...actorNames],
      nodeIds: [...nodeIds],
      previousPoints: previousPoints
        ? previousPoints.map((point) => [...point] as [number, number, number]) : null,
    };
    this.#captureFollowerBaseline();
  }

  public getFollowerPoints(): [number, number, number][] | null {
    if (!this.#followerView) return null;
    const points = this.#resolveFollowerPoints(this.#followerView);
    return points?.map((point) => [...point]) as [number, number, number][] | null;
  }

  public updateFollowerView(): boolean {
    const state = this.#followerView;
    if (!state) return false;
    const current = this.#resolveFollowerPoints(state);
    if (!current) return false;
    if (!state.previousPoints) {
      state.previousPoints = current;
      return false;
    }
    const previous = state.previousPoints;
    state.previousPoints = current;
    const camera = this.#window.getRenderer().getActiveCamera();
    const cameraPosition = add(camera.getPosition(), this.#worldOrigin);
    const cameraFocalPoint = add(camera.getFocalPoint(), this.#worldOrigin);
    if (current.length === 1) {
      const normal = camera.getViewPlaneNormal();
      const delta = subtract(previous[0]!, current[0]!);
      const projection = dot(normal, delta);
      const inPlane = subtract(delta, scale(normal, projection));
      if (lengthSquared(inPlane) <= 1e-24) return false;
      camera.setPosition(...subtract(subtract(cameraPosition, inPlane), this.#worldOrigin));
      camera.setFocalPoint(...subtract(subtract(cameraFocalPoint, inPlane), this.#worldOrigin));
    } else {
      const previousBasis = followerBasis(previous);
      const currentBasis = followerBasis(current);
      if (!previousBasis || !currentBasis) return false;
      const rotation = multiplyMatrix(currentBasis.matrix, transposeMatrix(previousBasis.matrix));
      const translation = subtract(currentBasis.center, multiplyMatrixVector(rotation, previousBasis.center));
      const transformedTranslation = multiplyMatrixVector(rotation, translation);
      camera.setPosition(...subtract(add(multiplyMatrixVector(rotation, cameraPosition), transformedTranslation),
        this.#worldOrigin));
      camera.setFocalPoint(...subtract(add(multiplyMatrixVector(rotation, cameraFocalPoint), transformedTranslation),
        this.#worldOrigin));
      camera.setViewUp(...multiplyMatrixVector(rotation, camera.getViewUp()));
    }
    return true;
  }

  public hasExplodedPreview(): boolean {
    return [...this.#explodedOffsets.values()].some((offset) => lengthSquared(offset) > 1e-24);
  }

  public setSectionView(point: Vector3, normal: Vector3): void {
    const magnitude = Math.sqrt(lengthSquared(normal));
    const normalized: [number, number, number] = [normal[0] / magnitude, normal[1] / magnitude,
      normal[2] / magnitude];
    const relativePoint: [number, number, number] = [point[0] - this.#worldOrigin[0],
      point[1] - this.#worldOrigin[1], point[2] - this.#worldOrigin[2]];
    if (!this.#sectionPlane) {
      this.#sectionPlane = vtkPlane.newInstance({ origin: relativePoint, normal: normalized });
      for (const pipeline of this.#chunks.values()) pipeline.mapper.addClippingPlane(this.#sectionPlane);
      for (const overlay of this.#selectionOverlays) overlay.mapper.addClippingPlane(this.#sectionPlane);
    } else {
      this.#sectionPlane.setOrigin(relativePoint);
      this.#sectionPlane.setNormal(normalized);
    }
    this.#sectionPoint = [...point];
    this.#sectionNormal = normalized;
    this.render();
  }

  public removeSectionView(): void {
    if (!this.#sectionPlane) return;
    for (const pipeline of this.#chunks.values()) pipeline.mapper.removeClippingPlane(this.#sectionPlane);
    for (const overlay of this.#selectionOverlays) overlay.mapper.removeClippingPlane(this.#sectionPlane);
    this.#sectionPlane.delete();
    this.#sectionPlane = null;
    this.#sectionPoint = null;
    this.#sectionNormal = null;
    this.render();
  }

  public async setExplodedViewPreview(
    offsets: ReadonlyMap<string, Vector3>,
    animate: boolean,
    durationMs: number,
  ): Promise<boolean> {
    const generation = ++this.#explodedAnimationGeneration;
    const starts = new Map<string, [number, number, number]>();
    for (const [actorName] of offsets) starts.set(actorName, [...(this.#explodedOffsets.get(actorName) ?? [0, 0, 0])]);
    const startedAt = performance.now();
    const duration = animate ? Math.max(1, durationMs) : 0;
    return await new Promise<boolean>((resolve) => {
      const apply = (now: number) => {
        if (generation !== this.#explodedAnimationGeneration) {
          resolve(false);
          return;
        }
        const linear = duration === 0 ? 1 : Math.min(1, (now - startedAt) / duration);
        const eased = 0.5 + 0.5 * Math.atan(linear * 6 - 3) / Math.atan(3);
        for (const [actorName, target] of offsets) {
          const start = starts.get(actorName)!;
          const offset: [number, number, number] = [
            start[0] + (target[0] - start[0]) * eased,
            start[1] + (target[1] - start[1]) * eased,
            start[2] + (target[2] - start[2]) * eased,
          ];
          if (linear === 1 && lengthSquared(offset) <= 1e-24) this.#explodedOffsets.delete(actorName);
          else this.#explodedOffsets.set(actorName, offset);
          this.#applyExplodedOffset(actorName, offset);
        }
        this.adjustCameraDistanceAndClipping();
        if (linear < 1) requestAnimationFrame(apply);
        else resolve(true);
      };
      requestAnimationFrame(apply);
    });
  }

  public cancelExplodedViewPreview(): void {
    this.#explodedAnimationGeneration += 1;
    for (const actorName of [...this.#explodedOffsets.keys()]) this.#applyExplodedOffset(actorName, [0, 0, 0]);
    this.#explodedOffsets.clear();
  }

  #applyExplodedOffset(actorName: string, offset: Vector3): void {
    const actorIds = new Set<string>();
    for (const pipeline of this.#chunks.values()) {
      if (pipeline.actorName !== actorName) continue;
      pipeline.actor.setPosition(...offset);
      actorIds.add(pipeline.actorId);
    }
    for (const overlay of this.#selectionOverlays) {
      if (actorIds.has(overlay.actorId)) overlay.actor.setPosition(...offset);
    }
    for (const pipeline of this.#sectionPipelines) {
      if (pipeline.actorName === actorName) pipeline.actor.setPosition(...offset);
    }
  }

  #captureFollowerBaseline(): void {
    if (!this.#followerView || this.#followerView.previousPoints) return;
    this.#followerView.previousPoints = this.#resolveFollowerPoints(this.#followerView);
  }

  #resolveFollowerPoints(state: FollowerViewState): [number, number, number][] | null {
    const points: [number, number, number][] = [];
    for (let follower = 0; follower < state.nodeIds.length; follower += 1) {
      let match: [number, number, number] | null = null;
      for (const pipeline of this.#chunks.values()) {
        if (pipeline.actorName !== state.actorNames[follower] || !pipeline.sourcePointIds) continue;
        const pointIndex = pipeline.sourcePointIds.indexOf(state.nodeIds[follower]!);
        if (pointIndex < 0) continue;
        const offset = this.#explodedOffsets.get(pipeline.actorName) ?? [0, 0, 0];
        match = [pipeline.positions[pointIndex * 3]! + this.#worldOrigin[0] + offset[0],
          pipeline.positions[pointIndex * 3 + 1]! + this.#worldOrigin[1] + offset[1],
          pipeline.positions[pointIndex * 3 + 2]! + this.#worldOrigin[2] + offset[2]];
        break;
      }
      if (!match) return null;
      points.push(match);
    }
    return points;
  }

  public resetCamera(): void {
    this.#window.getRenderer().resetCamera();
    this.render();
    this.#notifyCameraChanged();
  }

  public setParallelProjection(enabled: boolean): void {
    this.#window.getRenderer().getActiveCamera().setParallelProjection(enabled);
    this.#window.getRenderer().resetCameraClippingRange();
    this.render();
  }

  public setCameraView(view: CameraView): void {
    const renderer = this.#window.getRenderer();
    const bounds = renderer.computeVisiblePropBounds();
    if (bounds.some((value) => !Number.isFinite(value))) {
      return;
    }
    const center: [number, number, number] = [
      (bounds[0] + bounds[1]) / 2,
      (bounds[2] + bounds[3]) / 2,
      (bounds[4] + bounds[5]) / 2,
    ];
    const directions: Record<CameraView, readonly [number, number, number]> = {
      isometric: [1, 1, 1], front: [0, 0, 1], back: [0, 0, -1],
      left: [-1, 0, 0], right: [1, 0, 0], top: [0, 1, 0], bottom: [0, -1, 0],
    };
    const viewUps: Record<CameraView, readonly [number, number, number]> = {
      isometric: [0, 0, 1], front: [0, 1, 0], back: [0, 1, 0],
      left: [0, 1, 0], right: [0, 1, 0], top: [0, 0, -1], bottom: [0, 0, 1],
    };
    const direction = directions[view];
    const up = viewUps[view];
    const distance = Math.max(bounds[1] - bounds[0], bounds[3] - bounds[2], bounds[5] - bounds[4], 1) * 3;
    const camera = renderer.getActiveCamera();
    camera.setFocalPoint(...center);
    camera.setPosition(
      center[0] + direction[0] * distance,
      center[1] + direction[1] * distance,
      center[2] + direction[2] * distance,
    );
    camera.setViewUp(...up);
    renderer.resetCamera(bounds);
    renderer.resetCameraClippingRange(bounds);
    this.render();
    this.#notifyCameraChanged();
  }

  public setCameraParameters(
    position: readonly [number, number, number],
    focalPoint: readonly [number, number, number],
    viewUp: readonly [number, number, number],
    parallelScale: number,
    operation: CameraOperation = 'set',
  ): void {
    const renderer = this.#window.getRenderer();
    const camera = renderer.getActiveCamera();
    camera.setPosition(
      position[0] - this.#worldOrigin[0],
      position[1] - this.#worldOrigin[1],
      position[2] - this.#worldOrigin[2],
    );
    camera.setFocalPoint(
      focalPoint[0] - this.#worldOrigin[0],
      focalPoint[1] - this.#worldOrigin[1],
      focalPoint[2] - this.#worldOrigin[2],
    );
    camera.setViewUp(...viewUp);
    camera.setParallelScale(parallelScale);
    const bounds = renderer.computeVisiblePropBounds();
    const validBounds = bounds.every((value) => Number.isFinite(value));
    if (operation === 'fit' && validBounds) renderer.resetCamera(bounds);
    else if (operation === 'adjust' && validBounds) this.#adjustCameraDistance(bounds);
    renderer.resetCameraClippingRange(validBounds ? bounds : undefined);
    this.render();
    this.#notifyCameraChanged();
  }

  #adjustCameraDistance(bounds: number[]): void {
    const camera = this.#window.getRenderer().getActiveCamera();
    const position = camera.getPosition();
    const projection = camera.getDirectionOfProjection();
    const projectionLength = Math.hypot(projection[0], projection[1], projection[2]);
    if (!(projectionLength > 0)) return;
    const direction = [projection[0] / projectionLength, projection[1] / projectionLength,
      projection[2] / projectionLength] as const;
    const center = [(bounds[0]! + bounds[1]!) / 2, (bounds[2]! + bounds[3]!) / 2,
      (bounds[4]! + bounds[5]!) / 2] as const;
    const radius = 0.525 * Math.hypot(bounds[1]! - bounds[0]!, bounds[3]! - bounds[2]!,
      bounds[5]! - bounds[4]!);
    const distanceToCenter = direction[0] * (center[0] - position[0])
      + direction[1] * (center[1] - position[1]) + direction[2] * (center[2] - position[2]);
    const motion = distanceToCenter - radius;
    const nextPosition = [position[0] + motion * direction[0], position[1] + motion * direction[1],
      position[2] + motion * direction[2]] as const;
    camera.setPosition(...nextPosition);
    camera.setFocalPoint(nextPosition[0] + direction[0], nextPosition[1] + direction[1],
      nextPosition[2] + direction[2]);
  }

  public getCameraParameters(): CameraParameters {
    const camera = this.#window.getRenderer().getActiveCamera();
    const position = camera.getPosition();
    const focalPoint = camera.getFocalPoint();
    const viewUp = camera.getViewUp();
    return {
      position: [position[0] + this.#worldOrigin[0], position[1] + this.#worldOrigin[1],
        position[2] + this.#worldOrigin[2]],
      focalPoint: [focalPoint[0] + this.#worldOrigin[0], focalPoint[1] + this.#worldOrigin[1],
        focalPoint[2] + this.#worldOrigin[2]],
      viewUp: [viewUp[0], viewUp[1], viewUp[2]],
      parallelScale: camera.getParallelScale(),
    };
  }

  public boxZoom(startClientX: number, startClientY: number, endClientX: number, endClientY: number): boolean {
    const canvas = this.#window.getContainer().querySelector('canvas');
    if (!canvas || this.#contextLost) return false;
    const bounds = canvas.getBoundingClientRect();
    const width = Math.abs(endClientX - startClientX);
    const height = Math.abs(endClientY - startClientY);
    if (width < 2 || height < 2 || bounds.width <= 0 || bounds.height <= 0) return false;
    const zoomFactor = Math.min(bounds.width / width, bounds.height / height);
    if (!Number.isFinite(zoomFactor) || zoomFactor <= 1) return false;

    const renderer = this.#window.getRenderer();
    const camera = renderer.getActiveCamera();
    const renderView = this.#window.getInteractor().getView() as {
      worldToDisplay(x: number, y: number, z: number,
        renderer: ReturnType<VtkGenericRenderWindow['getRenderer']>): [number, number, number];
      displayToWorld(x: number, y: number, z: number,
        renderer: ReturnType<VtkGenericRenderWindow['getRenderer']>): [number, number, number];
    };
    const scaleX = canvas.width / bounds.width;
    const scaleY = canvas.height / bounds.height;
    const selectionX = (((startClientX + endClientX) / 2) - bounds.left) * scaleX;
    const selectionY = (bounds.bottom - ((startClientY + endClientY) / 2)) * scaleY;
    const focalPoint = camera.getFocalPoint();
    const focalDisplay = renderView.worldToDisplay(focalPoint[0], focalPoint[1], focalPoint[2], renderer);
    const selectionCenter = renderView.displayToWorld(selectionX, selectionY, focalDisplay[2], renderer);
    const windowCenter = renderView.displayToWorld(canvas.width / 2, canvas.height / 2, focalDisplay[2], renderer);
    const motion = [selectionCenter[0] - windowCenter[0], selectionCenter[1] - windowCenter[1],
      selectionCenter[2] - windowCenter[2]] as const;
    const position = camera.getPosition();
    camera.setParallelScale(camera.getParallelScale() / zoomFactor);
    camera.setPosition(position[0] + motion[0], position[1] + motion[1], position[2] + motion[2]);
    camera.setFocalPoint(focalPoint[0] + motion[0], focalPoint[1] + motion[1], focalPoint[2] + motion[2]);
    renderer.resetCameraClippingRange();
    this.render();
    this.#notifyCameraChanged();
    return true;
  }

  #notifyCameraChanged(): void {
    this.#cameraChanged?.(this.getCameraParameters());
  }

  public setContoursVisible(visible: boolean): void {
    for (const pipeline of this.#chunks.values()) {
      pipeline.mapper.setScalarVisibility(visible && pipeline.hasScalars);
    }
    this.render();
  }

  public updateScalars(chunkId: string, values: Float32Array): void {
    const pipeline = this.#chunks.get(chunkId);
    if (!pipeline) {
      throw new Error(`Cannot update scalars for missing chunk ${chunkId}.`);
    }
    if (values.length !== pipeline.polyData.getNumberOfPoints()) {
      throw new Error(`Scalar tuple count does not match chunk ${chunkId}.`);
    }
    const scalars = pipeline.polyData.getPointData().getScalars();
    if (!scalars) {
      throw new Error(`Chunk ${chunkId} does not have an existing scalar array.`);
    }
    scalars.setData(values, 1);
    scalars.modified();
    pipeline.polyData.modified();
    pipeline.mapper.modified();
  }

  public async pick(clientX: number, clientY: number): Promise<PickResolveRequest | null> {
    const canvas = this.#window.getContainer().querySelector('canvas');
    if (!canvas || this.#contextLost) {
      return null;
    }
    const bounds = canvas.getBoundingClientRect();
    if (clientX < bounds.left || clientX > bounds.right || clientY < bounds.top || clientY > bounds.bottom) {
      return null;
    }
    const displayX = (clientX - bounds.left) * canvas.width / bounds.width;
    const displayY = (bounds.bottom - clientY) * canvas.height / bounds.height;
    const renderer = this.#window.getRenderer();
    const renderView = this.#window.getInteractor().getView() as {
      displayToWorld(x: number, y: number, z: number, renderer: ReturnType<VtkGenericRenderWindow['getRenderer']>): [number, number, number];
    };
    const near = renderView.displayToWorld(displayX, displayY, 0, renderer);
    const far = renderView.displayToWorld(displayX, displayY, 1, renderer);
    let candidateCount = 0;
    for (const pipeline of this.#chunks.values()) {
      const offset = this.#explodedOffsets.get(pipeline.actorName) ?? [0, 0, 0];
      const shiftedBounds: [number, number, number, number, number, number] = [
        pipeline.relativeBounds[0] + offset[0], pipeline.relativeBounds[1] + offset[0],
        pipeline.relativeBounds[2] + offset[1], pipeline.relativeBounds[3] + offset[1],
        pipeline.relativeBounds[4] + offset[2], pipeline.relativeBounds[5] + offset[2],
      ];
      const candidate = pipeline.pickable && segmentIntersectsBounds(near, far, shiftedBounds);
      pipeline.actor.setPickable(candidate);
      candidateCount += candidate ? 1 : 0;
    }
    if (candidateCount === 0) {
      for (const pipeline of this.#chunks.values()) {
        pipeline.actor.setPickable(pipeline.pickable);
      }
      return null;
    }
    const nodes = await this.#selector.selectAsync(
      renderer,
      Math.round(displayX),
      Math.round(displayY),
      Math.round(displayX),
      Math.round(displayY),
    );
    try {
      const node = nodes[0];
      if (!node) {
        return null;
      }
      const properties = node.getProperties();
      const pipeline = properties.prop ? this.#chunksByProp.get(properties.prop as VtkActor) : undefined;
      const primitiveIndex = properties.attributeID ?? node.getSelectionList()[0];
      const relativeWorld = properties.worldPosition;
      if (!pipeline || primitiveIndex === undefined || !relativeWorld
        || !pipeline.sourcePointIds || !pipeline.sourceElementIds) {
        return null;
      }
      const visualHit: [number, number, number] = [relativeWorld[0], relativeWorld[1], relativeWorld[2]];
      const explodedOffset = this.#explodedOffsets.get(pipeline.actorName) ?? [0, 0, 0];
      const relativeHit = subtract(visualHit, explodedOffset);
      const mapping = resolveTriangleSource(
        pipeline.connectivity,
        pipeline.positions,
        pipeline.sourcePointIds,
        pipeline.sourceElementIds,
        primitiveIndex,
        relativeHit,
      );
      const cameraDirection = this.#window.getRenderer().getActiveCamera().getDirectionOfProjection();
      const renderer = this.#window.getRenderer();
      const renderView = this.#window.getApiSpecificRenderWindow();
      const hitDisplay = renderView.worldToDisplay(visualHit[0], visualHit[1], visualHit[2], renderer);
      const sevenCssPixels = 7 * canvas.width / bounds.width;
      const precisionPoint = renderView.displayToWorld(hitDisplay[0] + sevenCssPixels,
        hitDisplay[1], hitDisplay[2], renderer);
      const selectionPrecision = Math.hypot(precisionPoint[0] - visualHit[0],
        precisionPoint[1] - visualHit[1], precisionPoint[2] - visualHit[2]);
      if (!Number.isFinite(selectionPrecision) || selectionPrecision <= 0) return null;
      return {
        actorId: pipeline.actorId,
        chunkId: pipeline.chunkId,
        renderPrimitive: 'triangle',
        primitiveIndex,
        ...mapping,
        worldPosition: [
          this.#worldOrigin[0] + relativeHit[0],
          this.#worldOrigin[1] + relativeHit[1],
          this.#worldOrigin[2] + relativeHit[2],
        ],
        displayPosition: [clientX - bounds.left, clientY - bounds.top],
        cameraDirection: [cameraDirection[0], cameraDirection[1], cameraDirection[2]],
        selectionPrecision,
        mode: 'element',
      };
    } finally {
      for (const node of nodes) {
        node.delete();
      }
      this.#selector.beginSelection();
      this.#selector.endSelection();
      this.#selector.releasePixBuffers();
      for (const pipeline of this.#chunks.values()) {
        pipeline.actor.setPickable(pipeline.pickable);
      }
    }
  }

  public async selectArea(startClientX: number, startClientY: number, endClientX: number, endClientY: number,
    completelyInside: boolean): Promise<AreaResolveRequest> {
    const canvas = this.#window.getContainer().querySelector('canvas');
    if (!canvas || this.#contextLost) {
      return {
        chunks: [], cameraDirection: [0, 0, -1], completelyInside,
        ...this.#selectionSectionPlaneProperty(),
      };
    }
    const bounds = canvas.getBoundingClientRect();
    const scaleX = canvas.width / bounds.width;
    const scaleY = canvas.height / bounds.height;
    const x1 = Math.max(0, Math.min(canvas.width - 1, Math.round((Math.min(startClientX, endClientX) - bounds.left) * scaleX)));
    const x2 = Math.max(0, Math.min(canvas.width - 1, Math.round((Math.max(startClientX, endClientX) - bounds.left) * scaleX)));
    const y1 = Math.max(0, Math.min(canvas.height - 1, Math.round((bounds.bottom - Math.max(startClientY, endClientY)) * scaleY)));
    const y2 = Math.max(0, Math.min(canvas.height - 1, Math.round((bounds.bottom - Math.min(startClientY, endClientY)) * scaleY)));
    const renderer = this.#window.getRenderer();
    const renderView = this.#window.getInteractor().getView() as {
      worldToDisplay(x: number, y: number, z: number,
        renderer: ReturnType<VtkGenericRenderWindow['getRenderer']>): [number, number, number];
    };
    for (const pipeline of this.#chunks.values()) {
      pipeline.actor.setPickable(pipeline.pickable && pipeline.actor.getVisibility());
    }
    const nodes = await this.#selector.selectAsync(renderer, x1, y1, x2, y2);
    try {
      const chunks: { actorId: string; chunkId: string; primitiveIndices: number[] }[] = [];
      let total = 0;
      for (const node of nodes) {
        const properties = node.getProperties();
        const pipeline = properties.prop ? this.#chunksByProp.get(properties.prop as VtkActor) : undefined;
        if (!pipeline) continue;
        const unique = new Set<number>();
        for (const rawIndex of node.getSelectionList()) {
          const primitiveIndex = Number(rawIndex);
          if (!Number.isInteger(primitiveIndex) || primitiveIndex < 0 ||
              primitiveIndex * 3 + 2 >= pipeline.connectivity.length) continue;
          const polygon: SectionSelectionPoint[] = [];
          for (let vertex = 0; vertex < 3; vertex++) {
            const pointIndex = pipeline.connectivity[primitiveIndex * 3 + vertex]!;
            const offset = pointIndex * 3;
            polygon.push([pipeline.positions[offset]!, pipeline.positions[offset + 1]!,
              pipeline.positions[offset + 2]!]);
          }
          const visiblePolygon = this.#sectionPoint && this.#sectionNormal
            ? clipPolygonToSection(polygon, [
              this.#sectionPoint[0] - this.#worldOrigin[0],
              this.#sectionPoint[1] - this.#worldOrigin[1],
              this.#sectionPoint[2] - this.#worldOrigin[2],
            ], this.#sectionNormal)
            : polygon;
          if (visiblePolygon.length === 0) continue;
          if (completelyInside) {
            let inside = true;
            for (const point of visiblePolygon) {
              const display = renderView.worldToDisplay(point[0], point[1], point[2], renderer);
              if (display[0] < x1 || display[0] > x2 || display[1] < y1 || display[1] > y2) {
                inside = false;
                break;
              }
            }
            if (!inside) continue;
          }
          unique.add(primitiveIndex);
        }
        if (unique.size > 0) {
          total += unique.size;
          if (total > 100_000) throw new Error('Area selection exceeds the 100000-triangle safety bound.');
          chunks.push({ actorId: pipeline.actorId, chunkId: pipeline.chunkId, primitiveIndices: [...unique] });
        }
      }
      return {
        chunks,
        cameraDirection: [...renderer.getActiveCamera().getDirectionOfProjection()] as [number, number, number],
        completelyInside,
        ...this.#selectionSectionPlaneProperty(),
      };
    } finally {
      for (const node of nodes) node.delete();
      this.#selector.beginSelection();
      this.#selector.endSelection();
      this.#selector.releasePixBuffers();
      for (const pipeline of this.#chunks.values()) pipeline.actor.setPickable(pipeline.pickable);
    }
  }

  public createFrustum(startClientX: number, startClientY: number, endClientX: number, endClientY: number,
    completelyInside: boolean): FrustumResolveRequest {
    const canvas = this.#window.getContainer().querySelector('canvas');
    if (!canvas || this.#contextLost) throw new Error('The render canvas is unavailable for frustum selection.');
    const bounds = canvas.getBoundingClientRect();
    const scaleX = canvas.width / bounds.width;
    const scaleY = canvas.height / bounds.height;
    const x1 = Math.max(0, Math.min(canvas.width - 1, (Math.min(startClientX, endClientX) - bounds.left) * scaleX));
    const x2 = Math.max(0, Math.min(canvas.width - 1, (Math.max(startClientX, endClientX) - bounds.left) * scaleX));
    const y1 = Math.max(0, Math.min(canvas.height - 1, (bounds.bottom - Math.max(startClientY, endClientY)) * scaleY));
    const y2 = Math.max(0, Math.min(canvas.height - 1, (bounds.bottom - Math.min(startClientY, endClientY)) * scaleY));
    const renderer = this.#window.getRenderer();
    const renderView = this.#window.getInteractor().getView() as {
      displayToWorld(x: number, y: number, z: number,
        renderer: ReturnType<VtkGenericRenderWindow['getRenderer']>): [number, number, number];
    };
    const corners: [number, number, number][] = [];
    for (const depth of [0, 1]) {
      for (const [x, y] of [[x1, y1], [x2, y1], [x2, y2], [x1, y2]] as [number, number][]) {
        const point = renderView.displayToWorld(x, y, depth, renderer);
        corners.push([point[0] + this.#worldOrigin[0], point[1] + this.#worldOrigin[1], point[2] + this.#worldOrigin[2]]);
      }
    }
    return {
      corners,
      cameraDirection: [...renderer.getActiveCamera().getDirectionOfProjection()] as [number, number, number],
      completelyInside,
      ...this.#selectionSectionPlaneProperty(),
    };
  }

  #selectionSectionPlaneProperty(): { sectionPlane: SelectionSectionPlane } | { sectionPlane?: never } {
    if (!this.#sectionPoint || !this.#sectionNormal) return {};
    return { sectionPlane: { point: [...this.#sectionPoint], normal: [...this.#sectionNormal] } };
  }

  public render(): void {
    if (!this.#contextLost) {
      this.#window.getRenderWindow().render();
      this.#updateAnnotationPositions();
    }
  }

  public getDiagnostics(): object {
    const canvas = this.#window.getContainer().querySelector('canvas');
    const gl = canvas?.getContext('webgl2');
    const debugRenderer = gl?.getExtension('WEBGL_debug_renderer_info') as {
      UNMASKED_VENDOR_WEBGL: number;
      UNMASKED_RENDERER_WEBGL: number;
    } | null;
    const pipelines = [...this.#chunks.values()];
    const camera = this.#window.getRenderer().getActiveCamera();
    return {
      canvasCssWidth: canvas?.clientWidth ?? null,
      canvasCssHeight: canvas?.clientHeight ?? null,
      canvasBufferWidth: canvas?.width ?? null,
      canvasBufferHeight: canvas?.height ?? null,
      webgl2Available: Boolean(canvas?.getContext('webgl2')),
      webglVendor: gl && debugRenderer ? String(gl.getParameter(debugRenderer.UNMASKED_VENDOR_WEBGL)) : null,
      webglRenderer: gl && debugRenderer ? String(gl.getParameter(debugRenderer.UNMASKED_RENDERER_WEBGL)) : null,
      rendererActors: this.#window.getRenderer().getActors().length,
      pipelineChunks: pipelines.length,
      pipelinePoints: pipelines.reduce((sum, pipeline) => sum + pipeline.polyData.getNumberOfPoints(), 0),
      pipelinePolys: pipelines.reduce((sum, pipeline) => sum + pipeline.polyData.getNumberOfPolys(), 0),
      visiblePipelineChunks: pipelines.filter((pipeline) => pipeline.actor.getVisibility()).length,
      cameraDirection: [...camera.getDirectionOfProjection()],
      cameraViewUp: [...camera.getViewUp()],
      cameraPosition: [...camera.getPosition()],
      cameraFocalPoint: [...camera.getFocalPoint()],
      cameraParallelScale: camera.getParallelScale(),
      actorColors: pipelines.map((pipeline) => ({
        actorId: pipeline.actorId,
        color: [...pipeline.actor.getProperty().getColor()],
        opacity: pipeline.actor.getProperty().getOpacity(),
      })),
      actorPositions: pipelines.map((pipeline) => ({ actorName: pipeline.actorName,
        position: [...pipeline.actor.getPosition()] })),
      topologyTokens: pipelines.map((pipeline) => pipeline.topologyToken),
      selection: {
        item: this.#selectionItem,
        selectedIdCount: this.#selectedIds.size,
        selectedActorCount: this.#selectedActorNames.size,
        selectedFaceCount: this.#selectedFaceNodeIds.length,
        selectedEdgeCount: this.#selectedEdgeNodeIds.length,
        overlayActors: this.#selectionOverlays.length,
        overlayPositions: this.#selectionOverlays.map((overlay) => [...overlay.actor.getPosition()]),
        overflow: this.#selectionOverlayOverflow,
      },
      annotations: {
        count: this.#annotationOverlays.size,
        visible: [...this.#annotationOverlays.values()].filter((annotation) => !annotation.element.hidden).length,
      },
      graphics: this.#window.getRenderWindow().getStatistics(),
      explodedPreview: {
        actorCount: this.#explodedOffsets.size,
        offsets: [...this.#explodedOffsets.entries()].map(([actorName, offset]) => ({ actorName, offset })),
      },
      sectionView: {
        active: this.#sectionPlane !== null,
        point: this.#sectionPoint ? [...this.#sectionPoint] : null,
        normal: this.#sectionNormal ? [...this.#sectionNormal] : null,
        clippedChunks: this.#sectionPlane ? pipelines.length : 0,
        pipelineClippingPlanes: pipelines.map((pipeline) => pipeline.mapper.getNumberOfClippingPlanes()),
        clippedOverlayActors: this.#selectionOverlays.filter((overlay) =>
          overlay.mapper.getNumberOfClippingPlanes() > 0).length,
        capActors: this.#sectionPipelines.length,
      },
    };
  }

  public async measureOrbit(frameCount = 60): Promise<number[]> {
    const samples: number[] = [];
    const camera = this.#window.getRenderer().getActiveCamera();
    for (let frame = 0; frame < frameCount; frame += 1) {
      const startedAt = performance.now();
      camera.azimuth(1);
      this.#window.getRenderer().resetCameraClippingRange();
      this.render();
      await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
      samples.push(performance.now() - startedAt);
    }
    return samples;
  }

  public clear(): void {
    const renderer = this.#window.getRenderer();
    this.#clearSelectionOverlays();
    this.#clearSectionGeometry();
    for (const pipeline of this.#chunks.values()) {
      renderer.removeActor(pipeline.actor);
      this.#chunksByProp.delete(pipeline.actor);
      pipeline.actor.delete();
      pipeline.mapper.delete();
      pipeline.polyData.delete();
      pipeline.sourcePointIds = null;
      pipeline.sourceElementIds = null;
    }
    this.#chunks.clear();
    this.render();
  }

  #clearSectionGeometry(): void {
    const renderer = this.#window.getRenderer();
    for (const pipeline of this.#sectionPipelines) {
      renderer.removeActor(pipeline.actor);
      pipeline.actor.delete();
      pipeline.mapper.delete();
      pipeline.polyData.delete();
    }
    this.#sectionPipelines.length = 0;
  }

  public delete(): void {
    this.#closeAnnotationContextMenu();
    this.#resizeObserver.disconnect();
    for (const subscription of this.#interactionSubscriptions) subscription.unsubscribe();
    this.#annotationOverlays.clear();
    this.#annotationLayer.remove();
    this.clear();
    this.#sectionPlane?.delete();
    this.#sectionPlane = null;
    this.#lookupTable.delete();
    this.#window.delete();
  }

  #updateAnnotationPositions(): void {
    if (this.#annotationOverlays.size === 0) return;
    for (const { state, element } of this.#annotationOverlays.values())
      this.#positionAnnotation(state, element, state.offset);
  }

  #positionAnnotation(state: HostAnnotation, element: HTMLDivElement,
                      offset: readonly [number, number]): void {
    const canvas = this.#container.querySelector('canvas');
    if (!canvas || canvas.clientWidth <= 0 || canvas.clientHeight <= 0) return;
    const renderer = this.#window.getRenderer();
    const renderView = this.#window.getApiSpecificRenderWindow();
    const scaleX = canvas.width / canvas.clientWidth;
    const scaleY = canvas.height / canvas.clientHeight;
    const point = renderView.worldToDisplay(state.anchorPoint[0] - this.#worldOrigin[0],
      state.anchorPoint[1] - this.#worldOrigin[1], state.anchorPoint[2] - this.#worldOrigin[2], renderer);
    const projected = point.every(Number.isFinite) && point[2] >= 0 && point[2] <= 1;
    element.hidden = !state.visible || !projected;
    if (!element.hidden) {
      element.style.left = `${point[0] / scaleX + offset[0]}px`;
      element.style.top = `${canvas.clientHeight - point[1] / scaleY + offset[1]}px`;
    }
  }

  #updateLookupTable(): void {
    const [minimum, maximum] = this.#field?.displayedRange ?? [0, 1];
    const span = maximum - minimum;
    this.#lookupTable.removeAllPoints();
    if (this.#colorMapName === 'Blue to Red Rainbow') {
      this.#lookupTable.addRGBPoint(minimum, 0, 0, 1);
      this.#lookupTable.addRGBPoint(minimum + span * 0.25, 0, 1, 1);
      this.#lookupTable.addRGBPoint(minimum + span * 0.5, 0, 1, 0);
      this.#lookupTable.addRGBPoint(minimum + span * 0.75, 1, 1, 0);
      this.#lookupTable.addRGBPoint(maximum, 1, 0, 0);
    } else {
      this.#lookupTable.addRGBPoint(minimum, 0.23, 0.30, 0.75);
      this.#lookupTable.addRGBPoint(minimum + span * 0.5, 0.95, 0.95, 0.95);
      this.#lookupTable.addRGBPoint(maximum, 0.71, 0.02, 0.15);
    }
    this.#lookupTable.setMappingRange(minimum, maximum);
  }

  #rebuildSelectionOverlays(): void {
    this.#clearSelectionOverlays();
    this.#selectionOverlayOverflow = false;
    if (this.#selectionItem === 'none' || this.#selectionItem === 'unsupported') return;

    if (this.#selectionItem === 'element') {
      const selectedTriangles: Array<{ pipeline: ChunkPipeline; indices: number[] }> = [];
      let triangleCount = 0;
      for (const pipeline of this.#chunks.values()) {
        if (!pipeline.sourceElementIds) continue;
        const indices: number[] = [];
        for (let index = 0; index < pipeline.sourceElementIds.length; index += 1) {
          if (this.#selectedIds.has(pipeline.sourceElementIds[index]!)) indices.push(index);
        }
        triangleCount += indices.length;
        if (triangleCount > 500_000) {
          this.#selectionOverlayOverflow = true;
          return;
        }
        if (indices.length > 0) selectedTriangles.push({ pipeline, indices });
      }
      for (const selection of selectedTriangles) this.#addElementOverlay(selection.pipeline, selection.indices);
      return;
    }

    if (this.#selectionItem === 'surface' || this.#selectionItem === 'geometry') {
      const facesByNode = new Map<number, Set<ReadonlySet<number>>>();
      for (const face of this.#selectedFaceNodeIds) {
        const faceSet = new Set(face);
        for (const nodeId of faceSet) {
          const faces = facesByNode.get(nodeId) ?? new Set<ReadonlySet<number>>();
          faces.add(faceSet);
          facesByNode.set(nodeId, faces);
        }
      }
      const selectedTriangles: Array<{ pipeline: ChunkPipeline; indices: number[] }> = [];
      let triangleCount = 0;
      for (const pipeline of this.#chunks.values()) {
        if (!pipeline.sourcePointIds) continue;
        const indices: number[] = [];
        for (let triangle = 0; triangle < pipeline.connectivity.length / 3; triangle += 1) {
          const first = pipeline.sourcePointIds[pipeline.connectivity[triangle * 3]!]!;
          const second = pipeline.sourcePointIds[pipeline.connectivity[triangle * 3 + 1]!]!;
          const third = pipeline.sourcePointIds[pipeline.connectivity[triangle * 3 + 2]!]!;
          const candidates = facesByNode.get(first);
          if (candidates) {
            for (const face of candidates) {
              if (face.has(second) && face.has(third)) {
                indices.push(triangle);
                break;
              }
            }
          }
        }
        triangleCount += indices.length;
        if (triangleCount > 500_000) {
          this.#selectionOverlayOverflow = true;
          return;
        }
        if (indices.length > 0) selectedTriangles.push({ pipeline, indices });
      }
      for (const selection of selectedTriangles) this.#addElementOverlay(selection.pipeline, selection.indices);
      if (this.#selectionItem === 'surface') return;
    }

    if (this.#selectionItem === 'geometry-edge' || this.#selectionItem === 'geometry') {
      const renderedEdges = new Set<string>();
      for (const pipeline of this.#chunks.values()) {
        if (!pipeline.sourcePointIds) continue;
        const localPoints = new Map<number, number>();
        for (let point = 0; point < pipeline.sourcePointIds.length; point += 1) {
          localPoints.set(pipeline.sourcePointIds[point]!, point);
        }
        const positions: number[] = [];
        const lines: number[] = [];
        for (const edge of this.#selectedEdgeNodeIds) {
          const ordered = edge.length === 3 ? [edge[0]!, edge[2]!, edge[1]!] : [edge[0]!, edge[1]!];
          if (ordered.some((nodeId) => !localPoints.has(nodeId))) continue;
          const key = [...edge].sort((first, second) => first - second).join(':');
          if (renderedEdges.has(key)) continue;
          renderedEdges.add(key);
          for (let segment = 0; segment < ordered.length - 1; segment += 1) {
            const pointOffset = positions.length / 3;
            for (const nodeId of [ordered[segment]!, ordered[segment + 1]!]) {
              const localPoint = localPoints.get(nodeId)!;
              positions.push(
                pipeline.positions[localPoint * 3]!,
                pipeline.positions[localPoint * 3 + 1]!,
                pipeline.positions[localPoint * 3 + 2]!,
              );
            }
            lines.push(2, pointOffset, pointOffset + 1);
          }
        }
        if (lines.length > 0) this.#addEdgeOverlay(pipeline, positions, lines);
      }
      if (this.#selectionItem === 'geometry-edge') return;
    }

    if (this.#selectionItem === 'node' || this.#selectionItem === 'geometry') {
      let selectedPointCount = 0;
      for (const pipeline of this.#chunks.values()) {
        if (!pipeline.sourcePointIds) continue;
        for (let index = 0; index < pipeline.sourcePointIds.length; index += 1) {
          if (this.#selectedIds.has(pipeline.sourcePointIds[index]!)) selectedPointCount += 1;
        }
      }
      const stride = getNodeOverlayStride(selectedPointCount);
      this.#selectionOverlayOverflow ||= stride > 1;
      let selectedOrdinal = 0;
      for (const pipeline of this.#chunks.values()) {
        if (!pipeline.sourcePointIds) continue;
        const localIndices: number[] = [];
        for (let index = 0; index < pipeline.sourcePointIds.length; index += 1) {
          if (this.#selectedIds.has(pipeline.sourcePointIds[index]!)) {
            if (selectedOrdinal % stride === 0) localIndices.push(index);
            selectedOrdinal += 1;
          }
        }
        if (localIndices.length > 0) this.#addNodeOverlay(pipeline, localIndices);
      }
    }

    if (this.#selectionItem === 'part') {
      for (const pipeline of this.#chunks.values()) {
        if (!this.#selectedActorNames.has(pipeline.actorName)) continue;
        this.#addPartOverlay(pipeline);
      }
    }
  }

  #addElementOverlay(pipeline: ChunkPipeline, triangleIndices: readonly number[]): void {
    const positions = new Float32Array(triangleIndices.length * 9);
    const lines = new Uint32Array(triangleIndices.length * 5);
    for (let selectionIndex = 0; selectionIndex < triangleIndices.length; selectionIndex += 1) {
      const triangleIndex = triangleIndices[selectionIndex]!;
      for (let vertex = 0; vertex < 3; vertex += 1) {
        const sourcePoint = pipeline.connectivity[triangleIndex * 3 + vertex]!;
        positions.set(pipeline.positions.subarray(sourcePoint * 3, sourcePoint * 3 + 3), selectionIndex * 9 + vertex * 3);
      }
      const pointOffset = selectionIndex * 3;
      lines.set([4, pointOffset, pointOffset + 1, pointOffset + 2, pointOffset], selectionIndex * 5);
    }
    const polyData = vtkPolyData.newInstance();
    polyData.getPoints().setData(positions, 3);
    polyData.getLines().setData(lines);
    this.#addSelectionActor(pipeline, polyData, (actor) => {
      actor.getProperty().setColor(1, 0.55, 0);
      actor.getProperty().setLineWidth(4);
      actor.getProperty().setLighting(false);
    });
  }

  #addNodeOverlay(pipeline: ChunkPipeline, localIndices: readonly number[]): void {
    const positions = new Float32Array(localIndices.length * 3);
    const vertices = new Uint32Array(localIndices.length * 2);
    for (let selectionIndex = 0; selectionIndex < localIndices.length; selectionIndex += 1) {
      const localIndex = localIndices[selectionIndex]!;
      positions.set(pipeline.positions.subarray(localIndex * 3, localIndex * 3 + 3), selectionIndex * 3);
      vertices.set([1, selectionIndex], selectionIndex * 2);
    }
    const polyData = vtkPolyData.newInstance();
    polyData.getPoints().setData(positions, 3);
    polyData.getVerts().setData(vertices);
    this.#addSelectionActor(pipeline, polyData, (actor) => {
      actor.getProperty().setColor(1, 0.25, 0);
      actor.getProperty().setPointSize(12);
      actor.getProperty().setLighting(false);
    });
  }

  #addEdgeOverlay(pipeline: ChunkPipeline, positions: readonly number[], lines: readonly number[]): void {
    const polyData = vtkPolyData.newInstance();
    polyData.getPoints().setData(new Float32Array(positions), 3);
    polyData.getLines().setData(new Uint32Array(lines));
    this.#addSelectionActor(pipeline, polyData, (actor) => {
      actor.getProperty().setColor(1, 0.35, 0);
      actor.getProperty().setLineWidth(5);
      actor.getProperty().setLighting(false);
    });
  }

  #addPartOverlay(pipeline: ChunkPipeline): void {
    this.#addSelectionActor(pipeline, pipeline.polyData, (actor) => {
      actor.getProperty().setColor(1, 0.55, 0);
      actor.getProperty().setLineWidth(3);
      actor.getProperty().setRepresentationToWireframe();
      actor.getProperty().setLighting(false);
    }, false);
  }

  #addSelectionActor(
    pipeline: ChunkPipeline,
    polyData: VtkPolyData,
    configure: (actor: VtkActor) => void,
    ownsPolyData = true,
  ): void {
    const mapper = vtkMapper.newInstance({ scalarVisibility: false });
    mapper.setInputData(polyData);
    if (this.#sectionPlane) mapper.addClippingPlane(this.#sectionPlane);
    const actor = vtkActor.newInstance();
    actor.setMapper(mapper);
    actor.setPickable(false);
    actor.setVisibility(pipeline.actor.getVisibility());
    actor.setPosition(...(this.#explodedOffsets.get(pipeline.actorName) ?? [0, 0, 0]));
    configure(actor);
    this.#window.getRenderer().addActor(actor);
    this.#selectionOverlays.push({ actorId: pipeline.actorId, actor, mapper, polyData: ownsPolyData ? polyData : null });
  }

  #clearSelectionOverlays(): void {
    const renderer = this.#window.getRenderer();
    for (const overlay of this.#selectionOverlays) {
      renderer.removeActor(overlay.actor);
      overlay.actor.delete();
      overlay.mapper.delete();
      overlay.polyData?.delete();
    }
    this.#selectionOverlays.length = 0;
  }
}

function isReservedAnnotation(name: string): boolean {
  return name === 'Measure_Annotation' || name === 'Min_Annotation' || name === 'Max_Annotation';
}

function segmentIntersectsBounds(
  start: readonly [number, number, number],
  end: readonly [number, number, number],
  bounds: readonly [number, number, number, number, number, number],
): boolean {
  let minimumT = 0;
  let maximumT = 1;
  for (let axis = 0; axis < 3; axis += 1) {
    const origin = start[axis]!;
    const direction = end[axis]! - origin;
    const lower = bounds[axis * 2]!;
    const upper = bounds[axis * 2 + 1]!;
    if (Math.abs(direction) < 1e-12) {
      if (origin < lower || origin > upper) return false;
      continue;
    }
    const first = (lower - origin) / direction;
    const second = (upper - origin) / direction;
    minimumT = Math.max(minimumT, Math.min(first, second));
    maximumT = Math.min(maximumT, Math.max(first, second));
    if (minimumT > maximumT) return false;
  }
  return true;
}
