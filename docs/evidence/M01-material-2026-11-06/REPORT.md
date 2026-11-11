# M01 material-command evidence — 2026-11-06

The first shared material-command slice passes. **M01 remains in progress**:
these bounded checks support M01-T01/T03 without claiming their complete gates.
The [implementation contract](../../M01_MATERIAL_COMMANDS.md) describes request,
retry, preservation and history behavior.

## Results

| Check | Outcome |
| --- | --- |
| Prepomac ↔ real web material state, three checkpoints | Exact match |
| Density, plastic hardening, description and property-order corruptions | All four detected |
| Rejected material edit in both applications | State preserved |
| Portable characterization, including 54 new command checks across two cultures | 2,946 passed |
| Real HTTP acceptance, including two isolated production renderers | Passed |
| Playwright import/edit/export/external-results workflow | Passed |
| Existing client unit suite | 98 passed |
| Web build, TypeScript and client production build | Passed |

The initial material carries density, isotropic elastic data, a multiline
Combined plastic law and thermal expansion with reference temperature -40.
Checkpoint 1 changes E from 1000 to 2000 and leaves nu = 0. Checkpoint 2 changes
nu to 0.2 and preserves E = 2000. Both adapters retain all other property data,
order, name, description, activation and temperature-dependence state. The
desktop adapter uses unchanged public material creation/readback/replace APIs;
it supplies the complete property list after replacing only the Elastic part.

The shared-service tests additionally compare the whole persisted metadata
document with exactly one permitted cell change, preserve mesh/CAD bytes and
custom cylinders/user keywords, invalidate stale results and restore them on
undo. They exercise conflicts, unknown definitions/versions, equation refusal,
retry after undo/branch/snapshot eviction, property preservation and redo
discard. HTTP checks confirm receipt hashes and workspace isolation; Playwright
confirms the actual material form submits the typed command route.

## Reference identity and retained evidence

The first attempt correctly refused the earlier M00 capture: root
`PrePoMax/Forms/FrmMain.cs`, `README.md`, `UserControls/ModelTree.cs` and the
PrePoMax/UserControls binaries had changed since that capture. This work did
not modify those root files. A fresh source/runtime archive was captured for
this slice; all captured hashes were verified before and after the comparison.
The M00 full-suite results remain evidence about their original capture.

New reference capture:
`artifacts/parity/reference-material-command-2026-11-06/manifest.json`.
Qualified run:
`artifacts/parity/material-command-2026-11-06-qualified/`.
The current runtime binaries were captured as installed; this slice did not
rebuild or rerun the complete desktop suite. The runtime identity, captured
sources, archives and invocation are recorded in [summary.json](summary.json).
Earlier attempts remain under `artifacts/parity/`.

Reviewable copies:

- [Reference readback](reference.json) and [web readback](web.json).
- [Exact comparison](comparison.json) and [negative controls](negative-controls.json).
- [Typed command receipts](receipts.json) and [run summary](run-summary.json).
- [Domain](domain.log), [HTTP](http.log), [browser](browser.log) and [client unit](client-unit.log) checks.

The Playwright browser uses software WebGL. This is functional rendering
evidence; physical GPU and large-model performance remain later gates.

## Outstanding gates

No PMX/protection/legacy cross-open claim is added. Equation preservation,
stable entity identity across rename/removal, the other typed editors,
durable command/artifact contracts, full source-script coverage and journal/
regeneration/partial-script-failure parity remain open. The command receipts
are temporary workspace state; exported PMAs do not embed this command history.
