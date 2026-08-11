# PrePoMax VTK.js + C# Streaming Renderer Specification

**Status:** implementation specification for the first engineering deliverable, with a roadmap to the production renderer  
**Document version:** 1.0  
**Prepared:** 2026-08-05  
**Target readers:** autonomous AI coding agents, maintainers, reviewers, and performance testers  
**Related assessment:** [Linux port evaluation](LINUX_PORT_EVALUATION.md)

---

## 1. Executive decision

Build a new renderer as a thin VTK.js presentation client backed by a C# scene and streaming service. C# remains authoritative for finite-element topology, result interpretation, selection semantics, sectioning, derived fields, and future level-of-detail generation. The browser receives only render-ready surface primitives and the compact identifiers required to map a visual pick back to the PrePoMax model.

Do **not** attempt to port `vtkControl` line by line, expose the complete FE model to JavaScript, or make VTK.js reproduce the VTK C++ filter catalogue. VTK.js is explicitly a JavaScript rewrite focused primarily on `PolyData`, `ImageData`, rendering, and selected filters; it does not aim for filter parity with VTK C++ ([VTK.js overview](https://kitware.github.io/vtk-js/docs/index.html)). This architecture treats that boundary as a design constraint.

The first deliverable is a standalone, cross-platform vertical slice. It must prove that the proposed data contract can:

1. stream and render a synthetic surface with **3,000,000 nodes** without JSON-encoding numeric arrays;
2. display nodal scalar contours and update those scalars without retransmitting topology;
3. pick an actor, rendered triangle, source element, and nearest source node;
4. preserve usable orbit/pan/zoom interaction while the model is loaded;
5. run from the same source tree on Windows and Linux; and
6. publish measured timings and memory use, including an honest pass/fail report.

This first deliverable is deliberately isolated from WinForms and the existing ActiViz control. It proves or disproves the highest-risk assumptions before the application shell and controller are migrated.

---

## 2. Normative language

The words **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, and **MAY** are requirements at the indicated strength.

- **First deliverable** means the vertical slice defined in sections 12–17.
- **Final renderer** means the eventual production replacement for `vtkControl`.
- **Source model** means the authoritative PrePoMax FE/geometry/results data held by C#.
- **Render mesh** means a surface/line/point representation prepared for GPU rendering.
- **Actor** means a stable scene object corresponding normally to a PrePoMax part or overlay.
- **Chunk** means an independently loadable render-mesh partition with its own local indices.
- **Resource** means one immutable or versioned contiguous binary array.

---

## 3. Goals and success criteria

### 3.1 Final goal

Replace the ActiViz-dependent view with a renderer that:

- works on Linux and Windows;
- has no runtime or redistribution dependency on ActiViz;
- retains PrePoMax’s model, result, and selection semantics;
- remains interactive for multi-million-node engineering models;
- can be embedded in the future cross-platform desktop shell;
- exposes a stable renderer boundary so UI and rendering migrations can proceed independently;
- permits server-side/desktop-side algorithms to evolve without rewriting the web client; and
- uses an open-source visualization client whose licence is acceptable to the project. VTK.js is distributed under the BSD 3-Clause licence ([Kitware VTK.js repository](https://github.com/Kitware/vtk-js)). Legal review remains a project responsibility.

### 3.2 First-deliverable outcome

The vertical slice is successful only if all functional gates pass and its benchmark report demonstrates that the 3-million-node path is viable on at least one documented reference workstation. Performance numbers are evidence, not universal guarantees: browser, GPU, driver, viewport resolution, and mesh connectivity materially affect the result.

### 3.3 Non-goals for the first deliverable

The first deliverable MUST NOT include:

- replacement or modification of the production `vtkControl`;
- a WinForms, Avalonia, WebView, or application-shell integration;
- loading a `.pmx` file or using unsafe legacy serialization as a benchmark fixture;
- volume rendering or sending a complete solid-element mesh to JavaScript;
- production section cuts, iso-surfaces, streamlines, animations, symbols, annotations, or export;
- feature parity with the existing renderer;
- public-network or multi-user hosting; or
- a generalized distributed compute platform.

These exclusions keep the experiment capable of giving a clear technical answer.

---

## 4. Repository evidence and migration constraints

This specification is grounded in the current implementation, particularly:

- [`vtkControl/vtkMax/Actor/vtkMaxActorData.cs`](vtkControl/vtkMax/Actor/vtkMaxActorData.cs), the current actor transfer object;
- [`CaeGlobals/CommonClasses/DataExchange/PartExchangeData.cs`](CaeGlobals/CommonClasses/DataExchange/PartExchangeData.cs);
- [`CaeGlobals/CommonClasses/DataExchange/NodesExchangeData.cs`](CaeGlobals/CommonClasses/DataExchange/NodesExchangeData.cs);
- [`CaeGlobals/CommonClasses/DataExchange/CellsExchangeData.cs`](CaeGlobals/CommonClasses/DataExchange/CellsExchangeData.cs);
- [`vtkControl/vtkMax/Actor/vtkMaxActor.cs`](vtkControl/vtkMax/Actor/vtkMaxActor.cs), where data conversion, triangulation, locators, and VTK actors are currently coupled; and
- [`vtkControl/vtkControl.cs`](vtkControl/vtkControl.cs), which owns rendering, picking, sectioning, overlays, result display, and interaction.

### 4.1 Existing data semantics that must survive

`vtkMaxActorData` currently carries:

- stable names and parent names;
- base, selection, and overlay layers;
- visibility/pickability-related attributes;
- front and back colours, opacity, lighting coefficients, line width, and node size;
- representation type: solid, shell, wire, and related modes;
- visualization geometry, model edges, element edges, and a separate cell-locator dataset;
- point and cell values, extreme-value nodes, and animation coordinates;
- contour, smooth-shading, culling, and section eligibility flags; and
- transformed copies and highlighting behaviour through `vtkMaxActor`/`vtkControl`.

The exchange objects presently use jagged managed arrays:

- node IDs: `int[]`;
- coordinates and normals: `double[][]`;
- node values: `float[]`;
- cell IDs: `int[]`;
- connectivity: `int[][]`;
- VTK cell types: `int[]`; and
- cell values: `float[]`.

Jagged arrays are convenient for the current API but are a poor network and large-model representation. The streaming boundary MUST use contiguous arrays.

### 4.2 Current tessellation behaviour

The present `vtkMaxActor` converts render cells as follows:

| Source cell | Render primitives | Source-ID rule |
|---|---:|---|
| vertex | 1 point | source cell ID retained |
| linear line | 1 segment | source cell ID retained |
| quadratic line | 2 segments | source cell ID repeated |
| triangle | 1 triangle | source cell ID retained |
| quad | 2 triangles | source cell ID repeated |
| quadratic triangle | 4 triangles | source cell ID repeated |
| quadratic quad | 6 triangles | source cell ID repeated |

The new C# render-mesh builder MUST reproduce this winding and source-ID mapping before production integration. The first deliverable implements triangles only, but its contract must already make the mapping explicit.

### 4.3 Selection behaviour is not merely visual picking

Current single-click selection obtains a world point and actor, then uses a locator to recover source element and face/edge node IDs. Frustum selection runs against a separate full cell-locator grid and returns global point and cell IDs. Controller code then interprets those IDs as nodes, elements, parts, geometry vertices/edges/surfaces, and angle-connected regions.

Therefore:

- the client MAY identify the visual primitive;
- the client MUST return the world-space hit and compact source IDs;
- C# MUST remain responsible for higher-level selection interpretation;
- the final system MUST provide a server-side spatial index for exact/semantic and frustum queries; and
- a GPU pick alone is not a full replacement for current selection semantics.

### 4.4 Result updates are naturally streamable

The current `UpdateActorScalarField` replaces point scalars while retaining geometry. This becomes a first-class versioned scalar-resource update. Topology MUST not be resent when only a field, component, step, increment, deformation scale, or colour range changes unless the selected operation actually changes geometry.

---

## 5. Architectural principles

1. **C# owns engineering truth.** JavaScript never becomes the canonical FE database.
2. **The client receives render products, not solver objects.** The principal payload is surface `PolyData`-like primitives.
3. **Topology is immutable by version.** New topology creates a new resource/version; scalar updates do not mutate it over the wire.
4. **Numeric bulk data is binary.** JSON is control and metadata only. Base64 is forbidden.
5. **Indices are local; IDs are semantic.** GPU connectivity uses zero-based local indices. Separate arrays map primitives and points to source IDs.
6. **Progress is visible.** Manifest and chunks allow first pixels before the entire scene is ready.
7. **Large allocations are bounded and measured.** Avoid materializing jagged array, flattened array, HTTP copy, browser copy, VTK.js copy, and GPU copy simultaneously.
8. **Picking is hybrid.** GPU selection narrows the candidate; C# supplies authoritative interpretation when required.
9. **The protocol is renderer-neutral.** It may be implemented initially by VTK.js but must not expose VTK.js class names in the C# domain layer.
10. **The local transport is still a security boundary.** Bind only to loopback and require an unguessable session credential.

---

## 6. Target architecture

```text
PrePoMax domain / results (C#)
          |
          | domain objects, requests, source IDs
          v
Rendering abstraction (C#; no VTK or browser types)
  IRenderScene / IRenderCommandSink / ISelectionService
          |
          | RenderActor + RenderChunk + BinaryResource
          v
Render preparation services (C#)
  surface extraction | tessellation | normals | scalar mapping
  chunking | bounds | ID maps | spatial indices | future LOD
          |
          +------------------------------+
          |                              |
          v                              v
ASP.NET Core loopback host          Authoritative CPU state
manifest + binary resources         locators / FE model / results
WebSocket events/commands
          |
          | HTTP binary arrays + JSON control
          v
VTK.js TypeScript client
scene store -> vtkPolyData -> mapper -> actor -> WebGL canvas
          |
          v
camera / interaction / GPU pick / visual selection
```

For the final desktop application the web client is expected to run in an embedded cross-platform web view or a separately managed local browser surface. That embedding choice is deliberately deferred; it must not leak into the protocol.

### 6.1 Why not remote-image rendering

Remote-image rendering would preserve server-side VTK capability but adds video/image latency, image compression, server GPU requirements, and more complex picking. It remains a fallback for extremely large or algorithm-heavy workloads, not the default desktop replacement.

### 6.2 Why this resembles proven CAE systems

Commercial CAE web viewers similarly keep the complete unstructured model server-side and progressively stream display objects. HOOPS/CEETRON documents a `RemoteModel` backed by a server and “3D Progressive Object Streaming” ([architecture overview](https://docs.techsoft3d.com/hoops/envision-web/architecture.html)). This is architectural validation, not an assertion that VTK.js supplies those server capabilities automatically; PrePoMax must build them.

---

## 7. Technology baseline

### 7.1 Backend

- Current stable, supported .NET SDK at implementation start; prefer **.NET 10 LTS** if available in the build environment.
- ASP.NET Core minimal hosting.
- `System.Text.Json` for small control messages.
- Raw `Stream`/`ReadOnlyMemory<byte>` response bodies for resources.
- `System.Net.WebSockets` through ASP.NET Core for bidirectional control events. ASP.NET Core WebSockets are cross-platform and support allowed-origin configuration ([Microsoft documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)).
- xUnit (or the repository’s subsequently adopted standard) for backend tests.

### 7.2 Client

- TypeScript in strict mode.
- Vite for the prototype build.
- Current stable `@kitware/vtk.js`, pinned exactly in `package.json` and the lockfile.
- No React/Vue/Svelte for the vertical slice; a small DOM UI prevents framework concerns from obscuring renderer performance.
- Vitest for pure client tests.
- Playwright for functional browser tests where WebGL is available.

VTK.js `PolyData` and `DataArray` natively use `ArrayBuffer`-backed typed arrays ([PolyData structure](https://kitware.github.io/vtk-js/docs/structures_PolyData.html), [DataArray structure](https://kitware.github.io/vtk-js/docs/structures_DataArray.html)); the protocol is designed to feed these directly.

### 7.3 Version policy

The implementation agent MUST:

- record `dotnet --info`, Node, npm/pnpm, browser, VTK.js, OS, GPU, and driver versions in the benchmark report;
- commit one JavaScript lockfile;
- pin VTK.js exactly for reproducibility;
- avoid prerelease packages unless a documented blocker makes one necessary; and
- add an ADR if it changes the baseline stack.

---

## 8. Repository layout for the first deliverable

Create a new isolated subtree and leave existing solution projects untouched:

```text
prototypes/
  VtkJsStreaming/
    README.md
    VtkJsStreaming.sln
    Directory.Build.props
    src/
      PrePoMax.Render.Contracts/
      PrePoMax.Render.Server/
      PrePoMax.Render.SyntheticData/
      client/
        package.json
        package-lock.json (or pnpm-lock.yaml; exactly one)
        src/
    tests/
      PrePoMax.Render.Contracts.Tests/
      PrePoMax.Render.Server.Tests/
      client/
      e2e/
    docs/
      protocol-v1.md
      architecture.md
      benchmark-report.md
      test-matrix.md
      decisions/
        0001-transport.md
        0002-coordinate-precision.md
        0003-picking.md
    scripts/
      run-dev.ps1
      run-dev.sh
      benchmark.ps1
      benchmark.sh
```

Naming may be adjusted to existing repository conventions, but the separation of contracts, server, generated data, client, tests, and evidence MUST remain.

The new prototype MUST NOT add a reference from an existing production project to a prototype project.

---

## 9. Scene model contract

### 9.1 Identifier rules

- `sceneId`, `actorId`, `chunkId`, and `resourceId` MUST be opaque lowercase UUID strings in JSON.
- A PrePoMax source node/element ID is a signed 32-bit integer today and MUST be carried losslessly as `Int32`.
- GPU/local point and primitive indices MUST be `Uint32` unless a chunk has fewer than 65,536 points and the client explicitly chooses `Uint16` internally.
- IDs MUST never be inferred from array position except where a descriptor explicitly defines that mapping.
- Actor names are labels, not identities; renaming MUST NOT change `actorId`.

### 9.2 Scene manifest

`GET /api/v1/scenes/{sceneId}/manifest` returns UTF-8 JSON of this logical form:

```json
{
  "protocol": { "major": 1, "minor": 0 },
  "sceneId": "...",
  "sceneRevision": 1,
  "name": "synthetic-3m-grid",
  "units": { "length": "m" },
  "worldOrigin": [1234567.0, 2345678.0, 3456789.0],
  "bounds": [xmin, xmax, ymin, ymax, zmin, zmax],
  "background": { "top": "#FFFFFF", "bottom": "#DDE7F0" },
  "actors": [],
  "createdUtc": "2026-08-05T00:00:00Z"
}
```

`worldOrigin` is `Float64` metadata. Render positions are `Float32` offsets from this origin. This prevents loss of local detail when engineering coordinates are far from zero. The client reconstructs world coordinates for display and picks as `worldOrigin + relativePosition`, using JavaScript numbers for CPU values.

### 9.3 Actor manifest

Each actor MUST define:

```json
{
  "actorId": "...",
  "name": "Part-1",
  "parentActorId": null,
  "layer": "base",
  "visible": true,
  "pickable": true,
  "representation": "surface",
  "material": {
    "frontRgba": [0.70, 0.70, 0.75, 1.0],
    "backRgba": [0.85, 0.35, 0.35, 1.0],
    "ambient": 0.15,
    "diffuse": 0.85,
    "specular": 0.0,
    "backfaceCulling": true,
    "smoothShading": true,
    "lineWidthPx": 1.0,
    "pointSizePx": 5.0
  },
  "bounds": [xmin, xmax, ymin, ymax, zmin, zmax],
  "topologyVersion": 1,
  "fieldVersion": 1,
  "chunks": []
}
```

Valid initial layers are `base`, `selection`, and `overlay`, matching current PrePoMax semantics. Valid initial representations are `surface`, `wire`, `points`, and `surface-with-edges`. Later versions MAY add glyph and image representations.

### 9.4 Chunk manifest

Each chunk MUST be self-contained for drawing and SHOULD be independently disposable:

```json
{
  "chunkId": "...",
  "ordinal": 0,
  "priority": 100,
  "bounds": [xmin, xmax, ymin, ymax, zmin, zmax],
  "pointCount": 1048576,
  "triangleCount": 2090000,
  "resources": {
    "positions": { "resourceId": "..." },
    "triangleConnectivity": { "resourceId": "..." },
    "sourcePointIds": { "resourceId": "..." },
    "sourceElementIds": { "resourceId": "..." },
    "normals": { "resourceId": "..." },
    "pointScalars": { "resourceId": "..." }
  }
}
```

Required first-deliverable resources are positions, triangle connectivity, source point IDs, source element IDs, and point scalars. Normals are optional; when absent the client MAY compute them for small fixtures, but the 3-million-node benchmark MUST use server-produced normals or flat shading to avoid an uncontrolled client-side preprocessing spike.

### 9.5 Resource descriptor

Every referenced resource MUST have a descriptor available in the manifest or an adjacent resource table:

```json
{
  "resourceId": "...",
  "semantic": "positions",
  "dataType": "float32",
  "components": 3,
  "elementCount": 3000000,
  "byteLength": 36000000,
  "byteOrder": "little-endian",
  "contentEncoding": "identity",
  "sha256": "lowercase-hex",
  "version": 1,
  "immutable": true,
  "uri": "/api/v1/scenes/.../resources/..."
}
```

Allowed V1 data types are `int32`, `uint32`, and `float32`. `float64` is allowed only for small metadata-like resources and MUST be justified. Browsers and VTK.js support typed arrays for these representations ([VTK.js DataArray documentation](https://kitware.github.io/vtk-js/docs/structures_DataArray.html)).

### 9.6 Binary resource body

The HTTP body is the raw packed array and contains **no in-band header**. Its descriptor is the authoritative schema.

- Byte order MUST be little-endian.
- There MUST be no per-element padding.
- `Content-Type` MUST be `application/vnd.prepomax.render-array`.
- `Content-Length`, `ETag`, and `Cache-Control: private, immutable` MUST be returned for immutable resources.
- The server SHOULD support HTTP range requests for resources larger than 8 MiB.
- The server MUST validate that `byteLength = elementCount × components × sizeof(dataType)`.
- The client MUST reject a length mismatch before constructing a typed view.
- Development/test mode MUST verify SHA-256. Production MAY make full hashing configurable after measuring its cost.
- Gzip/Brotli MUST be disabled for the first performance baseline. General-purpose compression can require extra peak memory and can distort the result; it may be evaluated later per semantic. Geometry-specific compression is a separate future design.

### 9.7 Connectivity representation

`triangleConnectivity` is a flat `Uint32Array` of length `triangleCount × 3`:

```text
t0p0, t0p1, t0p2, t1p0, t1p1, t1p2, ...
```

Each value is a zero-based **chunk-local** point index. On the client it is converted once to VTK.js cell-array layout:

```text
3, t0p0, t0p1, t0p2, 3, t1p0, t1p1, t1p2, ...
```

This conversion adds `4 × triangleCount` bytes and a transient allocation. The benchmark MUST report that cost. If it is a significant bottleneck, protocol V1.1 MAY serve VTK.js packed cell arrays directly, but the renderer-neutral V1 representation is preferred until measured. The official VTK.js `PolyData` structure documents this packed cell format ([documentation](https://kitware.github.io/vtk-js/docs/structures_PolyData.html)).

`sourceElementIds` has one `Int32` per rendered triangle. This intentionally repeats a source ID for tessellated quads/quadratic faces and makes hardware-selected triangle-to-element mapping O(1).

`sourcePointIds` has one `Int32` per chunk-local point. If a point lies in multiple chunks, its source ID may occur in each relevant chunk.

### 9.8 Scalar representation

- `pointScalars` has one `Float32` per point for a scalar field.
- The actor/field metadata MUST include field name, component name, units, data association, valid range, displayed range, and invalid-value policy.
- NaN means unavailable. The client MUST apply a configured NaN colour and MUST NOT include NaN in range calculation.
- Infinity MUST be rejected by the server-side validator.
- The server SHOULD calculate min/max and extreme source-node IDs; the browser MUST NOT scan 3 million values just to reproduce known metadata.
- Updating a scalar field creates a new versioned resource and a `field.updated` event. Position/connectivity resource IDs remain unchanged.

### 9.9 Coordinate and deformation policy

For the final renderer there are two supported update modes:

1. stream complete relative positions for a deformed configuration; or
2. retain base positions and stream `Float32` displacement vectors, applying a deformation scale in the client.

The first deliverable uses base positions only. Deformation is deferred, but its protocol fields MUST be reserved in documentation. C# remains responsible for choosing the mode based on animation length and memory budget.

---

## 10. HTTP and WebSocket protocol V1

### 10.1 HTTP endpoints

| Method and path | Purpose |
|---|---|
| `GET /healthz` | process liveness; no scene details |
| `POST /api/v1/sessions` | create an authenticated local session |
| `GET /api/v1/scenes` | list scenes in the current session |
| `GET /api/v1/scenes/{sceneId}/manifest` | obtain consistent scene snapshot |
| `GET /api/v1/scenes/{sceneId}/resources/{resourceId}` | stream one packed array |
| `GET /api/v1/scenes/{sceneId}/events` | upgrade to WebSocket |
| `POST /api/v1/scenes/{sceneId}/commands` | optional command fallback/test endpoint |

All API responses MUST carry `X-PrePoMax-Protocol: 1.0`. Unsupported major versions return HTTP 426 with a machine-readable error.

### 10.2 WebSocket envelope

WebSocket V1 carries UTF-8 JSON control messages only. Bulk arrays use HTTP resources.

```json
{
  "protocol": "1.0",
  "messageId": "uuid",
  "correlationId": null,
  "sceneId": "uuid",
  "sceneRevision": 1,
  "type": "field.updated",
  "sentUtc": "2026-08-05T00:00:00.000Z",
  "payload": {}
}
```

The receiver MUST ignore unknown payload properties for forward-compatible minor revisions and MUST reject an unknown major protocol version.

### 10.3 Required server events

| Event | Required payload/behaviour |
|---|---|
| `scene.snapshot-required` | client refetches manifest; includes reason |
| `actor.added` | complete actor manifest |
| `actor.removed` | actor ID and new scene revision |
| `actor.properties-updated` | changed properties and actor property version |
| `field.updated` | actor/chunk, field metadata, new resource descriptor |
| `loading.progress` | stage, completed units, total units if known |
| `command.completed` | correlation ID and optional result |
| `command.failed` | stable error code, safe message, retryability |
| `server.shutting-down` | reason and grace interval |

### 10.4 Required client commands

| Command | Purpose |
|---|---|
| `client.ready` | reports renderer/backend capabilities and viewport |
| `actor.set-visibility` | requests visibility change |
| `camera.changed` | throttled camera state for future LOD prioritisation |
| `pick.resolve` | asks server to validate/enrich a visual hit |
| `selection.changed` | reports selected source IDs for authoritative state |
| `resource.cancel` | advisory cancellation for queued generation/transfer |
| `metrics.sample` | optional benchmark/diagnostic sample |

Commands that mutate scene state MUST include the last observed `sceneRevision`. The server rejects stale state with `revision-conflict`, and the client refetches the manifest.

### 10.5 Error model

HTTP errors use RFC 9457-style problem details with stable `code`. Required codes include:

- `protocol-version-unsupported`;
- `session-invalid`;
- `scene-not-found`;
- `resource-not-found`;
- `resource-length-invalid`;
- `resource-generation-cancelled`;
- `revision-conflict`;
- `capacity-exceeded`; and
- `internal-error`.

No response or log may expose arbitrary local file paths, environment secrets, or stack traces outside development mode.

---

## 11. Client rendering design

### 11.1 VTK.js pipeline per chunk

Each surface chunk has its own:

```text
vtkPolyData
  points: Float32Array(relative XYZ)
  polys: Uint32Array(VTK packed triangle cells)
  pointData: Float32Array(active scalar)
        -> vtkMapper
        -> vtkActor
        -> base renderer
```

Source point and element IDs SHOULD remain in application-side typed arrays rather than being uploaded as active GPU attributes unless the selected picking implementation requires them. This avoids unnecessary GPU memory.

The client MUST import only required VTK.js modules, not a monolithic convenience profile, unless measured bundle/build constraints justify it.

### 11.2 Chunking

- Initial target: 250,000–1,000,000 points per chunk, adjusted so each individual resource is normally below 64 MiB.
- Chunks MUST be spatially coherent.
- Shared boundary nodes MAY be duplicated; source IDs preserve identity.
- Manifest order is a coarse initial priority. The final renderer will combine projected size, distance, visibility, and interaction state.
- Loading SHOULD be limited to 2–4 simultaneous bulk requests; the exact default must be benchmarked.
- A chunk becomes drawable after positions and connectivity arrive. Scalars may arrive afterward; the actor initially uses its solid material colour.
- Resource fetches MUST support `AbortController` cancellation.

The synthetic first-deliverable generator MAY create three spatial strips of approximately one million points each. It MUST not generate a single 3-million-node jagged array and then flatten it.

### 11.3 Rendering and interaction

- Use perspective projection by default and provide an orthographic toggle.
- Implement reset-to-bounds, front, back, left, right, top, bottom, and isometric camera actions.
- Render on demand; do not continuously render when the scene and camera are idle.
- During interaction the client MAY hide element-edge overlays or reduce resolution, but it MUST restore quality after interaction ends.
- Device pixel ratio MUST be capped (initially at 1.5) during the large benchmark to prevent high-DPI fill rate from invalidating geometry measurements. Report the cap and physical viewport.
- The client MUST listen for WebGL context loss/restoration and display a recoverable error. VTK.js can manage these events and exposes approximate GPU allocation through `getGraphicsMemoryInfo()` ([RenderWindow API](https://kitware.github.io/vtk-js/api/Rendering_OpenGL_RenderWindow.html)).

### 11.4 Colour mapping

The first deliverable MUST support:

- continuous nodal scalars;
- a configurable min/max display range;
- at least one diverging map and one rainbow/turbo-like map;
- a scalar legend with field name, component, units, minimum, and maximum;
- solid-colour fallback before scalars load; and
- toggling contours without recreating topology.

The final mapping must cover current PrePoMax palettes: CoolWarm, Rainbow, RainbowDesaturated, Turbo, Jet, Warm, Cool, Cividis, Viridis, Plasma, BlackBody, Inferno, Kindlmann, and Grayscale. Exact visual parity is a later golden-image task.

### 11.5 Resource lifetime

When removing/replacing a chunk, the client MUST:

1. remove its actors from all renderers;
2. release VTK.js objects using their supported deletion lifecycle;
3. remove application references to `ArrayBuffer`s;
4. clear pick maps and pending requests; and
5. record post-cleanup memory metrics after a reasonable GC observation window, without relying on forced GC in production.

Repeated scene load/unload is a required leak test.

---

## 12. Picking and selection contract

### 12.1 First-deliverable pick flow

1. User clicks canvas coordinates `(x, y)`.
2. VTK.js hardware selection identifies the actor/chunk and rendered triangle. VTK.js provides an asynchronous `HardwareSelector` API for rectangular or point selection ([API](https://kitware.github.io/vtk-js/api/Rendering_Core_HardwareSelector.html)).
3. Client reads `sourceElementIds[triangleIndex]`.
4. Client obtains the three local vertex indices from connectivity.
5. Client computes/interpolates the relative hit and converts it to world coordinates.
6. Client chooses the nearest of those three vertices and reads `sourcePointIds[localPointIndex]`.
7. Client sends `pick.resolve` with actor ID, chunk ID, rendered triangle index, source element ID, candidate source node ID, world hit, display coordinates, camera direction, and selection mode.
8. Synthetic backend validates the IDs and responds with a resolved hit.
9. Client displays a marker and a small inspector containing all IDs and coordinates.

Required payload:

```json
{
  "actorId": "uuid",
  "chunkId": "uuid",
  "renderPrimitive": "triangle",
  "primitiveIndex": 123,
  "sourceElementId": 4567,
  "candidateSourceNodeId": 890,
  "worldPosition": [1.0, 2.0, 3.0],
  "displayPosition": [640, 360],
  "cameraDirection": [0.0, 0.0, -1.0],
  "mode": "element"
}
```

### 12.2 Accuracy tests

The small deterministic fixture MUST test:

- a triangle with known IDs and coordinates;
- two actors overlapping in screen space but separated in depth;
- a miss on background;
- a hidden and a non-pickable actor;
- picking after camera movement;
- picking after scalar update; and
- world-origin offset coordinates large enough to expose naive `Float32` precision loss.

### 12.3 Final selection evolution

Later phases add:

- area/frustum selection;
- front-only versus through selection;
- node/element/part/geometry modes;
- edge/surface angle growth;
- full face-node and edge-node recovery;
- additive, subtractive, intersect, and replace operations; and
- C# locators over the authoritative volume/cell mesh.

Large selection sets MUST be represented as compressed ranges/bitmaps or a server-side selection handle, not multi-megabyte JSON ID lists. The precise representation is deferred until real selection-density measurements exist.

---

## 13. Large-model budget

### 13.1 Canonical synthetic model

Generate a structured surface grid of exactly:

- dimensions: `2000 × 1500`;
- nodes: `3,000,000`;
- quads logically: `1999 × 1499 = 2,996,501`;
- rendered triangles: `5,993,002`; and
- one smooth deterministic scalar, such as `sin(x) * cos(y) + 0.1 * z`.

The generator emits triangle chunks directly. It MUST be deterministic from a documented seed/formula and MUST assign stable source node and source element IDs. The benchmark MAY define source element IDs per logical quad and repeat each ID for its two triangles.

### 13.2 Approximate raw data size

For an unchunked equivalent:

| Array | Formula | Approximate size |
|---|---:|---:|
| relative positions | 3,000,000 × 3 × 4 | 34.3 MiB |
| source point IDs | 3,000,000 × 4 | 11.4 MiB |
| triangle connectivity | 5,993,002 × 3 × 4 | 68.6 MiB |
| source element IDs | 5,993,002 × 4 | 22.9 MiB |
| nodal scalars | 3,000,000 × 4 | 11.4 MiB |
| optional normals | 3,000,000 × 3 × 4 | 34.3 MiB |
| **raw subtotal without normals** | | **148.6 MiB** |
| **raw subtotal with normals** | | **182.9 MiB** |

VTK.js packed cell layout adds roughly 22.9 MiB (one `Uint32` length prefix per triangle). Chunk boundary duplication, JavaScript/VTK object overhead, graphics-driver copies, framebuffer attachments, picking buffers, and transient conversion buffers are additional. A realistic browser/GPU footprint is therefore several times the raw position array, not 36 MiB.

### 13.3 Required memory discipline

- Backend generation MUST write into pooled/owned contiguous buffers or directly to response streams.
- No path may use `double[][]` for the large fixture.
- A response MUST not be copied into `byte[]`, then `MemoryStream`, then another response buffer.
- The client SHOULD transfer parsing/conversion work to a Web Worker if main-thread long-task measurements exceed the gate.
- Buffers transferred from workers MUST use transferable ownership, not structured-clone copying.
- Client concurrency MUST be bounded.
- Scalar replacement SHOULD reuse the existing VTK data-array object or replace only that array; topology objects MUST survive.
- The benchmark MUST capture transient peak memory, not only steady-state memory.

---

## 14. First deliverable: exact scope and work packages

### WP0 — Baseline and reproducibility

Deliver:

- toolchain/version record;
- the isolated project layout;
- one-command development startup on PowerShell and Bash;
- production builds for server and client;
- a concise architecture ADR; and
- no changes to production renderer files.

Exit criteria:

- `dotnet build` succeeds with warnings treated as errors for new C# code;
- client install/build/test succeeds from its lockfile;
- instructions work on a clean checkout on Windows and Linux, or deviations are documented as failed acceptance items.

### WP1 — Protocol and deterministic small fixture

Deliver:

- `protocol-v1.md` matching this specification;
- immutable C# contract records with validation;
- small two-actor triangle fixture;
- session, manifest, resource, and WebSocket endpoints;
- client schema/type definitions; and
- protocol/length/endian/hash/version tests.

Exit criteria:

- no numeric array appears in manifest JSON;
- all resource descriptors exactly match their bodies;
- malformed IDs, lengths, revisions, and protocol versions produce stable errors;
- little-endian typed values round-trip exactly.

### WP2 — VTK.js viewer

Deliver:

- chunk loader with progress and cancellation;
- VTK.js PolyData construction;
- surface rendering, camera controls, actor visibility, solid/scalar display, and legend;
- loading/error diagnostics panel; and
- context-loss handling.

Exit criteria:

- deterministic fixture renders correctly;
- first chunk renders before a deliberately delayed second chunk;
- camera remains responsive after load;
- hiding/showing an actor does not refetch resources.

### WP3 — Scalar-only update

Deliver:

- backend operation that produces a second scalar field/version;
- `field.updated` event;
- client fetch and replacement of only scalar arrays;
- UI button to switch fields; and
- network assertion that topology resources are not requested again.

Exit criteria:

- scalar colours and legend update;
- position/connectivity object identity or a suitable instrumentation token remains unchanged;
- resource log proves no topology retransmission.

### WP4 — Picking vertical slice

Deliver:

- hardware-selection implementation;
- source-element and nearest-source-node resolution;
- `pick.resolve` request/reply;
- visible hit marker/selection highlight; and
- automated deterministic pick tests where supported, with a manual fallback script if headless WebGL selection is unreliable.

Exit criteria:

- all accuracy cases in section 12.2 pass;
- no O(N) JavaScript scan occurs per pick;
- pick latency is measured and displayed.

### WP5 — Three-million-node benchmark

Deliver:

- streaming synthetic generator;
- configurable chunk size and concurrency;
- timing/memory/long-task instrumentation;
- repeated load/unload test;
- benchmark scripts; and
- a completed report with raw data and conclusions.

Exit criteria:

- exact dataset counts are asserted;
- first pixels appear before all chunks complete;
- topology is transferred once;
- a scalar update transfers scalar bytes only;
- functional interaction/pick still works at full scale;
- every quantitative gate is marked pass or fail, never omitted.

---

## 15. Acceptance criteria

### 15.1 Functional gates — mandatory

- [ ] Server and client run on Windows x64 and a supported Linux x64 distribution.
- [ ] No project in the prototype references `Kitware.VTK`, ActiViz, `System.Windows.Forms`, or `System.Drawing`.
- [ ] Browser requests only loopback URLs in normal operation.
- [ ] Small fixture rendering and ID mapping are deterministic.
- [ ] 3,000,000 nodes and 5,993,002 triangles are loaded and drawn.
- [ ] Chunk progress and first-pixel progressive rendering work.
- [ ] Scalar field update does not retransmit positions or connectivity.
- [ ] Actor visibility does not destroy/reload topology.
- [ ] Point pick returns correct actor, triangle, source element, candidate node, and world point.
- [ ] Invalid resource size/version/hash produces a visible recoverable error.
- [ ] Disconnect/reconnect either resumes safely or performs a clean snapshot reload.
- [ ] Five load/unload cycles do not show monotonically retained actors/resources.
- [ ] Tests and benchmark commands are documented and reproducible.

### 15.2 Initial quantitative gates

Measure in a release build, browser devtools closed except when collecting a required trace, at a documented 1920×1080-equivalent canvas and capped device pixel ratio. Use medians over three cold runs and at least 30 samples for interactive latency.

| Metric | Target | Hard failure threshold |
|---|---:|---:|
| server ready to manifest available | ≤ 2 s | > 5 s |
| manifest to first visible full-resolution chunk | ≤ 5 s | > 12 s |
| complete 3M-node scene load on loopback | ≤ 15 s | > 30 s |
| main-thread longest task during load | ≤ 200 ms | > 1000 ms |
| steady orbit, discrete GPU | ≥ 30 FPS median | < 20 FPS median |
| steady orbit, integrated GPU (if tested) | ≥ 20 FPS median | report only if no reference system |
| point-pick latency | ≤ 100 ms p95 | > 250 ms p95 |
| scalar update after resource is available | ≤ 500 ms | > 1500 ms |
| browser JS/array-buffer peak | ≤ 700 MiB | > 1.2 GiB |
| approximate GPU memory | ≤ 700 MiB | > 1.2 GiB or context loss |
| backend managed-memory peak above baseline | ≤ 400 MiB | > 800 MiB |
| memory retained after five unloads | ≤ 10% of one loaded-scene steady state | > 25% |

These thresholds are initial engineering gates, not promises for every device. If hardware cannot meet them, the agent MUST still deliver the evidence, identify the dominant bottleneck, and recommend one of: smaller chunks, web worker conversion, direct VTK packed connectivity, omission/computation of normals, interaction-quality reduction, LOD, or remote rendering. It MUST NOT weaken a threshold silently.

### 15.3 Network invariants

Automated or instrumented assertions MUST prove:

- manifest JSON remains under 1 MiB for the synthetic scene;
- no response contains Base64 geometry;
- resource byte totals agree with descriptors;
- immutable resource URLs/ETags are stable within a scene revision;
- second scalar field transfer is approximately `4 × pointCount` plus metadata per non-duplicated point/chunk, not the full scene payload;
- changing camera or visibility causes zero bulk-resource requests unless it makes a previously unloaded chunk visible in a later LOD implementation; and
- no single request exceeds the configured resource cap without a documented reason.

---

## 16. Test strategy

### 16.1 C# unit tests

Test:

- descriptor byte-length calculation with checked arithmetic;
- endianness and exact byte layout;
- finite-coordinate/scalar validation;
- bounds and scalar range calculation;
- stable source ID mapping;
- chunk-local connectivity bounds;
- deterministic synthetic generation;
- cancellation during generation/streaming;
- invalid UUID/session/revision handling; and
- resource lifetime/eviction rules.

Property-based tests are encouraged for array counts and malformed connectivity but are not required if they delay the vertical slice.

### 16.2 Server integration tests

Use an in-process test server to verify:

- session authentication;
- manifest schema and protocol headers;
- full and range resource responses;
- ETag/conditional requests;
- WebSocket connect/event/correlation flows;
- stale-revision failure;
- disconnected client cleanup; and
- response cancellation without unobserved exceptions.

### 16.3 Client unit tests

Test:

- descriptor-to-typed-array conversion;
- VTK packed-cell construction;
- length and endianness rejection;
- chunk state machine;
- version/stale-event handling;
- field update without topology replacement;
- source-ID lookup; and
- coordinate origin reconstruction.

### 16.4 Browser end-to-end tests

Test:

- initial loading/progress/error states;
- deterministic screenshot of the small fixture with tolerant image comparison;
- camera controls and reset;
- visibility;
- scalar range/legend change;
- click-pick ID result;
- progressive delayed chunk;
- WebSocket interruption/recovery; and
- context-loss path where browser automation permits it.

Headless software WebGL is suitable for correctness, not GPU performance. Performance acceptance MUST be run interactively on documented physical hardware.

### 16.5 Benchmark measurements

Capture at minimum:

- process start, manifest received, each resource start/end, first actor added, first render, all chunks loaded, first stable frame;
- bytes by resource semantic;
- backend process working set, GC heap, allocated bytes, collection counts, and generation time;
- browser `performance.memory` where available, otherwise clearly label missing values;
- VTK.js approximate graphics memory through the OpenGL render-window API;
- frame-time samples during a repeatable scripted orbit;
- long tasks and main-thread blocking;
- p50/p95 pick time;
- scalar download and activation time; and
- memory before load, steady loaded, after unload, and after five cycles.

Raw CSV/JSON measurement output MUST be committed alongside the human-readable report unless it is prohibitively large; in that case commit a summarized dataset and retrieval instructions.

---

## 17. Security, robustness, and lifecycle

### 17.1 Local-session security

- Bind Kestrel to `127.0.0.1` and `::1` only.
- Select an ephemeral port; do not use a globally fixed development port in production mode.
- Generate at least 128 bits of cryptographically random session entropy.
- Pass the bootstrap token to the trusted desktop/web client without writing it to logs.
- Require the session credential on manifest, resource, command, and WebSocket requests.
- Validate `Origin` as defence in depth, but do not treat it as authentication; Microsoft notes that clients can fake the Origin header ([ASP.NET Core WebSockets guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)).
- Apply a restrictive Content Security Policy and serve client assets from the same local origin.
- Do not expose arbitrary filesystem paths or a generic file-download endpoint.
- Enforce configured maximum counts, sizes, concurrent requests, and command rates.

The development-mode token may be displayed to simplify manual testing, but that exception MUST be obvious and MUST not be the production default.

### 17.2 Backpressure and cancellation

- Each client has a bounded command/event queue.
- Coalescible events such as camera and progress updates SHOULD replace older pending values.
- Resource generation observes request cancellation.
- A slow/disconnected client MUST not retain scene buffers forever.
- On shutdown the server emits `server.shutting-down`, stops accepting work, cancels generation, closes sockets, then disposes resources.

### 17.3 Recovery

- A missed/stale event causes a manifest snapshot reload.
- A resource integrity failure retries once, then stops and reports the failing resource ID.
- WebGL context restoration rebuilds GPU objects from retained CPU arrays when within memory policy; otherwise it refetches the scene.
- An out-of-memory or context-loss event MUST lead to an actionable message suggesting a reduced model/chunk/quality setting, not a blank canvas.

---

## 18. Observability requirements

The server MUST use structured logs with event IDs and correlation IDs. The client MUST expose a downloadable diagnostics JSON. Neither should log a message per node, triangle, or animation frame.

Required diagnostic fields:

- protocol/client/server versions;
- session and scene IDs (safe opaque values);
- browser/WebGL renderer and limits;
- viewport/device pixel ratio;
- scene/actor/chunk/resource counts;
- source/render node and primitive counts;
- bytes planned, transferred, decoded, and resident;
- timings described in section 16.5;
- resource retries/cancellations;
- context-loss count; and
- last safe error code.

The diagnostics panel MUST have a “copy summary” action and MUST avoid credentials.

---

## 19. Mapping from current objects to the new contract

| Current PrePoMax/ActiViz concept | New contract/owner | Notes |
|---|---|---|
| `vtkMaxActorData.Name` | actor `name` | label only; add opaque actor ID |
| `ParentName` | `parentActorId` | resolve name once in adapter |
| `vtkRendererLayer` | actor `layer` | base/selection/overlay retained |
| `Geometry.Nodes.Coor` | positions resource | flatten; origin-relative Float32 |
| `Geometry.Nodes.Ids` | source-point-ID resource | signed Int32 |
| `Geometry.Cells.CellNodeIds` | connectivity resources | C# tessellates to local triangles/lines |
| `Geometry.Cells.Ids` | source-element-ID resource | repeat per generated primitive |
| node/cell `Values` | versioned field resource | point and cell association explicit |
| `ExtremeNodes` | field metadata | computed by C#, small JSON metadata |
| `ModelEdges` | line actor/chunks | optional/lazy resource |
| generated element edges | line actor/chunks | preferably server-generated and lazy |
| `CellLocator` | C# selection service | never required in browser for basic display |
| `ColorTable` | lookup-table metadata | discrete categorical field |
| actor VTK properties | renderer-neutral material | translated by client adapter |
| `Copies`/transforms | actor instance transform | avoid duplicating geometry resources |
| section actor/filter | C# derived render actor | server computation in final system |
| scalar bar/status widgets | HTML/VTK overlay client | UI state driven by field metadata |

The future production adapter SHOULD translate existing `vtkMaxActorData` initially, but the long-term domain boundary SHOULD use new renderer-neutral records so `System.Drawing.Color`, VTK cell constants, and mutable jagged arrays do not become permanent protocol dependencies.

---

## 20. Final-goal roadmap after the vertical slice

### Phase 1 — Renderer-neutral C# boundary

- Introduce `IRenderScene`, commands, events, actor/material/camera records, and cancellation.
- Build an adapter from representative `vtkMaxActorData` instances.
- Move surface tessellation out of `vtkMaxActor` into tested C# preparation services.
- Preserve current source-ID and winding semantics with golden fixtures.

### Phase 2 — Real model and result display

- Geometry and model parts.
- Surface-extracted solid results.
- Nodal and elemental contours.
- deformed coordinates/displacements;
- model and element edges, lazy by view mode;
- visibility, transparency, smooth/flat shading, back-face colours/culling; and
- scalar legends and extrema markers.

### Phase 3 — Selection parity

- C# spatial indices for nearest cell/face/edge and frustum queries;
- node, element, part, geometry, edge, surface, and angle-based modes;
- selection operations and filters;
- overlay actors and large-selection representation; and
- exact comparison fixtures against the current renderer.

### Phase 4 — Derived views and engineering overlays

- section cuts/caps computed in C#;
- exploded views and transforms;
- constraints, loads, arrows, springs, axes, captions, annotations, and widgets;
- probe workflows; and
- image export.

### Phase 5 — Scale and animation

- view-dependent LOD and progressive prioritisation;
- memory-aware cache/eviction;
- result prefetch and displacement/scalar frame streaming;
- delta/quantized updates where validated;
- server-computed iso-surfaces/streamlines as derived actors; and
- optional remote-rendering fallback policy.

### Phase 6 — Desktop integration and cutover

- embed the client in the chosen cross-platform shell;
- formal lifecycle, focus, keyboard, clipboard, file-dialog, and DPI integration;
- package web assets and loopback server securely;
- Linux/Windows installers and GPU support matrix;
- renderer feature flags and side-by-side validation;
- remove ActiViz only after parity gates; and
- document recovery/rollback.

---

## 21. Risks and required mitigations

| Risk | Impact | Required mitigation/evidence |
|---|---|---|
| VTK.js connectivity conversion duplicates memory | high at 6M triangles | measure transient peak; worker/direct packed format if needed |
| WebGL/GPU limits vary | load/context failure | query capabilities; chunk; cap DPR; publish support matrix |
| 3M FE nodes produce far more render vertices after extraction/chunking | underestimated memory | benchmark actual surface outputs later; distinguish FE and render counts everywhere |
| large world coordinates lose Float32 detail | incorrect geometry/picks | origin-relative positions and precision test |
| client tries to reproduce VTK filters | schedule/quality risk | server-owned derived geometry |
| CPU and browser hold duplicate full scenes | memory pressure | resource ownership/lifetime metrics and bounded concurrency |
| hardware picking returns rendered primitive, not FE semantics | wrong selection | explicit primitive-to-source maps plus C# resolver |
| WebView GPU behaviour differs from browser | late integration failure | standalone proof first, then early WebView spike before full port |
| embedded-browser redistribution/licensing | deployment risk | separate legal/packaging review before shell selection |
| silent benchmark optimism | architectural misdecision | fixed canonical dataset, raw measurements, explicit failed gates |
| protocol coupled to VTK.js | replacement cost | renderer-neutral names and raw primitive arrays |
| local server exposed to malicious pages | data/action leak | loopback, token, CSP, validation, no generic filesystem endpoint |

---

## 22. AI-agent execution contract

An implementation agent taking this specification MUST follow this sequence:

1. Read this entire specification, the Linux evaluation, and the repository instruction files, if any.
2. Inspect the cited current source files; do not assume this document replaces source verification.
3. Check the working tree and preserve unrelated user changes.
4. Produce a short task plan mapped to WP0–WP5.
5. Implement only under the isolated prototype subtree unless a reviewed build-file change is strictly required.
6. Establish the deterministic small fixture and protocol tests before the large dataset.
7. Instrument before optimising; retain before/after measurements for material changes.
8. Run functional tests, then the canonical benchmark.
9. Complete every acceptance checkbox as pass/fail with evidence.
10. Stop and report, rather than silently redesign, if a decision would materially alter C# ownership, protocol shape, dataset counts, or acceptance gates.

The agent MUST NOT:

- edit or delete the current ActiViz implementation in this deliverable;
- use the 469 MiB sample `.pmx` as the first automated fixture;
- serialize jagged numerical data to JSON;
- claim “streaming” if the server first builds the entire scene in several duplicate buffers;
- claim 3-million-node success based on a 3-million-value point cloud with no 6-million-triangle topology;
- report FPS without viewport, DPR, GPU, browser, and sampling procedure;
- omit failed tests or adjust thresholds after seeing results without recording the change; or
- add a UI framework or distributed infrastructure not needed by WP0–WP5.

### 22.1 Required handoff artifacts

- source and committed dependency lockfiles;
- `README.md` with Windows/Linux commands;
- protocol and ADR documents;
- automated test suite and test matrix;
- deterministic generator and count assertions;
- raw/summarized benchmark outputs;
- completed benchmark report with screenshots or short capture;
- known limitations and failed gates;
- a prioritized recommendation for Phase 1; and
- a list of any spec deviations with rationale.

---

## 23. Review checklist

Architecture reviewer:

- [ ] C# remains authoritative for FE and semantic selection data.
- [ ] Protocol does not expose mutable domain or VTK.js-specific objects.
- [ ] topology/field versions and scene revisions are unambiguous.
- [ ] final-goal features have a credible server/client owner.

Performance reviewer:

- [ ] canonical counts are exact.
- [ ] raw and transient memory are both measured.
- [ ] first-pixel and full-load times are distinct.
- [ ] frame-time methodology is reproducible.
- [ ] topology-once/scalar-only network invariant is proved.

Security reviewer:

- [ ] loopback-only bind and random session credential exist.
- [ ] WebSocket and HTTP share authorization policy.
- [ ] Origin/CSP are defence in depth, not sole authentication.
- [ ] resource sizes/counts and paths are validated.
- [ ] logs/diagnostics contain no credentials or arbitrary paths.

PrePoMax domain reviewer:

- [ ] actor/layer/material/source-ID semantics map correctly.
- [ ] current tessellation and element-ID repetition are understood.
- [ ] pick result contains enough data for controller interpretation.
- [ ] plan acknowledges separate full-cell locators for final selection parity.
- [ ] scalar/extreme-node and transformed-copy behaviour have future owners.

---

## 24. Decisions intentionally deferred

The first deliverable should generate evidence for, but MUST NOT prematurely lock:

- Avalonia versus another cross-platform desktop shell;
- WebView implementation and redistribution package;
- raw WebSockets versus SignalR for the production control channel;
- direct VTK packed connectivity versus renderer-neutral connectivity;
- compression/quantization format;
- exact chunk size and concurrency defaults;
- WebGL versus VTK.js WebGPU backend for production;
- surface-extraction implementation/library;
- CPU spatial-index implementation;
- LOD algorithm and persistent cache;
- remote-rendering fallback; and
- production deployment topology beyond single-user loopback.

Each deferred decision should become an ADR only after a measured prototype or a concrete integration constraint exists.

---

## 25. Final recommendation

Proceed with the vertical slice exactly as a risk-reduction project, not as a partial UI port. The option is technically credible for a 3-million-node model if “node count” is treated together with resulting primitive count and memory amplification. The architecture will fail if JavaScript is made responsible for the full FE model or if millions of values are transported as object-heavy JSON. It has a realistic path if C# creates chunked surface representations, resources are contiguous binary typed arrays, topology is retained, updates are field-only, and semantic operations remain server-side.

The first deliverable is the decision gate. If it meets the functional criteria and remains inside the hard performance/memory limits, begin Phase 1 and integrate renderer-neutral contracts. If it misses those limits, use the captured evidence to test direct packed connectivity, workers, LOD, or a remote-rendering fallback before committing to the broader Linux UI migration.

