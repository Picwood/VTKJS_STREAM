# Material identity and rename — 2026-11-09

Material IDs now persist through PMA save/reopen, rename, result attachment,
unrelated edits, removal and undo. Recreating a deleted name allocates a new ID.
The browser submits material edits and rename by ID. M01-T01/T03 remain **in
progress**; other entities, durable command history, equations, PMX protection/
legacy interoperability and journal replay remain open.

See the [API and persistence contract](../../M01_MATERIAL_COMMANDS.md).

## Validation

| Check | Outcome |
| --- | --- |
| Material state/order/properties at three desktop/web rename checkpoints | Exact match |
| Five invalid/colliding names, including whitespace and case-insensitive collision | Both applications refuse without mutation |
| Web section assignment after rename and case-only rename | Correct references retained |
| Raw complete desktop/web comparison | Two explicit section-reference differences |
| Deliberate density, hardening, material-order and dangling-section errors | All four detected |
| Portable characterization including legacy formats, ID integrity, opaque-reference refusal, undo and stale requests | 3,030 passed across two cultures |
| HTTP acceptance including PMA copy/reopen, receipt isolation and two renderers | Passed |
| Playwright material edit/rename, solver export and external-results display | Passed |
| Existing client tests | 98 passed |
| .NET web and client production builds | Passed |

The [raw comparison](comparison.json) deliberately retains `passed: false`.
The current Prepomac `materials.rename` changes the material name while leaving
the assigned section at `ELASTIC`. At the same checkpoints the web changes that
assignment to `ALLOY`, then `alloy`. This is a documented reference defect and an
intentional web reference-integrity policy, **not full exact parity**.
[Material-only comparison](material-comparison.json) and the separate
[section integrity check](section-policy.json) both pass. No other differences
are permitted; no tolerance or general omission of section comparisons is used.
The root reference application and its original tests were not modified.

## Preserved evidence

- [Reference](reference.json) and [web](web.json) snapshots.
- [Run summary](run-summary.json), [receipts](receipts.json) and [negative controls](negative-controls.json).
- [Domain](domain.log), [HTTP](http.log), [browser](browser.log), [client unit](client-unit.log) and [web build](web-build.log) results.
- [Capture, implementation and evidence hashes](summary.json).

Raw reference capture:
`artifacts/parity/reference-material-identity-2026-11-09/manifest.json`.
Qualified run:
`artifacts/parity/material-identity-2026-11-09-qualified/`.
The runner verifies captured sources and runtime binaries before and after
execution. The existing reference runtime was captured as installed; this slice
does not rebuild the desktop or replace M00's historical full-suite baseline.
The browser check uses software WebGL; this is functional evidence.

The current root `macOS/CaeModel/CaeModel.csproj` also includes four virtual
friction source units added since M00. Its registry hash is updated with an
explicit source-adjustment record. This material slice does not qualify virtual
friction or alter those source files. Prior M00 and material-command evidence
continues to identify its original captures.

Three new desktop verifiers (virtual friction, automatic pressure weighting and
dialog resizing) are registered as `not_run`. M00 is reopened for this current
reference extension; its historical November 6 evidence remains intact. The
roadmap now has 52 cases and 15 custom reference verifiers. No validator rule
was relaxed to treat these new checks as attempted.

## Persistence boundary

New editable output uses PMA archive v7 and identity schema v1. Legacy PMAs
derive reproducible material IDs and store them on their next edit. Malformed,
unknown, duplicate or dangling identity maps are refused before publication;
the scene reader validates them too. The format also accommodates all known
payloads together rather than relying on the former fixed ZIP-entry count.
Older portable builds that only support archive versions through 6 refuse v7.

Raw solver keywords that mention the old material name cause rename to be
refused, preserving the source. The browser cannot safely infer references in
that opaque text. Supported typed section references are updated atomically.
Material IDs identify entities within the project; workspace ownership remains
separate, and command receipts still expire with their workspace.
