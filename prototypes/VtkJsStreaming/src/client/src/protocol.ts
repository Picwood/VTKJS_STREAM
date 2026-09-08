export const PROTOCOL_VERSION = '1.0' as const;

export type DataType = 'int32' | 'uint32' | 'float32' | 'float64';

export interface ResourceDescriptor {
  readonly resourceId: string;
  readonly semantic: string;
  readonly dataType: DataType;
  readonly components: number;
  readonly elementCount: number;
  readonly byteLength: number;
  readonly byteOrder: 'little-endian';
  readonly contentEncoding: 'identity';
  readonly sha256: string;
  readonly version: number;
  readonly immutable: boolean;
  readonly uri: string;
}

export interface ResourceReference {
  readonly resourceId: string;
}

export interface ChunkResources {
  readonly positions: ResourceReference;
  readonly triangleConnectivity: ResourceReference;
  readonly sourcePointIds: ResourceReference;
  readonly sourceElementIds: ResourceReference;
  readonly pointScalars: ResourceReference;
  readonly normals?: ResourceReference;
}

export interface RenderChunk {
  readonly chunkId: string;
  readonly ordinal: number;
  readonly priority: number;
  readonly bounds: readonly number[];
  readonly pointCount: number;
  readonly triangleCount: number;
  readonly resources: ChunkResources;
}

export interface RenderActor {
  readonly actorId: string;
  readonly name: string;
  readonly visible: boolean;
  readonly pickable: boolean;
  readonly bounds?: readonly number[] | undefined;
  readonly material: {
    readonly frontRgba: readonly [number, number, number, number];
    readonly backRgba: readonly [number, number, number, number];
  };
  readonly chunks: readonly RenderChunk[];
}

export interface ScalarFieldMetadata {
  readonly name: string;
  readonly component: string;
  readonly units: string;
  readonly association: 'point';
  readonly validRange: readonly [number, number];
  readonly displayedRange: readonly [number, number];
  readonly invalidValuePolicy: string;
}

export interface SceneManifest {
  readonly protocol: { readonly major: number; readonly minor: number };
  readonly sceneId: string;
  readonly sceneRevision: number;
  readonly name: string;
  readonly worldOrigin: readonly [number, number, number];
  readonly bounds: readonly number[];
  readonly actors: readonly RenderActor[];
  readonly resources: readonly ResourceDescriptor[];
  readonly field: ScalarFieldMetadata;
}

export interface SessionCreated {
  readonly sessionId: string;
  readonly accessToken: string;
  readonly sceneId: string;
}

export interface FieldChunkUpdate {
  readonly actorId: string;
  readonly chunkId: string;
  readonly fieldVersion: number;
  readonly resource: ResourceDescriptor;
}

export interface FieldUpdatedPayload {
  readonly field: ScalarFieldMetadata;
  readonly chunks: readonly FieldChunkUpdate[];
}

export interface ActorVisibilityUpdatedPayload {
  readonly actors: readonly {
    readonly actorId: string;
    readonly visible: boolean;
  }[];
}

export interface StandardCameraState {
  readonly kind: 'standard';
  readonly view: 'reset' | 'isometric' | 'front' | 'back' | 'left' | 'right' | 'top' | 'bottom';
}

export interface CustomCameraState {
  readonly kind: 'custom';
  readonly position: readonly [number, number, number];
  readonly focalPoint: readonly [number, number, number];
  readonly viewUp: readonly [number, number, number];
  readonly parallelScale: number;
  readonly operation?: 'set' | 'fit' | 'adjust';
}

export type CameraUpdatedPayload = StandardCameraState | CustomCameraState;

export interface SceneReplacedPayload {
  readonly reason: string;
  readonly sceneId: string;
  readonly camera: CameraUpdatedPayload;
}

export interface ActorPresentationUpdatedPayload {
  readonly actors: readonly {
    readonly actorId: string;
    readonly name: string;
    readonly frontRgba: readonly [number, number, number, number];
  }[];
}

export interface HostActorContour {
  readonly name: string;
  readonly colorContours: boolean;
}

export interface HostActorContourStateMessage {
  readonly type: 'prepomax.actor-contours.updated-v1';
  readonly actors: readonly HostActorContour[];
}

export interface ActorRemovedPayload {
  readonly actorIds: readonly string[];
}

export interface FrameUpdatedPayload {
  readonly field: ScalarFieldMetadata;
  readonly bounds: readonly [number, number, number, number, number, number];
  readonly actors: readonly {
    readonly actorId: string;
    readonly bounds: readonly [number, number, number, number, number, number];
  }[];
  readonly chunks: readonly {
    readonly actorId: string;
    readonly chunkId: string;
    readonly bounds: readonly [number, number, number, number, number, number];
    readonly positions: ResourceDescriptor;
    readonly pointScalars: ResourceDescriptor;
  }[];
}

export interface PickResolveRequest {
  readonly actorId: string;
  readonly chunkId: string;
  readonly renderPrimitive: 'triangle';
  readonly primitiveIndex: number;
  readonly sourceElementId: number;
  readonly candidateSourceNodeId: number;
  readonly worldPosition: readonly [number, number, number];
  readonly displayPosition: readonly [number, number];
  readonly cameraDirection: readonly [number, number, number];
  readonly selectionPrecision: number;
  readonly mode: 'element';
}

