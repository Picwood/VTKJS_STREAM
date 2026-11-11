# M01: first shared material command

Updated 2026-11-09. M01-T01 and M01-T03 are **in progress**. This slice moves
one real browser editor into a host-independent operation/publication service;
it does not complete the milestone. See the
[initial command evidence](evidence/M01-material-2026-11-06/REPORT.md) and
[material identity/rename evidence](evidence/M01-identity-2026-11-09/REPORT.md), and
[material lifecycle evidence](evidence/M01-lifecycle-2026-11-09/REPORT.md).

`ProjectCommandSession` in the Application assembly owns revision checks,
material command receipts and the existing 21-snapshot undo/redo mechanism.
The web host serializes access with its workspace gate and invalidates renderer
state after an accepted publication or history move. Imports, result attachments
and the other pilot editors use that same snapshot history. Elastic creation/editing, material rename, unused removal and solid-section
assignment now have typed commands and receipts. Section targets still use names;
stable entity IDs currently cover materials only.

The browser material actions and compatibility `material-add`, `material-save`
and `section-assign` actions all call this shared service. Existing CLI/desktop service callers of
`ProjectMaterials.SaveElasticCopy` use the same material patch implementation.
The root Prepomac application remains the independent oracle.

## Request and receipt

`POST /api/workspace/commands/material` requires the existing workspace cookie,
origin/custom-header checks, and an `If-Match` header equal to the command's
`expectedRevision`:

```json
{
  "operationId": "04dedc2e-35c2-4fce-bca3-df61a429bb11",
  "expectedRevision": 1,
  "materialId": "9af0c77b-86f0-422d-b660-dba4fe9acf99",
  "youngsModulus": 2000
}
```

Optional `youngsModulus` and `poissonsRatio` values preserve their existing
values when omitted or null. At least one is required. The merged values must
be finite with E > 0 and -1 < nu < 0.5. The command edits one constant elastic
row in an existing non-temperature-dependent material. It preserves the row's
temperature, all other material properties and their order, description,
activation, section references, mesh and retained CAD artifacts. Successful
edits discard stale results, as the existing pilot does.

Unknown JSON fields are rejected. Unsupported archive schemas, definitions
and expressions are refused by persistence before publication. Equations are
still unsupported; they are never silently converted to evaluated numbers.

Material IDs come from workspace readback (`materialIdentities`, plus `id` on
editable material rows). The browser addresses edits by ID. The older
`materialName` field remains accepted; if both ID and name are provided they
must resolve to the same current entity. A deleted ID is rejected even if
another material has since reused its name.

The response includes the current workspace revision and a versioned
`material.elastic.patch/1` receipt: the material ID, complete typed request, source and
committed revisions, and SHA-256 hashes of both PMA files. The returned current
revision can differ from the receipt's committed revision on a retry.
`GET /api/workspace/commands` reads this workspace's material receipts.
`currentCommandId` in workspace readback identifies the active snapshot's
typed command, or is null for an import or another editor.

An identical request with the same operation ID returns its original receipt
without another edit, even if it follows undo, a branch or history eviction.
Reusing the ID for a different payload is HTTP 409. A new operation with an old
revision is also HTTP 409. Rejected commands leave project bytes, revision,
history and receipts unchanged. A failed ID is not reserved. The compatibility
edit route assigns a new ID itself; retry guarantees require the typed route
and reuse of the original request and revision header.

Receipts stay in memory until workspace expiry. They survive snapshot eviction
and branch deletion and are capped at 4,096 accepted typed material commands per
workspace. The cap rejects further material commands before mutation. Undo
restores the previous snapshot and result validity; redo restores the same
command identity. Both advance the workspace revision.

## Create, assign and remove

All routes use the same operation ID, expected revision, workspace guards and
source/result hash receipt contract:

| Route suffix under `/api/workspace/commands/material` | Additional request fields | Receipt kind |
| --- | --- | --- |
| `/create` | `name`, `youngsModulus`, `poissonsRatio` | `material.elastic.create/1` |
| `/assign` | `sectionName`, `materialId` | `material.assign-solid-section/1` |
| `/remove` | `materialId` | `material.remove-unused/1` |

Creation adds one constant isotropic elastic material with a new persistent ID.
It rejects malformed names, surrounding spaces, case-insensitive collisions and
invalid elastic values before publication. Names are not silently trimmed.
Assignment resolves the material ID to its canonical name on an existing solid
section. Section IDs and section rename are not part of this slice.

Removal is limited to unused materials. Typed section references are checked
without regard to case; possible references in raw solver keywords also prevent
removal. The browser offers **Remove unused material** and lists only materials
not assigned to a typed solid section. Server validation remains authoritative.
This does not claim all desktop deletion behavior is implemented.

