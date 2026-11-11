import '@kitware/vtk.js/Rendering/Profiles/Geometry';
import '@kitware/vtk.js/Rendering/Profiles/Glyph';
import vtkDataArray from '@kitware/vtk.js/Common/Core/DataArray';
import vtkPlane, { type vtkPlane as VtkPlane } from '@kitware/vtk.js/Common/DataModel/Plane';
import vtkPolyData from '@kitware/vtk.js/Common/DataModel/PolyData';
import vtkSphereSource from '@kitware/vtk.js/Filters/Sources/SphereSource';
import vtkArrowSource from '@kitware/vtk.js/Filters/Sources/ArrowSource';
import vtkConeSource from '@kitware/vtk.js/Filters/Sources/ConeSource';
import vtkCylinderSource from '@kitware/vtk.js/Filters/Sources/CylinderSource';
import vtkAppendPolyData from '@kitware/vtk.js/Filters/General/AppendPolyData';
import vtkTubeFilter from '@kitware/vtk.js/Filters/General/TubeFilter';
import vtkTriangleFilter from '@kitware/vtk.js/Filters/General/TriangleFilter';
import { FieldAssociations } from '@kitware/vtk.js/Common/DataModel/DataSet/Constants';
import vtkActor, { type vtkActor as VtkActor } from '@kitware/vtk.js/Rendering/Core/Actor';
import vtkAxesActor from '@kitware/vtk.js/Rendering/Core/AxesActor';
import vtkColorTransferFunction from '@kitware/vtk.js/Rendering/Core/ColorTransferFunction';
import vtkGlyph3DMapper, { type vtkGlyph3DMapper as VtkGlyph3DMapper } from '@kitware/vtk.js/Rendering/Core/Glyph3DMapper';
import vtkMapper, { type vtkMapper as VtkMapper } from '@kitware/vtk.js/Rendering/Core/Mapper';
import type { vtkHardwareSelector as VtkHardwareSelector } from '@kitware/vtk.js/Rendering/Core/HardwareSelector';
import vtkGenericRenderWindow, { type vtkGenericRenderWindow as VtkGenericRenderWindow } from '@kitware/vtk.js/Rendering/Misc/GenericRenderWindow';
import vtkOrientationMarkerWidget from '@kitware/vtk.js/Interaction/Widgets/OrientationMarkerWidget';
import type { vtkPolyData as VtkPolyData } from '@kitware/vtk.js/Common/DataModel/PolyData';
import type { LoadedAttributes, LoadedGeometry } from './chunkLoader';
import type { AreaResolveRequest, FrustumResolveRequest, HostAnnotation, HostAnnotationAction, HostCaption, HostColorBarMessage, HostCoordinateAxis, HostDirectionalGlyph, HostGraphicsInteractionMessage, HostResultWidgetLayoutMessage, HostScalarLegendMessage, HostSphereSymbol, HostStatusBlockMessage, HostTransientPrimitive, HostViewportAppearanceMessage, PickResolveRequest, ScalarFieldMetadata, SelectionSectionPlane } from './protocol';
import { toVtkLineCells, toVtkTriangleCells } from './resourceDecoder';
import type { SectionGeometryActor } from './sectionGeometryBinary';
import { intersectPickedTriangle, resolveTriangleSource } from './pickResolver';
import { getNodeOverlayStride } from './selectionOverlay';
import { clipPolygonToSection, type SectionSelectionPoint } from './sectionSelection';
import { toRelativeTransformationMatrix } from './hostTransformations';