export interface ResolvedPick extends PickResolveRequest {
  readonly validated: true;
  readonly triangleSourceNodeIds: readonly [number, number, number];
}

export interface PickCompletedPayload {
  readonly commandType: 'pick.resolve';
  readonly result: ResolvedPick;
}

export interface AreaChunkCandidate {
  readonly actorId: string;
  readonly chunkId: string;
  readonly primitiveIndices: readonly number[];
}

export interface SelectionSectionPlane {
  readonly point: readonly [number, number, number];
  readonly normal: readonly [number, number, number];
}

export interface AreaResolveRequest {
  readonly chunks: readonly AreaChunkCandidate[];
  readonly cameraDirection: readonly [number, number, number];
  readonly completelyInside: boolean;
  readonly sectionPlane?: SelectionSectionPlane;
}

export interface ResolvedAreaTriangle {
  readonly actorId: string;
  readonly actorName: string;
  readonly sourceElementId: number;
  readonly triangleSourceNodeIds: readonly [number, number, number];
}

export interface ResolvedArea {
  readonly triangles: readonly ResolvedAreaTriangle[];
  readonly cameraDirection: readonly [number, number, number];
  readonly completelyInside: boolean;
  readonly validated: true;
}

export interface AreaCompletedPayload {
  readonly commandType: 'area.resolve';
  readonly result: ResolvedArea;
}

export interface FrustumResolveRequest {
  readonly corners: readonly (readonly [number, number, number])[];
  readonly cameraDirection: readonly [number, number, number];
  readonly completelyInside: boolean;
  readonly sectionPlane?: SelectionSectionPlane;
}

export interface ResolvedFrustum {
  readonly sourceNodeIds: readonly number[];
  readonly sourceElementIds: readonly number[];
  readonly actorNames: readonly string[];
  readonly cameraDirection: readonly [number, number, number];
  readonly completelyInside: boolean;
  readonly validated: true;
}

export interface FrustumCompletedPayload {
  readonly commandType: 'frustum.resolve';
  readonly result: ResolvedFrustum;
}

export type HostSelectionOperation = 'replace' | 'add' | 'subtract' | 'intersect';

export interface HostValidatedPickMessage {
  readonly type: 'prepomax.pick.validated-v1';
  readonly validated: true;
  readonly sourceElementId: number;
  readonly sourceNodeId: number;
  readonly triangleSourceNodeIds: readonly [number, number, number];
  readonly worldPosition: readonly [number, number, number];
  readonly cameraDirection: readonly [number, number, number];
  readonly selectionPrecision: number;
  readonly actorName: string;
  readonly operation: HostSelectionOperation;
}

export interface HostValidatedAreaMessage {
  readonly type: 'prepomax.area.validated-v1';
  readonly validated: true;
  readonly triangles: readonly ResolvedAreaTriangle[];
  readonly cameraDirection: readonly [number, number, number];
  readonly completelyInside: boolean;
  readonly operation: HostSelectionOperation;
  readonly locatorValidated?: boolean;
  readonly sourceNodeIds?: readonly number[];
  readonly sourceElementIds?: readonly number[];
  readonly actorNames?: readonly string[];
}

export interface HostSelectionStateMessage {
  readonly type: 'prepomax.selection.updated-v1';
  readonly item: 'none' | 'node' | 'element' | 'surface' | 'geometry-edge' | 'geometry' | 'part' | 'unsupported';
  readonly ids: readonly number[];
  readonly actorNames: readonly string[];
  readonly faceNodeIds: readonly (readonly number[])[];
  readonly edgeNodeIds: readonly (readonly number[])[];
}

export interface HostActorHighlightStateMessage {
  readonly type: 'prepomax.actor-highlights.updated-v1';
  readonly actorNames: readonly string[];
}

export interface HostAnnotation {
  readonly name: string;
  readonly text: string;
  readonly anchorPoint: readonly [number, number, number];
  readonly offset: readonly [number, number];
  readonly drawBackground: boolean;
  readonly drawBorder: boolean;
  readonly visible: boolean;
}

export interface HostAnnotationStateMessage {
  readonly type: 'prepomax.annotations.updated-v1';
  readonly annotations: readonly HostAnnotation[];
}

export interface HostSphereSymbol {
  readonly name: string;
  readonly points: readonly (readonly [number, number, number])[];
  readonly rgba: readonly [number, number, number, number];
  readonly symbolSize: number;
}

export interface HostSphereSymbolStateMessage {
  readonly type: 'prepomax.sphere-symbols.updated-v1';
  readonly symbols: readonly HostSphereSymbol[];
}

export interface HostTransientPrimitive {
  readonly name: string;
  readonly points: readonly (readonly [number, number, number])[];
  readonly lines: readonly (readonly [number, number])[];
  readonly drawPoints: boolean;
  readonly rgba: readonly [number, number, number, number];
  readonly pointSize: number;
  readonly lineWidth: number;
  readonly visible?: boolean;
}