Create retries return the original ID, including after undo or name recreation.
Remove retries return the original receipt without deleting an entity that was
restored by undo or later recreated with the same name. A new removal request
using the deleted ID is rejected. Undo/redo restores the same identity and object
state; branching discards redo without discarding old command receipts.

The shared-service checks compare the full metadata document around creation
and removal. Rejected operations preserve project bytes, revision, identity map
and receipts. Six real desktop/web material and section states compare exactly:
initial, created, assigned, reassigned, removed and recreated. This bounded
comparison is separate from the earlier rename section-reference deviation.

## Material rename and project persistence

`POST /api/workspace/commands/material/rename` accepts `operationId`,
`expectedRevision`, `materialId` and `newName`, with the same request guards
and retry rules. Its `material.rename/1` receipt records the previous name,
entity ID and both archive hashes. The browser action is **Rename material**.

Rename preserves material order/properties and the entity ID, and updates
all supported typed section assignments. Case-only rename is allowed; a
case-insensitive collision with another material, a malformed name, surrounding
spaces or a no-op rename is rejected. A potential reference in raw custom solver
keywords is refused before publication because those strings have no typed
reference graph. Supported custom definitions unrelated to the name survive.

**Observed Prepomac difference:** the current desktop `materials.rename` leaves
section assignments using the old material name. The web updates these references.
The differential runner retains its raw failing section comparison alongside
exact material-state comparisons and a separate reference-integrity check. It
does not report full exact parity for assigned-material rename or modify the
reference application to make the result pass.

New editable PMAs use archive version **7**, with checksummed
`material-identities.json` containing identity-schema version 1. The map must
cover every material exactly once with unique nonempty IDs and canonical names.
It is capped at 10,000 materials and 2 MiB. Unknown fields/versions, duplicate IDs,
missing entries and dangling names are rejected by project and scene readers.
Version 1–6 archives remain readable: their material IDs derive deterministically
from stored model metadata/mesh and material name, then persist on the next edit.
Older portable binaries that only accept archive versions through 6 cannot open
new version 7 output. PMX interoperability is unchanged and remains a separate gate.

The edit, result-attachment and local-solver paths preserve IDs. Deletion removes
the ID; recreating the name allocates a new ID. Undo restores the old ID from its
snapshot. Downloading/reopening/copying a PMA preserves IDs, without granting any
workspace access. `StaticProjectArchive.Save(path, openedProject)` preserves
identities, results and artifacts; low-level `Save(path, model)` creates new IDs
unless an identity map is explicitly supplied. Command receipts remain ephemeral
and are not embedded in PMAs.

## Verification and remaining contracts

The material differential runner uses Prepomac's real public API. That API
replaces complete property parts: its adapter reads detached copies of the
existing properties, replaces only the Elastic part and submits the resulting
list. It preserves omitted material attributes through Prepomac's own replace
semantics. Browser commands patch named elastic fields in that same material
state. Three material snapshots compare exactly, including all property tables,
hardening, thermal reference temperature, order and material attributes. This
bounded adapter does not replace any original source test.

The shared-service characterization compares the **whole** persisted metadata
document around the edit, allowing only the requested elastic cell to change.
It also checks mesh/CAD payload bytes, custom cylinder and user-keyword retention,
result invalidation/restoration, bad definitions, equation refusal, bad requests,
retry identity, undo branches and history eviction. Real HTTP and Playwright
checks cover ownership, typed command submission and production rendering.

To repeat the differential check against a newly captured reference:

```bash
python3 scripts/capture-prepomac-baseline.py --output artifacts/parity/NEW-reference
python3 scripts/run-material-command-parity.py \
  --baseline artifacts/parity/NEW-reference/manifest.json \
  --output artifacts/parity/NEW-material-command
```

Use `scripts/run-material-identity-parity.py` with the same arguments for the
identity/rename comparison, including its explicit section-reference deviation.
Use `scripts/run-material-lifecycle-parity.py` for creation, assignment, unused
removal and recreation, using a new output directory each time.

Source or runtime drift aborts these runners. Outputs must be new directories.
Build the browser/renderer first with `bash scripts/start-web-workspace.sh --build`;
stop that host before running acceptance checks, which start their own hosts.

Remaining M01 work includes stable identity for entity types beyond materials,
typed commands for the other editors, persistent/versioned command and artifact
contracts, equation preservation, broader custom-content qualification and the
PMX/protection/legacy fixture matrix. Material entity identity is now distinct
from display names and operation IDs; durable hosted revision/journal storage
remains open.

Each operation is atomic. There is no multi-operation transaction endpoint or
hosted Python runner yet. Earlier accepted operations remain accepted if a
subsequent operation fails. Full Prepomac partial-script-failure, regeneration
and journal semantics still require their own source-matched tests. Snapshot
undo and these receipts do not qualify replay parity.
