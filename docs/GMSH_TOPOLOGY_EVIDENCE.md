# Gmsh data and topology boundary

2026-09-11: the portable CaeMesh build links 85 production source files,
including GmshData (and GmshIdLocation), GmshEdge, GmshSurface and GmshVolume.

## Persistence split

The Windows controller writes GmshData via WriteToFile; GmshCaller reads it with
Tools.LoadDumpFromFile<GmshData>. Changing only the writer would break that
producer/consumer protocol. WriteToFile's unchanged implementation now lives in
GmshData.Legacy.cs, a partial class compiled by the legacy CaeMesh project only.
The portable assembly contains the real data fields without the legacy writer.
The class name, namespace, fields and Serializable attribute remain unchanged.
No new protocol or legacy round-trip compatibility is claimed by this split;
the Windows executable was not run locally.

This makes the in-memory contract available without a throwing persistence stub
or enabling BinaryFormatter on modern .NET. A future portable caller needs an
explicit versioned format and matching reader, including polymorphic setup
items, array-key topology maps and selection data. It cannot yet call WriteToFile.

## Topology correction and evidence

A collapsed four-edge/three-vertex face was repaired into three working edges,
but the public EdgeIds field still held the original four IDs. Later
SetFirstVertexId used those stale IDs against the repaired vertex pairs. The
repair now updates EdgeIds as well. The new regression test failed before this
change and passed after it.

`bash scripts/test-portable.sh` passes on Linux SDK 10.0.401:
60 STL + 178 legacy NCalc oracle + 794 CaeGlobals + 1,258 CaeMesh = **2,290 checks**.
Sixteen new assertions run across en-US and it-IT: triangle corner selection and
rotation, repaired triangle IDs, quadrilateral opposite pairs, unique incident
surfaces, volume face counts and descending topology ID ordering. Existing
compiler warnings remain. Shell syntax and git diff whitespace checks pass.

These are valid-topology fixtures, not malformed-input qualification. In
particular, SetFirstVertexId still assumes a member vertex ID, and topology
constructors assume connected, consistent edge arrays. General degenerate faces,
holes, native Gmsh execution and file transport remain unqualified.

Subsequent status: native Gmsh execution and a bounded application file transport
now pass through [the Linux CAD-to-mesh workflow](LINUX_GMSH_WORKFLOW.md). This
does not qualify the richer GmshData topology/setup transport described above.

Next: remaining FeMesh dependencies, its desktop warning and aggregate cloning,
plus a coordinated portable data transport when the native caller is migrated.