export interface HostTransientPrimitiveStateMessage {
  readonly type: 'prepomax.transient-primitives.updated-v1';
  readonly primitives: readonly HostTransientPrimitive[];
}

export interface HostCoordinateAxis {
  readonly name: string;
  readonly point: readonly [number, number, number];
  readonly direction: readonly [number, number, number];
  readonly rgba: readonly [number, number, number, number];
  readonly symbolSize: number;
}

export interface HostCaption {
  readonly name: string;
  readonly text: string;
  readonly position: readonly [number, number, number];
  readonly offsetVector: readonly [number, number, number] | null;
  readonly rgba: readonly [number, number, number, number];
  readonly fontScaleFactor: number;
}

export interface HostCoordinateOverlayStateMessage {
  readonly type: 'prepomax.coordinate-overlays.updated-v1';
  readonly axes: readonly HostCoordinateAxis[];
  readonly captions: readonly HostCaption[];
}

export interface HostDirectionalGlyph {
  readonly name: string;
  readonly kind: 'displacement-cone' | 'general-cone' | 'rotational-constraint' | 'oriented-arrow' | 'double-arrow' | 'oriented-spring' | 'oriented-thermal' | 'oriented-flux';
  readonly points: readonly (readonly [number, number, number])[];
  readonly directions: readonly (readonly [number, number, number])[];
  readonly rgba: readonly [number, number, number, number];
  readonly symbolSize: number;
  readonly invert?: boolean;
  readonly center?: boolean;
  readonly relativeSize?: number;
}

export interface HostDirectionalGlyphStateMessage {
  readonly type: 'prepomax.directional-glyphs.updated-v1';
  readonly glyphs: readonly HostDirectionalGlyph[];
}

export interface HostViewportAppearanceMessage {
  readonly type: 'prepomax.viewport-appearance.updated-v1';
  readonly gradient: boolean;
  readonly topRgb: readonly [number, number, number];
  readonly bottomRgb: readonly [number, number, number];
  readonly ambient: number;
  readonly diffuse: number;
  readonly redraw: boolean;
}

export interface HostOrientationMarkerMessage {
  readonly type: 'prepomax.orientation-marker.updated-v1';
  readonly visible: boolean;
}

export interface HostScaleWidgetMessage {
  readonly type: 'prepomax.scale-widget.updated-v1';
  readonly visible: boolean;
  readonly unit: string;
}

export interface HostScalarLegendMessage {
  readonly type: 'prepomax.scalar-legend.updated-v1';
  readonly visible: boolean;
  readonly fieldName: string;
  readonly componentName: string;
  readonly unit: string;
  readonly complexComponent: string;
  readonly minMaxType: string;
  readonly numberFormat: string;
  readonly drawBackground: boolean;
  readonly drawBorder: boolean;
  readonly minimum: number;
  readonly maximum: number;
  readonly colors: readonly (readonly [number, number, number])[];
}

export interface HostColorBarMessage {
  readonly type: 'prepomax.color-bar.updated-v1';
  readonly visible: boolean;
  readonly drawBackground: boolean;
  readonly drawBorder: boolean;
  readonly colors: readonly (readonly [number, number, number])[];
  readonly labels: readonly string[];
}

export interface HostStatusBlockMessage {
  readonly type: 'prepomax.status-block.updated-v1';
  readonly visible: boolean;
  readonly drawBackground: boolean;
  readonly drawBorder: boolean;
  readonly lines: readonly string[];
}

export interface HostResultWidgetLayoutMessage {
  readonly type: 'prepomax.result-widget-layout.updated-v1';
  readonly scalarLegend: Readonly<{ top: number; left: number }>;
  readonly colorBar: Readonly<{ top: number; left: number }>;
  readonly statusBlock: Readonly<{ top: number; right: number }>;
}

export interface HostGraphicsInteractionMessage {
  readonly type: 'prepomax.graphics-interaction.updated-v1';
  readonly pointSmoothing: boolean;
  readonly lineSmoothing: boolean;
  readonly primaryHighlightRgb: readonly [number, number, number];
  readonly secondaryHighlightRgb: readonly [number, number, number];
  readonly mouseHighlightRgb: readonly [number, number, number];
}

export interface HostTransformationStateMessage {
  readonly type: 'prepomax.transformations.updated-v1';
  readonly visible: boolean;
  readonly matrices: readonly (readonly number[])[];
}

export type HostAnnotationAction = 'reset' | 'delete' | 'settings';

export interface HostAnnotationActionMessage {
  readonly type: 'prepomax.annotation.action-v1';
  readonly name: string;
  readonly action: HostAnnotationAction;
}

export interface ProtocolMessage<TPayload = unknown> {
  readonly protocol: '1.0';
  readonly messageId: string;
  readonly correlationId: string | null;
  readonly sceneId: string;
  readonly sceneRevision: number;
  readonly type: string;
  readonly sentUtc: string;
  readonly payload: TPayload;
}

export type SupportedTypedArray = Int32Array | Uint32Array | Float32Array | Float64Array;
