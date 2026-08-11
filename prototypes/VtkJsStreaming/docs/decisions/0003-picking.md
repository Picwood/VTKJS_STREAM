# ADR 0003: hybrid rendered-primitive picking

Status: accepted and implemented in WP4.

VTK.js hardware selection identifies a chunk and rendered triangle. Constant-time typed-array lookup maps that triangle to a source element and a candidate source node. The server validates the candidate against immutable topology and returns all three authoritative triangle source-node IDs. C# revalidates domain IDs, resolves exact element faces for surface mode, and remains authoritative for higher-level FE and geometry selection.

The client obtains the concrete selector from the active render-window view; constructing the core selector directly yields only the abstract API shell. Cell association and Z capture are enabled. Actor identity maps directly to a chunk pipeline, the primitive index directly indexes `sourceElementIds`, and exactly three connectivity/position entries are inspected to find the nearest source node. The rendered relative hit is converted to engineering coordinates by adding the manifest's float64 `worldOrigin`.

The server independently verifies scene revision, actor/chunk identity, visibility and pickability, primitive range, element mapping, candidate-node membership, finite vectors, and chunk world bounds before returning `validated: true`.
