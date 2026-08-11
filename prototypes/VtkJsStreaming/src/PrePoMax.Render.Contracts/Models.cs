namespace PrePoMax.Render.Contracts;

public sealed record ProtocolVersion(int Major, int Minor);

public sealed record SceneUnits(string Length);

public sealed record SceneBackground(string Top, string Bottom);

public sealed record RenderMaterial(
    IReadOnlyList<double> FrontRgba,
    IReadOnlyList<double> BackRgba,
    double Ambient,
    double Diffuse,
    double Specular,
    bool BackfaceCulling,
    bool SmoothShading,
    double LineWidthPx,
    double PointSizePx);

public sealed record ResourceReference(string ResourceId);

public sealed record ChunkResources(
    ResourceReference Positions,
    ResourceReference TriangleConnectivity,
    ResourceReference SourcePointIds,
    ResourceReference SourceElementIds,
    ResourceReference PointScalars,
    ResourceReference? Normals = null);

public sealed record RenderChunk(
    string ChunkId,
    int Ordinal,
    int Priority,
    IReadOnlyList<double> Bounds,
    int PointCount,
    int TriangleCount,
    ChunkResources Resources);

public sealed record RenderActor(
    string ActorId,
    string Name,
    string? ParentActorId,
    string Layer,
    bool Visible,
    bool Pickable,
    string Representation,
    RenderMaterial Material,
    IReadOnlyList<double> Bounds,
    int TopologyVersion,
    int FieldVersion,
    IReadOnlyList<RenderChunk> Chunks);

public sealed record ScalarFieldMetadata(
    string Name,
    string Component,
    string Units,
    string Association,
    IReadOnlyList<double> ValidRange,
    IReadOnlyList<double> DisplayedRange,
    string InvalidValuePolicy,
    int? MinimumSourcePointId,
    int? MaximumSourcePointId);

public sealed record ResourceDescriptor(
    string ResourceId,
    string Semantic,
    string DataType,
    int Components,
    long ElementCount,
    long ByteLength,
    string ByteOrder,
    string ContentEncoding,
    string Sha256,
    int Version,
    bool Immutable,
    string Uri);

public sealed record SceneManifest(
    ProtocolVersion Protocol,
    string SceneId,
    long SceneRevision,
    string Name,
    SceneUnits Units,
    IReadOnlyList<double> WorldOrigin,
    IReadOnlyList<double> Bounds,
    SceneBackground Background,
    IReadOnlyList<RenderActor> Actors,
    IReadOnlyList<ResourceDescriptor> Resources,
    ScalarFieldMetadata Field,
    DateTimeOffset CreatedUtc);

public sealed record SceneSummary(string SceneId, string Name, long SceneRevision);

public sealed record SessionCreated(string SessionId, string AccessToken, string SceneId);

public sealed record ProtocolMessage(
    string Protocol,
    string MessageId,
    string? CorrelationId,
    string SceneId,
    long SceneRevision,
    string Type,
    DateTimeOffset SentUtc,
    object Payload);

public sealed record ApiError(string Code, string Message, bool Retryable = false);

public sealed record FieldChunkUpdate(
    string ActorId,
    string ChunkId,
    int FieldVersion,
    ResourceDescriptor Resource);

public sealed record FieldUpdatedPayload(
    ScalarFieldMetadata Field,
    IReadOnlyList<FieldChunkUpdate> Chunks);

public sealed record PickResolveRequest(
    string ActorId,
    string ChunkId,
    string RenderPrimitive,
    int PrimitiveIndex,
    int SourceElementId,
    int CandidateSourceNodeId,
    IReadOnlyList<double> WorldPosition,
    IReadOnlyList<double> DisplayPosition,
    IReadOnlyList<double> CameraDirection,
    double SelectionPrecision,
    string Mode);

public sealed record ResolvedPick(
    string ActorId,
    string ChunkId,
    string RenderPrimitive,
    int PrimitiveIndex,
    int SourceElementId,
    int CandidateSourceNodeId,
    IReadOnlyList<int> TriangleSourceNodeIds,
    IReadOnlyList<double> WorldPosition,
    IReadOnlyList<double> DisplayPosition,
    IReadOnlyList<double> CameraDirection,
    double SelectionPrecision,
    string Mode,
    bool Validated);

public sealed record AreaChunkCandidate(
    string ActorId,
    string ChunkId,
    IReadOnlyList<int> PrimitiveIndices);

public sealed record SelectionSectionPlane(
    IReadOnlyList<double> Point,
    IReadOnlyList<double> Normal);

public sealed record AreaResolveRequest(
    IReadOnlyList<AreaChunkCandidate> Chunks,
    IReadOnlyList<double> CameraDirection,
    bool CompletelyInside,
    SelectionSectionPlane? SectionPlane = null);

public sealed record ResolvedAreaTriangle(
    string ActorId,
    string ActorName,
    int SourceElementId,
    IReadOnlyList<int> TriangleSourceNodeIds);

public sealed record ResolvedArea(
    IReadOnlyList<ResolvedAreaTriangle> Triangles,
    IReadOnlyList<double> CameraDirection,
    bool CompletelyInside,
    bool Validated);

public sealed record FrustumResolveRequest(
    IReadOnlyList<IReadOnlyList<double>> Corners,
    IReadOnlyList<double> CameraDirection,
    bool CompletelyInside,
    SelectionSectionPlane? SectionPlane = null);

public sealed record ResolvedFrustum(
    IReadOnlyList<int> SourceNodeIds,
    IReadOnlyList<int> SourceElementIds,
    IReadOnlyList<string> ActorNames,
    IReadOnlyList<double> CameraDirection,
    bool CompletelyInside,
    bool Validated);