interface ChunkPipeline {
  readonly actorId: string;
  actorName: string;
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

interface TransformedActorPipeline {
  readonly actor: VtkActor;
  readonly source: ChunkPipeline;
}

interface AnnotationOverlay {
  readonly state: HostAnnotation;
  readonly element: HTMLDivElement;
}

interface SphereSymbolPipeline {
  readonly actor: VtkActor;
  readonly mapper: VtkGlyph3DMapper;
  readonly polyData: VtkPolyData;
  readonly scaleArray: ReturnType<typeof vtkDataArray.newInstance>;
  readonly relativePoints: Float32Array;
  readonly scales: Float32Array;
  readonly symbolSize: number;
}

interface TransientPrimitivePipeline {
  readonly actorName: string;
  readonly actor: VtkActor;
  readonly mapper: VtkMapper;
  readonly polyData: VtkPolyData;
  readonly pointCount: number;
  readonly lineCount: number;
}

interface CoordinateAxisPipeline extends SphereSymbolPipeline {
  readonly directionArray: ReturnType<typeof vtkDataArray.newInstance>;
}

interface DirectionalGlyphPipeline extends SphereSymbolPipeline {
  readonly kind: HostDirectionalGlyph['kind'];
  readonly invert: boolean | undefined;
  readonly center: boolean | undefined;
  readonly directionArray: ReturnType<typeof vtkDataArray.newInstance>;
  readonly ownedSources: readonly { delete(): void }[];
  readonly sourceData: VtkPolyData | null;
}

interface CaptionOverlay {
  readonly state: HostCaption;
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
interface DisplayCoordinateRenderView {
  worldToDisplay(x: number, y: number, z: number,
    renderer: ReturnType<VtkGenericRenderWindow['getRenderer']>): [number, number, number];
  displayToWorld(x: number, y: number, z: number,
    renderer: ReturnType<VtkGenericRenderWindow['getRenderer']>): [number, number, number];
}

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

function createCoordinateAxisSource(): VtkPolyData {
  const resolution = 31;
  const points = new Float32Array((resolution + 4) * 3);
  points.set([0, 0, 0, 1, 0, 0, 1, 0, 0, 0.8, 0, 0]);
  for (let index = 0; index < resolution; index += 1) {
    const angle = 2 * Math.PI * index / resolution;
    points.set([0.8, 0.08 * Math.cos(angle), 0.08 * Math.sin(angle)], (index + 4) * 3);
  }
  const polys = new Uint32Array(resolution * 8);
  for (let index = 0; index < resolution; index += 1) {
    const next = (index + 1) % resolution;
    polys.set([3, 2, index + 4, next + 4, 3, 3, next + 4, index + 4], index * 8);
  }
  const source = vtkPolyData.newInstance();
  source.getPoints().setData(points, 3);
  source.getLines().setData(new Uint32Array([2, 0, 1]));
  source.getPolys().setData(polys);
  return source;
}

function createDisplacementConeSource(): VtkPolyData {
  const resolution = 31;
  const points = new Float32Array((resolution + 2) * 3);
  points.set([0, 0, 0, -1, 0, 0]);
  for (let index = 0; index < resolution; index += 1) {
    const angle = 2 * Math.PI * index / resolution;
    points.set([-1, 0.5 * Math.cos(angle), 0.5 * Math.sin(angle)], (index + 2) * 3);
  }
  const polys = new Uint32Array(resolution * 8);
  for (let index = 0; index < resolution; index += 1) {
    const next = (index + 1) % resolution;
    polys.set([3, 0, index + 2, next + 2, 3, 1, next + 2, index + 2], index * 8);
  }
  const source = vtkPolyData.newInstance();
  source.getPoints().setData(points, 3);
  source.getPolys().setData(polys);
  return source;
}

function createGeneralConeSource(): VtkPolyData {
  const resolution = 11;
  const points = new Float32Array((resolution + 2) * 3);
  points.set([0, 0, 0, -1, 0, 0]);
  for (let index = 0; index < resolution; index += 1) {
    const angle = 2 * Math.PI * index / resolution;
    points.set([-1, 0.35 * Math.cos(angle), 0.35 * Math.sin(angle)], (index + 2) * 3);
  }
  const polys = new Uint32Array(resolution * 8);
  for (let index = 0; index < resolution; index += 1) {
    const next = (index + 1) % resolution;
    polys.set([3, 0, index + 2, next + 2, 3, 1, next + 2, index + 2], index * 8);
  }
  const source = vtkPolyData.newInstance();
  source.getPoints().setData(points, 3);
  source.getPolys().setData(polys);
  return source;
}

function createRotationalConstraintSource(): VtkPolyData {
  const xMin = -1.75; const xMax = -1.35; const yMin = -0.45; const yMax = 0.45;
  const zMin = -0.45; const zMax = 0.45;
  const points = new Float32Array([
    xMax, yMin, zMin, xMax, yMax, zMin, xMax, yMax, zMax, xMax, yMin, zMax,
    xMin, yMin, zMin, xMin, yMin, zMax, xMin, yMax, zMax, xMin, yMax, zMin,
    xMin, yMax, zMin, xMin, yMax, zMax, xMax, yMax, zMax, xMax, yMax, zMin,
    xMin, yMin, zMin, xMax, yMin, zMin, xMax, yMin, zMax, xMin, yMin, zMax,
    xMin, yMin, zMax, xMax, yMin, zMax, xMax, yMax, zMax, xMin, yMax, zMax,
    xMin, yMin, zMin, xMin, yMax, zMin, xMax, yMax, zMin, xMax, yMin, zMin,
    0, 0, 0, -1.3, 0, 0,
  ]);
  const polys = new Uint32Array(6 * 8);
  for (let face = 0; face < 6; face += 1) {
    const offset = face * 4;
    polys.set([3, offset, offset + 1, offset + 2, 3, offset, offset + 2, offset + 3], face * 8);
  }
  const source = vtkPolyData.newInstance();
  source.getPoints().setData(points, 3);
  source.getPolys().setData(polys);
  source.getLines().setData(new Uint32Array([2, 24, 25]));
  return source;
}

function createSpringSource(): { readonly data: VtkPolyData; readonly owners: readonly { delete(): void }[] } {
  const diameter = 0.2;
  const height = 1;
  const turns = 8;
  const radialResolution = 10;
  const linearResolution = 3;
  const radius = diameter / 2;
  const linearPart = diameter;
  const heightDelta = (height - 2 * linearPart) / (turns * radialResolution);
  const raw: [number, number, number][] = [];
  for (let index = 0; index < linearResolution; index += 1)
    raw.push([linearPart * index / linearResolution, 0, 0]);
  for (let index = 0; index < linearResolution; index += 1)
    raw.push([linearPart, radius * index / linearResolution, 0]);
  let phi = 0;
  for (let index = 0; index <= turns * radialResolution; index += 1) {
    raw.push([linearPart + heightDelta * index, radius * Math.cos(phi), radius * Math.sin(phi)]);
    phi += Math.PI / radialResolution;
  }
  for (let index = 0; index < linearResolution; index += 1)
    raw.push([height - linearPart, radius - radius * (index + 1) / linearResolution, 0]);
  for (let index = 0; index < linearResolution; index += 1)
    raw.push([height - linearPart + linearPart * (index + 1) / linearResolution, 0, 0]);

  const points = new Float32Array(raw.length * 3);
  for (let index = 0; index < raw.length; index += 1) {
    const smoothed: readonly number[] = index === 0 || index === raw.length - 1 ? raw[index]!
      : [
          (raw[index - 1]![0] + raw[index]![0] + raw[index + 1]![0]) / 3,
          (raw[index - 1]![1] + raw[index]![1] + raw[index + 1]![1]) / 3,
          (raw[index - 1]![2] + raw[index]![2] + raw[index + 1]![2]) / 3,
        ];
    points.set(smoothed, index * 3);
  }
  const lines = new Uint32Array(raw.length + 1);
  lines[0] = raw.length;
  for (let index = 0; index < raw.length; index += 1) lines[index + 1] = index;
  const lineData = vtkPolyData.newInstance();
  lineData.getPoints().setData(points, 3);
  lineData.getLines().setData(lines);
  const tube = vtkTubeFilter.newInstance({ radius: 0.03, numberOfSides: 10, capping: true,
    useDefaultNormal: false });
  tube.setInputData(lineData);
  tube.update();
  // vtk.js TriangleFilter currently ignores triangle strips, unlike the VTK 5.8 filter used by ActiViz.
  // Expand the tube strips explicitly while retaining the tube filter's shared points and normals.
  const data = tube.getOutputData();
  const strips = data.getStrips().getData();
  const triangleCells: number[] = [];
  for (let cell = 0; cell < strips.length;) {
    const count = strips[cell]!;
    for (let index = 0; index < count - 2; index += 1) {
      const first = strips[cell + index + 1]!;
      const second = strips[cell + index + 2]!;
      const third = strips[cell + index + 3]!;
      triangleCells.push(3, ...(index % 2 === 0 ? [first, second, third] : [second, first, third]));
    }
    cell += count + 1;
  }
  data.getPolys().setData(new Uint32Array(triangleCells));
  data.getStrips().setData(new Uint32Array());
  const values = data.getPoints().getData();
  for (let index = 0; index < values.length; index += 3) values[index] = values[index]! - 1.05;
  data.getPoints().modified();
  return { data, owners: [tube, lineData] };
}

function combineTriangulatedSources(sources: readonly VtkPolyData[], translationX: number): VtkPolyData {
  const pointCount = sources.reduce((sum, source) => sum + source.getNumberOfPoints(), 0);
  const points = new Float32Array(pointCount * 3);
  const normals = new Float32Array(pointCount * 3);
  const cells: number[] = [];
  let pointOffset = 0;
  for (const source of sources) {
    const sourcePoints = source.getPoints().getData();
    const sourceNormals = source.getPointData().getNormals()?.getData();
    for (let index = 0; index < source.getNumberOfPoints(); index += 1) {
      const offset = index * 3;
      const target = (pointOffset + index) * 3;
      points[target] = sourcePoints[offset]! + translationX;
      points[target + 1] = sourcePoints[offset + 1]!;
      points[target + 2] = sourcePoints[offset + 2]!;
      if (sourceNormals) {
        normals[target] = sourceNormals[offset]!;
        normals[target + 1] = sourceNormals[offset + 1]!;
        normals[target + 2] = sourceNormals[offset + 2]!;
      }
    }
    const sourceCells = source.getPolys().getData();
    for (let cell = 0; cell < sourceCells.length;) {
      const count = sourceCells[cell]!;
      for (let index = 1; index < count - 1; index += 1)
        cells.push(3, pointOffset + sourceCells[cell + 1]!, pointOffset + sourceCells[cell + index + 1]!,
          pointOffset + sourceCells[cell + index + 2]!);
      cell += count + 1;
    }
    pointOffset += source.getNumberOfPoints();
  }
  const data = vtkPolyData.newInstance();
  data.getPoints().setData(points, 3);
  data.getPolys().setData(new Uint32Array(cells));
  data.getPointData().setNormals(vtkDataArray.newInstance({ name: 'Normals', numberOfComponents: 3, values: normals }));
  return data;
}

function createThermalSource(invert: boolean): { readonly data: VtkPolyData;
  readonly owners: readonly { delete(): void }[] } {
  const sphere = vtkSphereSource.newInstance({ radius: 0.2, phiResolution: 11, thetaResolution: 11,
    center: [0.2, 0, 0] });
  const cylinder = vtkCylinderSource.newInstance({ radius: 0.1, height: 0.8, resolution: 7,
    center: [0, 0.7, 0] });
  sphere.update();
  cylinder.update();
  const cylinderData = cylinder.getOutputData();
  const cylinderPoints = cylinderData.getPoints().getData();
  const cylinderNormals = cylinderData.getPointData().getNormals()?.getData();
  for (let index = 0; index < cylinderData.getNumberOfPoints(); index += 1) {
    const offset = index * 3;
    const x = cylinderPoints[offset]!;
    cylinderPoints[offset] = cylinderPoints[offset + 1]!;
    cylinderPoints[offset + 1] = -x;
    if (cylinderNormals) {
      const normalX = cylinderNormals[offset]!;
      cylinderNormals[offset] = cylinderNormals[offset + 1]!;
      cylinderNormals[offset + 1] = -normalX;
    }
  }
  cylinderData.getPoints().modified();
  cylinderData.getPointData().getNormals()?.modified();
  const data = combineTriangulatedSources([sphere.getOutputData(), cylinderData], invert ? -1.05 : 0.05);
  return { data, owners: [data, cylinder, sphere] };
}

function createFluxSource(center: boolean, invert: boolean): { readonly data: VtkPolyData;
  readonly owners: readonly { delete(): void }[] } {
  const sphere = vtkSphereSource.newInstance({ radius: 0.2, phiResolution: 9, thetaResolution: 11,
    center: [center ? 0 : 0.2, 0, 0] });
  sphere.update();
  const data = combineTriangulatedSources([sphere.getOutputData()], invert ? -1.05 : 0.05);
  return { data, owners: [data, sphere] };
}

function worldPerPixelAt(camera: { getParallelProjection(): boolean; getParallelScale(): number;
  getPosition(): number[]; getDirectionOfProjection(): number[]; getViewAngle(): number },
  height: number, relativePoint: ArrayLike<number>): number {
  if (camera.getParallelProjection()) return 2 * camera.getParallelScale() / height;
  const cameraPosition = camera.getPosition();
  const direction = camera.getDirectionOfProjection();
  const depth = Math.max(1e-9, (relativePoint[0]! - cameraPosition[0]!) * direction[0]!
    + (relativePoint[1]! - cameraPosition[1]!) * direction[1]!
    + (relativePoint[2]! - cameraPosition[2]!) * direction[2]!);
  return 2 * Math.tan(camera.getViewAngle() * Math.PI / 360) * depth / height;
}

function updateScreenScaledPipelines(window: VtkGenericRenderWindow,
                                     pipelines: readonly SphereSymbolPipeline[]): void {
  if (pipelines.length === 0) return;
  const canvas = window.getContainer().querySelector('canvas');
  if (!canvas || canvas.clientHeight <= 0) return;
  const camera = window.getRenderer().getActiveCamera();
  for (const pipeline of pipelines) {
    for (let index = 0; index < pipeline.scales.length; index += 1) {
      const offset = index * 3;
      pipeline.scales[index] = worldPerPixelAt(camera, canvas.clientHeight,
        pipeline.relativePoints.subarray(offset, offset + 3)) * pipeline.symbolSize;
    }
    pipeline.scaleArray.modified();
  }
}

function formatLegendValue(value: number, format: string): string {
  const digits = Number.parseInt(format.slice(1), 10);
  let result: string;
  if (format[0] === 'E') result = value.toExponential(digits - 1).replace('e', 'E');
  else {
    const magnitude = value === 0 ? 0 : Math.floor(Math.log10(Math.abs(value)));
    result = magnitude < -4 || magnitude >= digits
      ? value.toExponential(digits - 1).replace('e', 'E')
      : Number(value.toPrecision(digits)).toString();
  }
  if (value >= 0) return `+${result}`;
  return `- ${result.slice(1)}`;
}

export class VtkSceneView {
  readonly #window: VtkGenericRenderWindow;
  readonly #orientationAxes = vtkAxesActor.newInstance();
  readonly #orientationMarker: ReturnType<typeof vtkOrientationMarkerWidget.newInstance>;
  readonly #lookupTable = vtkColorTransferFunction.newInstance();
  readonly #sphereSource = vtkSphereSource.newInstance({ radius: 0.2, phiResolution: 13, thetaResolution: 13 });
  readonly #coordinateAxisSource = createCoordinateAxisSource();
  readonly #displacementConeSource = createDisplacementConeSource();
  readonly #generalConeSource = createGeneralConeSource();
  readonly #rotationalConstraintSource = createRotationalConstraintSource();
  readonly #springSource = createSpringSource();
  readonly #chunks = new Map<string, ChunkPipeline>();
  readonly #chunksByProp = new Map<VtkActor, ChunkPipeline>();
  readonly #selector: ConcreteHardwareSelector;
  readonly #selectionOverlays: SelectionOverlay[] = [];
  readonly #highlightOverlays: SelectionOverlay[] = [];
  readonly #sectionPipelines: SectionPipeline[] = [];
  readonly #transformedActorPipelines: TransformedActorPipeline[] = [];
  readonly #container: HTMLElement;
  readonly #annotationLayer: HTMLDivElement;
  readonly #scaleWidget: HTMLDivElement;
  readonly #scaleWidgetBar: HTMLDivElement;
  readonly #scaleWidgetLabels: readonly HTMLSpanElement[];
  readonly #scalarLegend: HTMLDivElement;
  readonly #scalarLegendColors: HTMLDivElement;
  readonly #scalarLegendLabels: HTMLDivElement;
  readonly #scalarLegendText: HTMLDivElement;
  readonly #colorBar: HTMLDivElement;
  readonly #colorBarEntries: HTMLDivElement;
  readonly #statusBlock: HTMLDivElement;
  readonly #annotationOverlays = new Map<string, AnnotationOverlay>();
  readonly #sphereSymbolPipelines: SphereSymbolPipeline[] = [];
  readonly #transientPrimitivePipelines: TransientPrimitivePipeline[] = [];
  readonly #coordinateAxisPipelines: CoordinateAxisPipeline[] = [];
  readonly #directionalGlyphPipelines: DirectionalGlyphPipeline[] = [];
  readonly #captionOverlays = new Map<string, CaptionOverlay>();
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
  #highlightedActorNames = new Set<string>();
  #contoursVisible = true;
  readonly #actorColorContours = new Map<string, boolean>();
  #selectedFaceNodeIds: readonly (readonly number[])[] = [];
  #selectedEdgeNodeIds: readonly (readonly number[])[] = [];
  #selectionOverlayOverflow = false;
  #followerView: FollowerViewState | null = null;
  readonly #explodedOffsets = new Map<string, [number, number, number]>();
  #explodedAnimationGeneration = 0;
  #transformationMatrices: readonly (readonly number[])[] = [];
  #transformedActorsVisible = true;
  #sectionPlane: VtkPlane | null = null;
  #sectionPoint: [number, number, number] | null = null;
  #sectionNormal: [number, number, number] | null = null;
  #viewportAppearance: HostViewportAppearanceMessage = {
    type: 'prepomax.viewport-appearance.updated-v1', gradient: false,
    topRgb: [0.87, 0.91, 0.95], bottomRgb: [0.87, 0.91, 0.95],
    ambient: 0, diffuse: 1, redraw: false,
  };
  #scaleWidgetVisible = true;
  #scaleWidgetUnit = '';
  #scaleWidgetWorldLength = 0;
  #hostScalarLegend: HostScalarLegendMessage | null = null;
  #hostColorBar: HostColorBarMessage | null = null;
  #hostStatusBlock: HostStatusBlockMessage | null = null;
  #hostResultWidgetLayout: HostResultWidgetLayoutMessage | null = null;
  #graphicsInteraction: HostGraphicsInteractionMessage = {
    type: 'prepomax.graphics-interaction.updated-v1', pointSmoothing: true, lineSmoothing: true,
    primaryHighlightRgb: [1, 0, 0], secondaryHighlightRgb: [0.93, 0.51, 0.93],
    mouseHighlightRgb: [1, 0.65, 0],
  };

