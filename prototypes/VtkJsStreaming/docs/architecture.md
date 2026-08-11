# Prototype architecture

The prototype is split at a renderer-neutral binary boundary:

```text
Synthetic C# source -> immutable scene/contracts -> authenticated HTTP resources
                                              -> JSON WebSocket control
raw typed arrays -> strict TypeScript validation -> future VTK.js chunk pipeline
```

C# owns stable source identifiers and any semantic interpretation. JSON carries only metadata and resource references. Each chunk is independently drawable and its binary topology is versioned separately from scalar resources.

The small fixture is deliberately generated directly into contiguous arrays. It establishes exact byte layout and ID mapping before work begins on the large streaming generator.
