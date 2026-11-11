# Material lifecycle commands — 2026-11-09

Elastic material creation, existing solid-section assignment and unused-material
removal now use the shared typed command/publication path. The browser submits
material IDs for assignment and removal; the legacy create and assignment routes
also produce domain receipts. M01-T01/T03 remain **in progress** for their wider
entity, equation, project and journal contracts.

See the [API contract](../../M01_MATERIAL_COMMANDS.md#create-assign-and-remove).

## Results

| Check | Outcome |
| --- | --- |
| Initial, created, assigned, reassigned, removed and recreated material/section states against Prepomac | All six match exactly |
| Five malformed/colliding names and negative modulus | Both applications refuse without mutation |
| Corrupted created modulus, assignment, removed target and recreated ratio | All four detected |
| Shared-service characterization, including 86 additional lifecycle checks across two cultures | 3,116 passed |
| Real HTTP ownership, typed lifecycle, compatibility and renderer checks | Passed |
| Playwright edit/rename/export/results and create/assign/remove/undo workflows | Two passed |
| Existing client unit suite | 98 passed |
| .NET web and client production builds | Passed |

The reference adapter uses the unchanged public `materials.add`,
`sections.replace_solid` and `materials.remove` APIs. The fixture's existing
material includes density, a multiline Combined plastic law and thermal
expansion; that state and material order survive each operation. The six
readbacks compare property tables, names, attributes and section references
without numeric tolerances. Persistent browser IDs are qualified separately;
the desktop API has no equivalent persistent material-ID response.

The shared-service tests compare the complete metadata document around creation
and removal, preserving every other definition, mesh and retained geometry.
They check result invalidation/restoration, duplicate names, invalid values,
case-insensitive section references, opaque solver references, failed-operation
ID reuse, source/result hashes, stale requests, cross-command ID collisions,
undo/redo, branching and retries after deletion/name recreation. A deleted ID
cannot target a replacement that reuses its name.

HTTP checks cover typed and compatibility routes, empty-workspace refusal,
unknown fields and real workspace isolation. Browser checks use actual form
submission, verify that assigned materials are excluded from removal choices,
and restore the original material ID through undo/redo. Software WebGL is used;
these are functional checks, not large-model/GPU qualification.

## Evidence

- [Reference](reference.json), [web](web.json) and [exact comparison](comparison.json).
- [Command receipts](receipts.json), [negative controls](negative-controls.json) and [run summary](run-summary.json).
- [Domain](domain.log), [HTTP](http.log), [browser](browser.log), [client unit](client-unit.log) and [web build](web-build.log) results.
- [Capture, implementation and evidence hashes](summary.json).

Reference capture:
`artifacts/parity/reference-material-lifecycle-2026-11-09/manifest.json`.
Qualified run:
`artifacts/parity/material-lifecycle-2026-11-09-qualified/`.
Reference sources/runtime are checked against that capture before and after
execution. The installed desktop runtime is captured as-is; the complete
desktop suite and pending M00 reference extensions were not rerun here. Earlier
captures and outcomes remain immutable historical evidence.

## Limits

Removal deliberately covers **unused** materials. Any typed section reference
(including a different case) or possible raw-keyword reference prevents it.
Assigned-material deletion and automatic cascading deletion are not qualified.
Creation covers constant isotropic elasticity. Section targets still use names.
Other material properties/editors, other entity IDs, equations, durable command
history, PMX interoperability and regeneration/journals remain open.

This exact lifecycle comparison does not overwrite the earlier documented
[desktop rename/section-reference discrepancy](../M01-identity-2026-11-09/REPORT.md).