  public constructor(container: HTMLElement, onContextError: (message: string) => void,
                     annotationInteractions: AnnotationInteractionCallbacks | null = null,
                     cameraChanged: ((camera: CameraParameters) => void) | null = null) {
    this.#container = container;
    this.#annotationInteractions = annotationInteractions;
    this.#cameraChanged = cameraChanged;
    this.#window = vtkGenericRenderWindow.newInstance({ background: [0.87, 0.91, 0.95] });
    this.#window.setContainer(container);
    this.#window.resize();
    this.#orientationAxes.setXAxisColor([180, 4, 38]);
    this.#orientationAxes.setYAxisColor([33, 206, 38]);
    this.#orientationAxes.setZAxisColor([59, 76, 192]);
    this.#orientationAxes.update();
    this.#orientationMarker = vtkOrientationMarkerWidget.newInstance({
      actor: this.#orientationAxes,
      interactor: this.#window.getInteractor(),
      parentRenderer: this.#window.getRenderer(),
      viewportCorner: vtkOrientationMarkerWidget.Corners.BOTTOM_LEFT,
      viewportSize: 1,
      minPixelSize: 50,
      maxPixelSize: 200,
    });
    this.#orientationMarker.setEnabled(true);
    this.#selector = this.#window.getInteractor().getView().getSelector() as ConcreteHardwareSelector;
    this.#selector.setFieldAssociation(FieldAssociations.FIELD_ASSOCIATION_CELLS);
    // Only primitive IDs are needed; the hit is recovered from triangle geometry.
    this.#selector.setCaptureZValues(false);
    this.#annotationLayer = document.createElement('div');
    this.#annotationLayer.className = 'annotation-layer';
    container.appendChild(this.#annotationLayer);
    this.#scaleWidget = document.createElement('div');
    this.#scaleWidget.className = 'scale-widget';
    this.#scaleWidget.setAttribute('aria-hidden', 'true');
    const scaleLabels = document.createElement('div');
    scaleLabels.className = 'scale-widget-labels';
    this.#scaleWidgetLabels = Array.from({ length: 6 }, () => {
      const label = document.createElement('span');
      scaleLabels.appendChild(label);
      return label;
    });
    this.#scaleWidgetBar = document.createElement('div');
    this.#scaleWidgetBar.className = 'scale-widget-bar';
    for (let index = 0; index < 5; index += 1) {
      const segment = document.createElement('span');
      if ((index & 1) === 0) segment.className = 'filled';
      this.#scaleWidgetBar.appendChild(segment);
    }
    this.#scaleWidget.append(scaleLabels, this.#scaleWidgetBar);
    container.appendChild(this.#scaleWidget);
    this.#scalarLegend = document.createElement('div');
    this.#scalarLegend.className = 'scalar-legend';
    this.#scalarLegend.setAttribute('aria-label', 'Scalar legend');
    this.#scalarLegend.hidden = true;
    const scalarLegendBody = document.createElement('div');
    scalarLegendBody.className = 'scalar-legend-body';
    this.#scalarLegendColors = document.createElement('div');
    this.#scalarLegendColors.className = 'scalar-legend-colors';
    this.#scalarLegendLabels = document.createElement('div');
    this.#scalarLegendLabels.className = 'scalar-legend-labels';
    scalarLegendBody.append(this.#scalarLegendColors, this.#scalarLegendLabels);
    this.#scalarLegendText = document.createElement('div');
    this.#scalarLegendText.className = 'scalar-legend-text';
    this.#scalarLegend.append(scalarLegendBody, this.#scalarLegendText);
    container.appendChild(this.#scalarLegend);
    this.#colorBar = document.createElement('div');
    this.#colorBar.className = 'categorical-color-bar';
    this.#colorBar.setAttribute('aria-label', 'Categorical color bar');
    this.#colorBar.hidden = true;
    this.#colorBarEntries = document.createElement('div');
    this.#colorBarEntries.className = 'categorical-color-bar-entries';
    this.#colorBar.appendChild(this.#colorBarEntries);
    container.appendChild(this.#colorBar);
    this.#statusBlock = document.createElement('div');
    this.#statusBlock.className = 'status-block';
    this.#statusBlock.setAttribute('aria-label', 'Result status');
    this.#statusBlock.hidden = true;
    container.appendChild(this.#statusBlock);
    const interactor = this.#window.getInteractor();
    this.#interactionSubscriptions = [
      interactor.onAnimation(() => {
        this.#updateSphereSymbolScales();
        this.#updateCoordinateOverlayPositions();
        this.#updateDirectionalGlyphScales();
        this.#updateAnnotationPositions();
        this.#updateScaleWidget();
      }),
      interactor.onEndAnimation(() => this.#notifyCameraChanged()),
    ];
    this.#resizeObserver = new ResizeObserver(() => {
      this.#window.resize();
      this.#updateSphereSymbolScales();
      this.#updateCoordinateOverlayPositions();
      this.#updateDirectionalGlyphScales();
      this.#updateAnnotationPositions();
      this.#updateScaleWidget();
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
    const range = this.#displayedScalarRange();
    for (const pipeline of this.#chunks.values()) pipeline.mapper.setScalarRange(range);
    for (const pipeline of this.#sectionPipelines) pipeline.mapper.setScalarRange(range);
  }

  public setWorldOrigin(origin: readonly [number, number, number]): void {
    this.#worldOrigin = [...origin];
  }

  public setOrientationMarkerVisible(visible: boolean): void {
    this.#orientationMarker.setEnabled(visible);
    this.render();
  }

  public setScaleWidget(visible: boolean, unit: string): void {
    this.#scaleWidgetVisible = visible;
    this.#scaleWidgetUnit = unit;
    this.#scaleWidget.hidden = !visible;
    this.#updateScaleWidget();
  }

  public setScalarLegend(state: HostScalarLegendMessage): void {
    this.#hostScalarLegend = { ...state, colors: state.colors.map((color) => [...color]) };
    this.#scalarLegend.hidden = !state.visible;
    this.#scalarLegend.classList.toggle('with-background', state.drawBackground);
    this.#scalarLegend.classList.toggle('with-border', state.drawBorder);
    this.#scalarLegendColors.replaceChildren(...[...state.colors].reverse().map((color) => {
      const segment = document.createElement('span');
      segment.style.backgroundColor = `rgb(${color.map((channel) => Math.round(channel * 255)).join(' ')})`;
      return segment;
    }));
    const span = state.maximum - state.minimum;
    this.#scalarLegendLabels.replaceChildren(...Array.from({ length: state.colors.length + 1 }, (_, index) => {
      const label = document.createElement('span');
      label.textContent = formatLegendValue(state.maximum - span * index / state.colors.length,
        state.numberFormat);
      return label;
    }));
    const textLines = [];
    if (state.complexComponent) textLines.push(`Complex: ${state.complexComponent}`);
    textLines.push(`${state.fieldName.replace(/[_-]/gu, ' ')}: ${state.componentName.replace(/[_-]/gu, ' ')}`);
    textLines.push(`Unit: ${state.unit}`);
    textLines.push(state.minMaxType);
    this.#scalarLegendText.textContent = textLines.join('\n');
    this.#updateLookupTable();
    const range: [number, number] = [state.minimum, state.maximum];
    for (const pipeline of this.#chunks.values()) {
      pipeline.mapper.setLookupTable(this.#lookupTable);
      pipeline.mapper.setScalarRange(range);
    }
    for (const pipeline of this.#sectionPipelines) {
      pipeline.mapper.setLookupTable(this.#lookupTable);
      pipeline.mapper.setScalarRange(range);
    }
    this.render();
  }

  public setColorBar(state: HostColorBarMessage): void {
    this.#hostColorBar = {
      ...state, colors: state.colors.map((color) => [...color]), labels: [...state.labels],
    };
    this.#colorBar.hidden = !state.visible;
    this.#colorBar.classList.toggle('with-background', state.drawBackground);
    this.#colorBar.classList.toggle('with-border', state.drawBorder);
    this.#colorBarEntries.replaceChildren(...state.labels.map((text, index) => {
      const entry = document.createElement('div');
      entry.className = 'categorical-color-bar-entry';
      const swatch = document.createElement('span');
      swatch.className = 'categorical-color-bar-swatch';
      const color = state.colors[index]!;
      swatch.style.backgroundColor = `rgb(${color.map((channel) => Math.round(channel * 255)).join(' ')})`;
      const label = document.createElement('span');
      label.textContent = text;
      entry.append(swatch, label);
      return entry;
    }));
  }

  public setStatusBlock(state: HostStatusBlockMessage): void {
    this.#hostStatusBlock = { ...state, lines: [...state.lines] };
    this.#statusBlock.hidden = !state.visible;
    this.#statusBlock.classList.toggle('with-background', state.drawBackground);
    this.#statusBlock.classList.toggle('with-border', state.drawBorder);
    this.#statusBlock.replaceChildren(...state.lines.map((text) => {
      const line = document.createElement('div');
      line.textContent = text;
      return line;
    }));
  }

  public setResultWidgetLayout(state: HostResultWidgetLayoutMessage): void {
    this.#hostResultWidgetLayout = {
      ...state, scalarLegend: { ...state.scalarLegend }, colorBar: { ...state.colorBar },
      statusBlock: { ...state.statusBlock },
    };
    this.#scalarLegend.style.top = `${state.scalarLegend.top}px`;
    this.#scalarLegend.style.left = `${state.scalarLegend.left}px`;
    this.#colorBar.style.top = `${state.colorBar.top}px`;
    this.#colorBar.style.left = `${state.colorBar.left}px`;
    this.#statusBlock.style.top = `${state.statusBlock.top}px`;
    this.#statusBlock.style.right = `${state.statusBlock.right}px`;
  }

  public setGraphicsInteraction(state: HostGraphicsInteractionMessage): void {
    this.#graphicsInteraction = {
      ...state, primaryHighlightRgb: [...state.primaryHighlightRgb],
      secondaryHighlightRgb: [...state.secondaryHighlightRgb], mouseHighlightRgb: [...state.mouseHighlightRgb],
    };
    this.#rebuildSelectionOverlays();
    this.#rebuildHighlightOverlays();
    this.render();
  }

  public setViewportAppearance(appearance: HostViewportAppearanceMessage): void {
    this.#viewportAppearance = {
      ...appearance, topRgb: [...appearance.topRgb], bottomRgb: [...appearance.bottomRgb],
    };
    const renderer = this.#window.getRenderer();
    renderer.setGradientBackground(appearance.gradient);
    renderer.setBackground(...appearance.bottomRgb);
    renderer.setBackground2(...appearance.topRgb);
    for (const pipeline of this.#chunks.values()) this.#applyLighting(pipeline.actor);
    for (const pipeline of this.#transformedActorPipelines) this.#applyLighting(pipeline.actor);
    for (const pipeline of this.#sectionPipelines) this.#applyLighting(pipeline.actor);
    if (appearance.redraw) this.render();
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
    this.#applyLighting(actor);
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
    this.#addTransformedActors(pipeline);
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
      pipeline.mapper.setScalarRange(this.#displayedScalarRange());
    }
    pipeline.mapper.setLookupTable(this.#lookupTable);
    pipeline.mapper.setUseLookupTableScalarRange(true);
    pipeline.mapper.setScalarModeToUsePointData();
    pipeline.mapper.setScalarVisibility(this.#isContourVisible(pipeline));
    pipeline.polyData.modified();
    this.#rebuildSelectionOverlays();
    this.#rebuildHighlightOverlays();
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
      if (this.#field) mapper.setScalarRange(this.#displayedScalarRange());
      const actor = vtkActor.newInstance();
      actor.setMapper(mapper);
      actor.setPickable(false);
      actor.setPosition(...(this.#explodedOffsets.get(geometry.name) ?? [0, 0, 0]));
      actor.getProperty().setColor(geometry.rgba[0], geometry.rgba[1], geometry.rgba[2]);
      actor.getProperty().setOpacity(geometry.rgba[3]);
      actor.getProperty().setLineWidth(geometry.lineWidthPx);
      this.#applyLighting(actor);
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
    ids: Iterable<number>,
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
    for (const { element } of this.#annotationOverlays.values()) element.remove();
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

  public setSphereSymbols(symbols: readonly HostSphereSymbol[]): void {
    this.#clearSphereSymbols();
    const renderer = this.#window.getRenderer();
    for (const symbol of symbols) {
      const relativePoints = new Float32Array(symbol.points.length * 3);
      for (let index = 0; index < symbol.points.length; index += 1) {
        const point = symbol.points[index]!;
        relativePoints[index * 3] = point[0] - this.#worldOrigin[0];
        relativePoints[index * 3 + 1] = point[1] - this.#worldOrigin[1];
        relativePoints[index * 3 + 2] = point[2] - this.#worldOrigin[2];
      }
      const polyData = vtkPolyData.newInstance();
      polyData.getPoints().setData(relativePoints, 3);
      const scales = new Float32Array(symbol.points.length);
      const scaleArray = vtkDataArray.newInstance({ name: 'SphereSymbolScale', numberOfComponents: 1, values: scales });
      polyData.getPointData().addArray(scaleArray);
      const mapper = vtkGlyph3DMapper.newInstance({ scaling: true, scaleArray: 'SphereSymbolScale' });
      mapper.setInputData(polyData);
      mapper.setSourceConnection(this.#sphereSource.getOutputPort());
      mapper.setScaleModeToScaleByMagnitude();
      const actor = vtkActor.newInstance();
      actor.setMapper(mapper);
      actor.setPickable(false);
      actor.getProperty().setColor(symbol.rgba[0], symbol.rgba[1], symbol.rgba[2]);
      actor.getProperty().setOpacity(symbol.rgba[3]);
      renderer.addActor(actor);
      this.#sphereSymbolPipelines.push({ actor, mapper, polyData, scaleArray, relativePoints, scales,
        symbolSize: symbol.symbolSize });
    }
    this.#updateSphereSymbolScales();
    this.render();
  }

  public setTransientPrimitives(primitives: readonly HostTransientPrimitive[]): void {
    this.#clearTransientPrimitives();
    const renderer = this.#window.getRenderer();
    for (const primitive of primitives) {
      const positions = new Float32Array(primitive.points.length * 3);
      for (let index = 0; index < primitive.points.length; index += 1) {
        const point = primitive.points[index]!;
        positions.set([point[0] - this.#worldOrigin[0], point[1] - this.#worldOrigin[1],
          point[2] - this.#worldOrigin[2]], index * 3);
      }
      const polyData = vtkPolyData.newInstance();
      polyData.getPoints().setData(positions, 3);
      if (primitive.drawPoints) {
        const vertices = new Uint32Array(primitive.points.length * 2);
        for (let index = 0; index < primitive.points.length; index += 1)
          vertices.set([1, index], index * 2);
        polyData.getVerts().setData(vertices);
      }
      if (primitive.lines.length > 0) {
        const connectivity = new Uint32Array(primitive.lines.length * 2);
        for (let index = 0; index < primitive.lines.length; index += 1)
          connectivity.set(primitive.lines[index]!, index * 2);
        polyData.getLines().setData(toVtkLineCells(connectivity));
      }
      const mapper = vtkMapper.newInstance({ scalarVisibility: false });
      mapper.setInputData(polyData);
      if (this.#sectionPlane) mapper.addClippingPlane(this.#sectionPlane);
      const actor = vtkActor.newInstance();
      actor.setMapper(mapper);
      actor.setPickable(false);
      actor.setVisibility(primitive.visible !== false);
      actor.getProperty().setColor(primitive.rgba[0], primitive.rgba[1], primitive.rgba[2]);
      actor.getProperty().setOpacity(primitive.rgba[3]);
      actor.getProperty().setPointSize(primitive.pointSize);
      actor.getProperty().setLineWidth(primitive.lineWidth);
      actor.getProperty().setLighting(false);
      renderer.addActor(actor);
      this.#transientPrimitivePipelines.push({ actorName: primitive.name, actor, mapper, polyData,
        pointCount: primitive.points.length, lineCount: primitive.lines.length });
    }
    this.render();
  }

  public setCoordinateOverlays(axes: readonly HostCoordinateAxis[], captions: readonly HostCaption[]): void {
    this.#clearCoordinateOverlays();
    const renderer = this.#window.getRenderer();
    for (const axis of axes) {
      const relativePoints = new Float32Array([axis.point[0] - this.#worldOrigin[0],
        axis.point[1] - this.#worldOrigin[1], axis.point[2] - this.#worldOrigin[2]]);
      const polyData = vtkPolyData.newInstance();
      polyData.getPoints().setData(relativePoints, 3);
      const scales = new Float32Array(1);
      const scaleArray = vtkDataArray.newInstance({ name: 'CoordinateAxisScale', numberOfComponents: 1, values: scales });
      const directionArray = vtkDataArray.newInstance({ name: 'CoordinateAxisDirection', numberOfComponents: 3,
        values: new Float32Array(axis.direction) });
      polyData.getPointData().addArray(scaleArray);
      polyData.getPointData().addArray(directionArray);
      const mapper = vtkGlyph3DMapper.newInstance({ scaling: true, scaleArray: 'CoordinateAxisScale',
        orientationArray: 'CoordinateAxisDirection' });
      mapper.setInputData(polyData);
      mapper.setInputData(this.#coordinateAxisSource, 1);
      mapper.setScaleModeToScaleByMagnitude();
      mapper.setOrientationModeToDirection();
      const actor = vtkActor.newInstance();
      actor.setMapper(mapper);
      actor.setPickable(false);
      actor.getProperty().setColor(axis.rgba[0], axis.rgba[1], axis.rgba[2]);
      actor.getProperty().setOpacity(axis.rgba[3]);
      actor.getProperty().setLineWidth(2);
      renderer.addActor(actor);
      this.#coordinateAxisPipelines.push({ actor, mapper, polyData, scaleArray, directionArray, relativePoints, scales,
        symbolSize: axis.symbolSize });
    }
    for (const state of captions) {
      const element = document.createElement('div');
      element.className = 'coordinate-caption';
      element.textContent = state.text;
      element.title = state.name;
      element.style.color = `rgba(${Math.round(state.rgba[0] * 255)}, ${Math.round(state.rgba[1] * 255)}, ${Math.round(state.rgba[2] * 255)}, ${state.rgba[3]})`;
      element.style.fontSize = `${Math.max(1, Math.trunc(16 * state.fontScaleFactor))}px`;
      this.#annotationLayer.appendChild(element);
      this.#captionOverlays.set(state.name, { state, element });
    }
    this.#updateCoordinateOverlayPositions();
    this.render();
  }

  public setDirectionalGlyphs(glyphs: readonly HostDirectionalGlyph[]): void {
    this.#clearDirectionalGlyphs();
    const renderer = this.#window.getRenderer();
    for (const glyph of glyphs) {
      const relativePoints = new Float32Array(glyph.points.length * 3);
      const directions = new Float32Array(glyph.directions.length * 3);
      for (let index = 0; index < glyph.points.length; index += 1) {
        const point = glyph.points[index]!;
        relativePoints.set([point[0] - this.#worldOrigin[0], point[1] - this.#worldOrigin[1],
          point[2] - this.#worldOrigin[2]], index * 3);
        directions.set(glyph.directions[index]!, index * 3);
      }
      const polyData = vtkPolyData.newInstance();
      polyData.getPoints().setData(relativePoints, 3);
      const scales = new Float32Array(glyph.points.length);
      const scaleArray = vtkDataArray.newInstance({ name: 'DirectionalGlyphScale', numberOfComponents: 1,
        values: scales });
      const directionArray = vtkDataArray.newInstance({ name: 'DirectionalGlyphDirection', numberOfComponents: 3,
        values: directions });
      polyData.getPointData().addArray(scaleArray);
      polyData.getPointData().addArray(directionArray);
      const mapper = vtkGlyph3DMapper.newInstance({ scaling: true, scaleArray: 'DirectionalGlyphScale',
        orientationArray: 'DirectionalGlyphDirection' });
      mapper.setInputData(polyData);
      const ownedSources: { delete(): void }[] = [];
      let sourceData: VtkPolyData | null = null;
      if (glyph.kind === 'oriented-arrow') {
        const relativeSize = glyph.relativeSize!;
        const arrow = vtkArrowSource.newInstance({ tipResolution: 21, tipLength: 0.3 / relativeSize,
          tipRadius: 0.1 / relativeSize, shaftResolution: 15, shaftRadius: 0.03 / relativeSize });
        arrow.update();
        const sourcePoints = arrow.getOutputData().getPoints();
        const values = sourcePoints.getData();
        // Current vtk.js recenters vtkArrowSource by -0.5 + tipLength/2; ActiViz 5.8 leaves it at X=0..1.
        const translation = 0.5 - 0.15 / relativeSize + (glyph.invert ? -1.05 : 0.05);
        for (let index = 0; index < values.length; index += 3) values[index] = values[index]! + translation;
        sourcePoints.modified();
        const triangles = vtkTriangleFilter.newInstance();
        triangles.setInputData(arrow.getOutputData());
        triangles.update();
        sourceData = triangles.getOutputData();
        mapper.setInputConnection(triangles.getOutputPort(), 1);
        ownedSources.push(triangles, arrow);
      } else if (glyph.kind === 'double-arrow') {
        const arrow = vtkArrowSource.newInstance({ tipResolution: 21, tipLength: 0.3, tipRadius: 0.1,
          shaftResolution: 15, shaftRadius: 0.03 });
        arrow.update();
        const arrowPoints = arrow.getOutputData().getPoints();
        const arrowValues = arrowPoints.getData();
        for (let index = 0; index < arrowValues.length; index += 3)
          arrowValues[index] = arrowValues[index]! + 0.35;
        arrowPoints.modified();
        const cone = vtkConeSource.newInstance({ resolution: 21, height: 0.3, radius: 0.1,
          center: [0.65, 0, 0] });
        cone.update();
        const append = vtkAppendPolyData.newInstance();
        append.setInputData(arrow.getOutputData());
        append.addInputData(cone.getOutputData());
        append.update();
        const appendedPoints = append.getOutputData().getPoints();
        const appendedValues = appendedPoints.getData();
        for (let index = 0; index < appendedValues.length; index += 3)
          appendedValues[index] = appendedValues[index]! + 0.05;
        appendedPoints.modified();
        const triangles = vtkTriangleFilter.newInstance();
        triangles.setInputData(append.getOutputData());
        triangles.update();
        sourceData = triangles.getOutputData();
        mapper.setInputConnection(triangles.getOutputPort(), 1);
        ownedSources.push(triangles, append, cone, arrow);
      } else if (glyph.kind === 'oriented-thermal') {
        const source = createThermalSource(glyph.invert!);
        sourceData = source.data;
        mapper.setInputData(sourceData, 1);
        ownedSources.push(...source.owners);
      } else if (glyph.kind === 'oriented-flux') {
        const source = createFluxSource(glyph.center!, glyph.invert!);
        sourceData = source.data;
        mapper.setInputData(sourceData, 1);
        ownedSources.push(...source.owners);
      } else {
        mapper.setInputData(glyph.kind === 'displacement-cone' ? this.#displacementConeSource
          : glyph.kind === 'general-cone' ? this.#generalConeSource
          : glyph.kind === 'rotational-constraint' ? this.#rotationalConstraintSource
            : this.#springSource.data, 1);
      }
      mapper.setScaleModeToScaleByMagnitude();
      mapper.setOrientationModeToDirection();
      const actor = vtkActor.newInstance();
      actor.setMapper(mapper);
      actor.setPickable(false);
      actor.getProperty().setColor(glyph.rgba[0], glyph.rgba[1], glyph.rgba[2]);
      actor.getProperty().setOpacity(glyph.rgba[3]);
      actor.getProperty().setAmbient(0.6);
      actor.getProperty().setDiffuse(0.6);
      actor.getProperty().setBackfaceCulling(true);
      if (glyph.kind === 'rotational-constraint') actor.getProperty().setInterpolationToFlat();
      renderer.addActor(actor);
      this.#directionalGlyphPipelines.push({ kind: glyph.kind, invert: glyph.invert, center: glyph.center,
        actor, mapper, polyData, scaleArray, directionArray,
        ownedSources, sourceData,
        relativePoints, scales, symbolSize: glyph.symbolSize * (glyph.kind === 'oriented-arrow' ? glyph.relativeSize!
          : glyph.kind === 'displacement-cone' || glyph.kind === 'general-cone'
            || glyph.kind === 'rotational-constraint' ? 0.3
            : glyph.kind === 'oriented-thermal' || glyph.kind === 'oriented-flux' ? 0.5 : 1) });
    }
    this.#updateDirectionalGlyphScales();
    this.render();
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
    for (const transformed of this.#transformedActorPipelines) {
      if (transformed.source.actorId === actorId)
        transformed.actor.setVisibility(visible && this.#transformedActorsVisible);
    }
    for (const overlay of this.#selectionOverlays) {
      if (overlay.actorId === actorId) overlay.actor.setVisibility(visible);
    }
    for (const overlay of this.#highlightOverlays) {
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

  public setActorPresentation(actorId: string, actorName: string,
                              rgba: readonly [number, number, number, number]): void {
    let previousName: string | null = null;
    for (const pipeline of this.#chunks.values()) {
      if (pipeline.actorId === actorId) {
        previousName ??= pipeline.actorName;
        pipeline.actorName = actorName;
        pipeline.actor.getProperty().setColor(rgba[0], rgba[1], rgba[2]);
        pipeline.actor.getProperty().setOpacity(rgba[3]);
        pipeline.mapper.setScalarVisibility(this.#isContourVisible(pipeline));
      }
    }
    for (const transformed of this.#transformedActorPipelines) {
      if (transformed.source.actorId !== actorId) continue;
      transformed.actor.getProperty().setColor(rgba[0], rgba[1], rgba[2]);
      transformed.actor.getProperty().setOpacity(rgba[3]);
    }
    if (previousName && previousName !== actorName && this.#selectedActorNames.delete(previousName)) {
      this.#selectedActorNames.add(actorName);
      this.#rebuildSelectionOverlays();
    }
    if (previousName && previousName !== actorName && this.#highlightedActorNames.delete(previousName)) {
      this.#highlightedActorNames.add(actorName);
      this.#rebuildHighlightOverlays();
    }
    this.render();
  }

  public setActorContours(actors: readonly { name: string; colorContours: boolean }[]): void {
    this.#actorColorContours.clear();
    for (const actor of actors) this.#actorColorContours.set(actor.name, actor.colorContours);
    for (const pipeline of this.#chunks.values()) {
      pipeline.mapper.setScalarVisibility(this.#isContourVisible(pipeline));
    }
    this.render();
  }

  public setActorHighlights(actorNames: readonly string[]): void {
    this.#highlightedActorNames = new Set(actorNames);
    this.#rebuildHighlightOverlays();
    this.render();
  }

  public removeActors(actorIds: readonly string[]): void {
    const removedIds = new Set(actorIds);
    const removedNames = new Set<string>();
    const renderer = this.#window.getRenderer();
    this.#clearTransformedActors();
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
    for (const name of removedNames) this.#highlightedActorNames.delete(name);
    for (const pipeline of this.#chunks.values()) this.#addTransformedActors(pipeline);
    this.#rebuildSelectionOverlays();
    this.#rebuildHighlightOverlays();
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

  public setTransformations(matrices: readonly (readonly number[])[], visible: boolean): void {
    this.#clearTransformedActors();
    this.#transformationMatrices = matrices.map((matrix) => [...matrix]);
    this.#transformedActorsVisible = visible;
    for (const pipeline of this.#chunks.values()) this.#addTransformedActors(pipeline);
    this.render();
  }

  public getTransformationDiagnostics(): { matrices: number; actors: number; visibleActors: number; pickableActors: number } {
    return {
      matrices: this.#transformationMatrices.length,
      actors: this.#transformedActorPipelines.length,
      visibleActors: this.#transformedActorPipelines.filter((pipeline) => pipeline.actor.getVisibility()).length,
      pickableActors: this.#transformedActorPipelines.filter((pipeline) => pipeline.actor.getPickable()).length,
    };
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
      for (const overlay of this.#highlightOverlays) overlay.mapper.addClippingPlane(this.#sectionPlane);
      for (const pipeline of this.#transientPrimitivePipelines) pipeline.mapper.addClippingPlane(this.#sectionPlane);
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
    for (const overlay of this.#highlightOverlays) overlay.mapper.removeClippingPlane(this.#sectionPlane);
    for (const pipeline of this.#transientPrimitivePipelines) pipeline.mapper.removeClippingPlane(this.#sectionPlane);
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
    for (const overlay of this.#highlightOverlays) {
      if (actorIds.has(overlay.actorId)) overlay.actor.setPosition(...offset);
    }
    for (const pipeline of this.#sectionPipelines) {
      if (pipeline.actorName === actorName) pipeline.actor.setPosition(...offset);
    }
    for (const transformed of this.#transformedActorPipelines) {
      if (transformed.source.actorName === actorName) transformed.actor.setPosition(...offset);
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
    const radius = Math.max(1e-12, 0.525 * Math.hypot(bounds[1]! - bounds[0]!, bounds[3]! - bounds[2]!,
      bounds[5]! - bounds[4]!));
    const distanceToCenter = direction[0] * (center[0] - position[0])
      + direction[1] * (center[1] - position[1]) + direction[2] * (center[2] - position[2]);
    // In parallel projection distance does not change the zoom. Perspective
    // views keep their distance unless the updated bounds require backing out.
    const distance = camera.getParallelProjection() ? radius : Math.max(distanceToCenter, radius);
    const motion = distanceToCenter - distance;
    const nextPosition = [position[0] + motion * direction[0], position[1] + motion * direction[1],
      position[2] + motion * direction[2]] as const;
    camera.setPosition(...nextPosition);
    // Trackball rotation pivots at the focal point. A fixed one-unit distance
    // leaves that pivot beside the camera on large models and rotates the mesh
    // behind the near plane. Keep it on the model's center-depth plane, while
    // preserving the current view direction and pan.
    camera.setFocalPoint(position[0] + direction[0] * distanceToCenter,
      position[1] + direction[1] * distanceToCenter, position[2] + direction[2] * distanceToCenter);
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
    this.#contoursVisible = visible;
    for (const pipeline of this.#chunks.values()) {
      pipeline.mapper.setScalarVisibility(this.#isContourVisible(pipeline));
    }
    this.render();
  }

  #isContourVisible(pipeline: ChunkPipeline): boolean {
    return this.#contoursVisible && (this.#actorColorContours.get(pipeline.actorName) ?? true)
      && pipeline.hasScalars;
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
      if (!pipeline || primitiveIndex === undefined
        || !pipeline.sourcePointIds || !pipeline.sourceElementIds) {
        return null;
      }
      const explodedOffset = this.#explodedOffsets.get(pipeline.actorName) ?? [0, 0, 0];
      const relativeHit = intersectPickedTriangle(pipeline.connectivity, pipeline.positions, primitiveIndex,
        subtract(near, explodedOffset), subtract(far, explodedOffset));
      if (!relativeHit) return null;
      const visualHit = add(relativeHit, explodedOffset);
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
      const renderView = this.#window.getApiSpecificRenderWindow() as unknown as DisplayCoordinateRenderView;
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
      this.#updateSphereSymbolScales();
      this.#updateCoordinateOverlayPositions();
      this.#updateDirectionalGlyphScales();
      this.#updateScaleWidget();
      this.#window.getRenderWindow().render();
      this.#updateAnnotationPositions();
    }
  }

  #applyLighting(actor: VtkActor): void {
    actor.getProperty().setAmbient(this.#viewportAppearance.ambient);
    actor.getProperty().setDiffuse(this.#viewportAppearance.diffuse);
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
      webglLimits: gl ? {
        maxTextureSize: Number(gl.getParameter(gl.MAX_TEXTURE_SIZE)),
        maxRenderbufferSize: Number(gl.getParameter(gl.MAX_RENDERBUFFER_SIZE)),
        maxVertexAttribs: Number(gl.getParameter(gl.MAX_VERTEX_ATTRIBS)),
        maxCombinedTextureImageUnits: Number(gl.getParameter(gl.MAX_COMBINED_TEXTURE_IMAGE_UNITS)),
      } : null,
      rendererActors: this.#window.getRenderer().getActors().length,
      pipelineChunks: pipelines.length,
      pipelinePoints: pipelines.reduce((sum, pipeline) => sum + pipeline.polyData.getNumberOfPoints(), 0),
      pipelinePolys: pipelines.reduce((sum, pipeline) => sum + pipeline.polyData.getNumberOfPolys(), 0),
      visiblePipelineChunks: pipelines.filter((pipeline) => pipeline.actor.getVisibility()).length,
      pickablePipelineChunks: pipelines.filter((pipeline) => pipeline.actor.getPickable()).length,
      transformations: this.getTransformationDiagnostics(),
      viewportAppearance: {
        gradient: this.#viewportAppearance.gradient,
        topRgb: [...this.#viewportAppearance.topRgb],
        bottomRgb: [...this.#viewportAppearance.bottomRgb],
        ambient: this.#viewportAppearance.ambient,
        diffuse: this.#viewportAppearance.diffuse,
        actorLighting: pipelines.map((pipeline) => ({
          ambient: pipeline.actor.getProperty().getAmbient(),
          diffuse: pipeline.actor.getProperty().getDiffuse(),
        })),
      },
      orientationMarker: {
        visible: this.#orientationMarker.getEnabled(),
        rendererActors: this.#orientationMarker.getRenderer().getActors().length,
        viewport: [...this.#orientationMarker.computeViewport()],
        maxPixelSize: this.#orientationMarker.getMaxPixelSize(),
        cameraAligned: vectorsNear(
          this.#window.getRenderer().getActiveCamera().getDirectionOfProjection(),
          this.#orientationMarker.getRenderer().getActiveCamera().getDirectionOfProjection(), 1e-9),
        xColor: [180 / 255, 4 / 255, 38 / 255],
        yColor: [33 / 255, 206 / 255, 38 / 255],
        zColor: [59 / 255, 76 / 255, 192 / 255],
      },
      scaleWidget: {
        visible: this.#scaleWidgetVisible && !this.#scaleWidget.hidden,
        unit: this.#scaleWidgetUnit,
        worldLength: this.#scaleWidgetWorldLength,
        pixelWidth: Number.parseFloat(this.#scaleWidgetBar.style.width) || 0,
        labels: this.#scaleWidgetLabels.map((label) => label.textContent ?? ''),
      },
      scalarLegend: {
        visible: !this.#scalarLegend.hidden,
        fieldName: this.#hostScalarLegend?.fieldName ?? '',
        componentName: this.#hostScalarLegend?.componentName ?? '',
        unit: this.#hostScalarLegend?.unit ?? '',
        minMaxType: this.#hostScalarLegend?.minMaxType ?? '',
        numberFormat: this.#hostScalarLegend?.numberFormat ?? '',
        minimum: this.#hostScalarLegend?.minimum ?? 0,
        maximum: this.#hostScalarLegend?.maximum ?? 1,
        colors: this.#scalarLegendColors.childElementCount,
        labels: [...this.#scalarLegendLabels.children].map((label) => label.textContent ?? ''),
        text: this.#scalarLegendText.textContent ?? '',
        drawBackground: this.#scalarLegend.classList.contains('with-background'),
        drawBorder: this.#scalarLegend.classList.contains('with-border'),
      },
      colorBar: {
        visible: !this.#colorBar.hidden,
        entries: this.#colorBarEntries.childElementCount,
        labels: this.#hostColorBar?.labels ?? [],
        colors: this.#hostColorBar?.colors ?? [],
        drawBackground: this.#colorBar.classList.contains('with-background'),
        drawBorder: this.#colorBar.classList.contains('with-border'),
      },
      statusBlock: {
        visible: !this.#statusBlock.hidden,
        lines: this.#hostStatusBlock?.lines ?? [],
        drawBackground: this.#statusBlock.classList.contains('with-background'),
        drawBorder: this.#statusBlock.classList.contains('with-border'),
      },
      resultWidgetLayout: this.#hostResultWidgetLayout ?? {
        scalarLegend: { top: 20, left: 20 }, colorBar: { top: 20, left: 20 },
        statusBlock: { top: 20, right: 20 },
      },
      cameraDirection: [...camera.getDirectionOfProjection()],
      cameraViewUp: [...camera.getViewUp()],
      cameraPosition: [...camera.getPosition()],
      cameraFocalPoint: [...camera.getFocalPoint()],
      cameraParallelScale: camera.getParallelScale(),
      cameraClippingRange: [...camera.getClippingRange()],
      actorColors: pipelines.map((pipeline) => ({
        actorId: pipeline.actorId,
        actorName: pipeline.actorName,
        color: [...pipeline.actor.getProperty().getColor()],
        opacity: pipeline.actor.getProperty().getOpacity(),
        visible: pipeline.actor.getVisibility(),
        scalarVisibility: pipeline.mapper.getScalarVisibility(),
        pickable: pipeline.actor.getPickable(),
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
        overlayPointTuples: this.#selectionOverlays.reduce((sum, overlay) =>
          sum + (overlay.polyData?.getNumberOfPoints() ?? 0), 0),
        overlayPositions: this.#selectionOverlays.map((overlay) => [...overlay.actor.getPosition()]),
        overflow: this.#selectionOverlayOverflow,
      },
      actorHighlights: {
        actorNames: [...this.#highlightedActorNames],
        overlayActors: this.#highlightOverlays.length,
        overlayPositions: this.#highlightOverlays.map((overlay) => [...overlay.actor.getPosition()]),
        colors: this.#highlightOverlays.map((overlay) => [...overlay.actor.getProperty().getColor()]),
      },
      graphicsInteraction: {
        pointSmoothing: this.#graphicsInteraction.pointSmoothing,
        lineSmoothing: this.#graphicsInteraction.lineSmoothing,
        primaryHighlightRgb: [...this.#graphicsInteraction.primaryHighlightRgb],
        secondaryHighlightRgb: [...this.#graphicsInteraction.secondaryHighlightRgb],
        mouseHighlightRgb: [...this.#graphicsInteraction.mouseHighlightRgb],
        rasterization: 'browser-webgl-antialias',
        selectionColors: this.#selectionOverlays.map((overlay) => [...overlay.actor.getProperty().getColor()]),
      },
      annotations: {
        count: this.#annotationOverlays.size,
        visible: [...this.#annotationOverlays.values()].filter((annotation) => !annotation.element.hidden).length,
      },
      sphereSymbols: {
        actors: this.#sphereSymbolPipelines.length,
        points: this.#sphereSymbolPipelines.reduce((sum, pipeline) => sum + pipeline.scales.length, 0),
        scales: this.#sphereSymbolPipelines.flatMap((pipeline) => [...pipeline.scales]),
      },
      transientPrimitives: {
        actors: this.#transientPrimitivePipelines.length,
        actorNames: this.#transientPrimitivePipelines.map((pipeline) => pipeline.actorName),
        visibleActors: this.#transientPrimitivePipelines.filter((pipeline) => pipeline.actor.getVisibility()).length,
        points: this.#transientPrimitivePipelines.reduce((sum, pipeline) => sum + pipeline.pointCount, 0),
        lines: this.#transientPrimitivePipelines.reduce((sum, pipeline) => sum + pipeline.lineCount, 0),
        colors: this.#transientPrimitivePipelines.map((pipeline) => [...pipeline.actor.getProperty().getColor()]),
      },
      coordinateAxes: {
        actors: this.#coordinateAxisPipelines.length,
        scales: this.#coordinateAxisPipelines.flatMap((pipeline) => [...pipeline.scales]),
      },
      captions: {
        count: this.#captionOverlays.size,
        visible: [...this.#captionOverlays.values()].filter((caption) => !caption.element.hidden).length,
        texts: [...this.#captionOverlays.values()].map((caption) => caption.element.textContent),
      },
      directionalGlyphs: {
        actors: this.#directionalGlyphPipelines.length,
        points: this.#directionalGlyphPipelines.reduce((sum, pipeline) => sum + pipeline.scales.length, 0),
        scales: this.#directionalGlyphPipelines.flatMap((pipeline) => [...pipeline.scales]),
        sourcePoints: this.#displacementConeSource.getNumberOfPoints(),
        sourcePolys: this.#displacementConeSource.getNumberOfPolys(),
        generalConeSourcePoints: this.#generalConeSource.getNumberOfPoints(),
        generalConeSourcePolys: this.#generalConeSource.getNumberOfPolys(),
        generalConeSourceBounds: [...this.#generalConeSource.getBounds()],
        rotationalSourcePoints: this.#rotationalConstraintSource.getNumberOfPoints(),
        rotationalSourcePolys: this.#rotationalConstraintSource.getNumberOfPolys(),
        rotationalSourceLines: this.#rotationalConstraintSource.getNumberOfLines(),
        springSourcePoints: this.#springSource.data.getNumberOfPoints(),
        springSourcePolys: this.#springSource.data.getNumberOfPolys(),
        springSourceBounds: [...this.#springSource.data.getBounds()],
        arrowSources: this.#directionalGlyphPipelines.filter((pipeline) => pipeline.sourceData !== null
          && (pipeline.kind === 'oriented-arrow' || pipeline.kind === 'double-arrow'))
          .map((pipeline) => {
            const source = pipeline.sourceData!;
            return { kind: pipeline.kind, points: source.getNumberOfPoints(), polys: source.getNumberOfPolys(),
              bounds: [...source.getBounds()] };
          }),
        thermalSources: this.#directionalGlyphPipelines.filter((pipeline) => pipeline.sourceData !== null
          && (pipeline.kind === 'oriented-thermal' || pipeline.kind === 'oriented-flux'))
          .map((pipeline) => {
            const source = pipeline.sourceData!;
            return { kind: pipeline.kind, invert: pipeline.invert, center: pipeline.center,
              points: source.getNumberOfPoints(), polys: source.getNumberOfPolys(), bounds: [...source.getBounds()] };
          }),
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
          overlay.mapper.getNumberOfClippingPlanes() > 0).length + this.#highlightOverlays.filter((overlay) =>
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
    this.#clearHighlightOverlays();
    this.#clearSectionGeometry();
    this.#clearSphereSymbols();
    this.#clearTransientPrimitives();
    this.#clearCoordinateOverlays();
    this.#clearDirectionalGlyphs();
    this.#clearTransformedActors();
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

  #addTransformedActors(source: ChunkPipeline): void {
    const renderer = this.#window.getRenderer();
    for (const matrix of this.#transformationMatrices) {
      const actor = vtkActor.newInstance();
      actor.setMapper(source.mapper);
      actor.setPickable(false);
      actor.setVisibility(this.#transformedActorsVisible && source.actor.getVisibility());
      const sourcePosition = source.actor.getPosition();
      actor.setPosition(sourcePosition[0]!, sourcePosition[1]!, sourcePosition[2]!);
      const sourceProperty = source.actor.getProperty();
      actor.getProperty().setColor(...sourceProperty.getColor());
      actor.getProperty().setOpacity(sourceProperty.getOpacity());
      this.#applyLighting(actor);
      actor.setUserMatrix(toRelativeTransformationMatrix(matrix, this.#worldOrigin));
      renderer.addActor(actor);
      this.#transformedActorPipelines.push({ actor, source });
    }
  }

  #clearTransformedActors(): void {
    const renderer = this.#window.getRenderer();
    for (const pipeline of this.#transformedActorPipelines) {
      renderer.removeActor(pipeline.actor);
      pipeline.actor.delete();
    }
    this.#transformedActorPipelines.length = 0;
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

  #clearSphereSymbols(): void {
    const renderer = this.#window.getRenderer();
    for (const pipeline of this.#sphereSymbolPipelines) {
      renderer.removeActor(pipeline.actor);
      pipeline.actor.delete();
      pipeline.mapper.delete();
      pipeline.polyData.delete();
      pipeline.scaleArray.delete();
    }
    this.#sphereSymbolPipelines.length = 0;
  }

  #clearTransientPrimitives(): void {
    const renderer = this.#window.getRenderer();
    for (const pipeline of this.#transientPrimitivePipelines) {
      renderer.removeActor(pipeline.actor);
      pipeline.actor.delete();
      pipeline.mapper.delete();
      pipeline.polyData.delete();
    }
    this.#transientPrimitivePipelines.length = 0;
  }

  #updateSphereSymbolScales(): void {
    if (this.#sphereSymbolPipelines.length === 0) return;
    const canvas = this.#window.getContainer().querySelector('canvas');
    if (!canvas || canvas.clientHeight <= 0) return;
    const camera = this.#window.getRenderer().getActiveCamera();
    const parallel = camera.getParallelProjection();
    const commonWorldPerPixel = parallel ? 2 * camera.getParallelScale() / canvas.clientHeight : 0;
    const cameraPosition = camera.getPosition();
    const direction = camera.getDirectionOfProjection();
    const perspectiveFactor = parallel ? 0
      : 2 * Math.tan(camera.getViewAngle() * Math.PI / 360) / canvas.clientHeight;
    for (const pipeline of this.#sphereSymbolPipelines) {
      for (let index = 0; index < pipeline.scales.length; index += 1) {
        const offset = index * 3;
        const worldPerPixel = parallel ? commonWorldPerPixel : perspectiveFactor * Math.max(1e-9,
          (pipeline.relativePoints[offset]! - cameraPosition[0]!) * direction[0]!
          + (pipeline.relativePoints[offset + 1]! - cameraPosition[1]!) * direction[1]!
          + (pipeline.relativePoints[offset + 2]! - cameraPosition[2]!) * direction[2]!);
        pipeline.scales[index] = worldPerPixel * pipeline.symbolSize;
      }
      pipeline.scaleArray.modified();
    }
  }

  #clearCoordinateOverlays(): void {
    const renderer = this.#window.getRenderer();
    for (const pipeline of this.#coordinateAxisPipelines) {
      renderer.removeActor(pipeline.actor);
      pipeline.actor.delete();
      pipeline.mapper.delete();
      pipeline.polyData.delete();
      pipeline.scaleArray.delete();
      pipeline.directionArray.delete();
    }
    this.#coordinateAxisPipelines.length = 0;
    for (const { element } of this.#captionOverlays.values()) element.remove();
    this.#captionOverlays.clear();
  }

  #updateCoordinateOverlayPositions(): void {
    updateScreenScaledPipelines(this.#window, this.#coordinateAxisPipelines);
    if (this.#captionOverlays.size === 0) return;
    const canvas = this.#container.querySelector('canvas');
    if (!canvas || canvas.clientWidth <= 0 || canvas.clientHeight <= 0) return;
    const renderer = this.#window.getRenderer();
    const renderView = this.#window.getApiSpecificRenderWindow() as unknown as DisplayCoordinateRenderView;
    const camera = renderer.getActiveCamera();
    const scaleX = canvas.width / canvas.clientWidth;
    const scaleY = canvas.height / canvas.clientHeight;
    for (const { state, element } of this.#captionOverlays.values()) {
      let point: readonly [number, number, number] = state.position;
      if (state.offsetVector) {
        const relative = subtract(state.position, this.#worldOrigin);
        const worldPerPixel = worldPerPixelAt(camera, canvas.clientHeight, relative);
        point = add(state.position, scale(state.offsetVector, worldPerPixel));
      }
      const display = renderView.worldToDisplay(point[0] - this.#worldOrigin[0],
        point[1] - this.#worldOrigin[1], point[2] - this.#worldOrigin[2], renderer);
      element.hidden = !display.every(Number.isFinite) || display[2] < 0 || display[2] > 1;
      if (!element.hidden) {
        element.style.left = `${display[0] / scaleX}px`;
        element.style.top = `${canvas.clientHeight - display[1] / scaleY}px`;
      }
    }
  }

  #clearDirectionalGlyphs(): void {
    const renderer = this.#window.getRenderer();
    for (const pipeline of this.#directionalGlyphPipelines) {
      renderer.removeActor(pipeline.actor);
      pipeline.actor.delete();
      pipeline.mapper.delete();
      pipeline.polyData.delete();
      pipeline.scaleArray.delete();
      pipeline.directionArray.delete();
      for (const source of pipeline.ownedSources) source.delete();
    }
    this.#directionalGlyphPipelines.length = 0;
  }

  #updateDirectionalGlyphScales(): void {
    updateScreenScaledPipelines(this.#window, this.#directionalGlyphPipelines);
  }

  public delete(): void {
    this.#closeAnnotationContextMenu();
    this.#resizeObserver.disconnect();
    for (const subscription of this.#interactionSubscriptions) subscription.unsubscribe();
    this.#annotationOverlays.clear();
    this.#annotationLayer.remove();
    this.#scaleWidget.remove();
    this.#scalarLegend.remove();
    this.#colorBar.remove();
    this.#statusBlock.remove();
    this.clear();
    this.#sectionPlane?.delete();
    this.#sectionPlane = null;
    this.#sphereSource.delete();
    this.#coordinateAxisSource.delete();
    this.#displacementConeSource.delete();
    this.#generalConeSource.delete();
    this.#rotationalConstraintSource.delete();
    for (const source of this.#springSource.owners) source.delete();
    this.#lookupTable.delete();
    this.#orientationMarker.setEnabled(false);
    this.#orientationMarker.delete();
    this.#orientationAxes.delete();
    this.#window.delete();
  }

  #updateAnnotationPositions(): void {
    if (this.#annotationOverlays.size === 0) return;
    for (const { state, element } of this.#annotationOverlays.values())
      this.#positionAnnotation(state, element, state.offset);
  }

  #updateScaleWidget(): void {
    if (!this.#scaleWidgetVisible) return;
    const canvas = this.#container.querySelector('canvas');
    if (!canvas || canvas.clientHeight <= 0) return;
    const camera = this.#window.getRenderer().getActiveCamera();
    const targetWidth = 400;
    const rawLength = worldPerPixelAt(camera, canvas.clientHeight, camera.getFocalPoint()) * targetWidth;
    if (!Number.isFinite(rawLength) || rawLength <= 0) return;
    const exponent = Math.floor(Math.log10(rawLength));
    const power = 10 ** exponent;
    const roundedLength = Math.round(5 * rawLength / power) / 5 * power;
    const pixelWidth = targetWidth * roundedLength / rawLength;
    this.#scaleWidgetWorldLength = roundedLength;
    this.#scaleWidgetBar.style.width = `${pixelWidth}px`;
    for (let index = 0; index < this.#scaleWidgetLabels.length; index += 1) {
      const value = roundedLength * index / 5;
      this.#scaleWidgetLabels[index]!.textContent = formatScaleValue(value)
        + (index === 5 && this.#scaleWidgetUnit.length > 0 ? ` ${this.#scaleWidgetUnit}` : '');
      this.#scaleWidgetLabels[index]!.style.left = `${index * 20}%`;
    }
    this.#scaleWidget.querySelector<HTMLDivElement>('.scale-widget-labels')!.style.width = `${pixelWidth}px`;
  }

  #positionAnnotation(state: HostAnnotation, element: HTMLDivElement,
                      offset: readonly [number, number]): void {
    const canvas = this.#container.querySelector('canvas');
    if (!canvas || canvas.clientWidth <= 0 || canvas.clientHeight <= 0) return;
    const renderer = this.#window.getRenderer();
    const renderView = this.#window.getApiSpecificRenderWindow() as unknown as DisplayCoordinateRenderView;
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
    const [minimum, maximum] = this.#hostScalarLegend
      ? [this.#hostScalarLegend.minimum, this.#hostScalarLegend.maximum]
      : this.#field?.displayedRange ?? [0, 1];
    const span = maximum - minimum;
    this.#lookupTable.removeAllPoints();
    if (this.#hostScalarLegend) {
      const colors = this.#hostScalarLegend.colors;
      for (let index = 0; index < colors.length; index += 1) {
        const color = colors[index]!;
        const value = colors.length === 1 ? minimum : minimum + span * index / (colors.length - 1);
        this.#lookupTable.addRGBPoint(value, color[0], color[1], color[2]);
      }
    } else if (this.#colorMapName === 'Blue to Red Rainbow') {
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

  #displayedScalarRange(): [number, number] {
    return this.#hostScalarLegend
      ? [this.#hostScalarLegend.minimum, this.#hostScalarLegend.maximum]
      : [...(this.#field?.displayedRange ?? [0, 1])];
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

  #rebuildHighlightOverlays(): void {
    this.#clearHighlightOverlays();
    for (const pipeline of this.#chunks.values()) {
      if (!this.#highlightedActorNames.has(pipeline.actorName)) continue;
      const mapper = vtkMapper.newInstance({ scalarVisibility: false });
      mapper.setInputData(pipeline.polyData);
      if (this.#sectionPlane) mapper.addClippingPlane(this.#sectionPlane);
      const actor = vtkActor.newInstance();
      actor.setMapper(mapper);
      actor.setPickable(false);
      actor.setVisibility(pipeline.actor.getVisibility());
      actor.setPosition(...(this.#explodedOffsets.get(pipeline.actorName) ?? [0, 0, 0]));
      actor.getProperty().setColor(...scaleRgb(this.#graphicsInteraction.primaryHighlightRgb, 0.7));
      actor.getProperty().setLineWidth(3);
      actor.getProperty().setRepresentationToWireframe();
      actor.getProperty().setLighting(false);
      this.#window.getRenderer().addActor(actor);
      this.#highlightOverlays.push({ actorId: pipeline.actorId, actor, mapper, polyData: null });
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
      actor.getProperty().setColor(...this.#graphicsInteraction.primaryHighlightRgb);
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
      actor.getProperty().setColor(...this.#graphicsInteraction.primaryHighlightRgb);
      actor.getProperty().setPointSize(12);
      actor.getProperty().setLighting(false);
    });
  }

  #addEdgeOverlay(pipeline: ChunkPipeline, positions: readonly number[], lines: readonly number[]): void {
    const polyData = vtkPolyData.newInstance();
    polyData.getPoints().setData(new Float32Array(positions), 3);
    polyData.getLines().setData(new Uint32Array(lines));
    this.#addSelectionActor(pipeline, polyData, (actor) => {
      actor.getProperty().setColor(...this.#graphicsInteraction.primaryHighlightRgb);
      actor.getProperty().setLineWidth(5);
      actor.getProperty().setLighting(false);
    });
  }

  #addPartOverlay(pipeline: ChunkPipeline): void {
    this.#addSelectionActor(pipeline, pipeline.polyData, (actor) => {
      actor.getProperty().setColor(...scaleRgb(this.#graphicsInteraction.primaryHighlightRgb, 0.7));
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

  #clearHighlightOverlays(): void {
    const renderer = this.#window.getRenderer();
    for (const overlay of this.#highlightOverlays) {
      renderer.removeActor(overlay.actor);
      overlay.actor.delete();
      overlay.mapper.delete();
    }
    this.#highlightOverlays.length = 0;
  }
}

function isReservedAnnotation(name: string): boolean {
  return name === 'Measure_Annotation' || name === 'Min_Annotation' || name === 'Max_Annotation';
}

function vectorsNear(first: readonly number[], second: readonly number[], tolerance: number): boolean {
  return first.length === second.length
    && first.every((value, index) => Math.abs(value - second[index]!) <= tolerance);
}

function scaleRgb(rgb: readonly [number, number, number], scale: number): [number, number, number] {
  return [rgb[0] * scale, rgb[1] * scale, rgb[2] * scale];
}

function formatScaleValue(value: number): string {
  if (value === 0) return '0';
  const absolute = Math.abs(value);
  if (absolute >= 10_000 || absolute < 0.001) {
    const [mantissa, exponent] = value.toExponential(3).split('e');
    return `${Number(mantissa)}e${exponent}`;
  }
  return Number(value.toPrecision(4)).toString();
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
