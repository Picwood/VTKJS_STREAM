# Element-set authoring through the model tree

Validated: 2026-10-29.

The owner's [reference model tree](LINUX_MODEL_TREE_TARGET.md) remains the UI
target. **Model → Mesh → Element Sets** now supports creation, membership
editing, rename, duplicate and removal through context actions. Double-click
an existing set to edit it. Changes Apply to the current session and participate
in Save/Save as and Undo/Redo.

## Membership and assignment

Choose one membership source:

- **Element IDs / ranges:** explicit IDs separated by commas/spaces, or inclusive
  `START:END[:STEP]` ranges. For example, `10, 20, 30:100:10`. Duplicate IDs are
  combined; missing IDs, malformed/reversed ranges and nonpositive values are
  rejected.
- **Whole mesh parts:** select one or more existing parts. The production
  `FeElementSet.CreatedFromParts` representation retains actual part IDs,
  including sparse IDs, rather than treating them as element IDs.
- **Keep existing membership:** retain the complete existing representation
  while changing its name. This also works when membership is too large to show
  in the text entry.

**Use elements from view selection** fills the ID entry from the owning elements
of already validated selected faces. Existing click, rectangle and angle-growth
face-selection limits continue to apply. This reuses surface selection; general
through-volume element picking remains open.

**Preview set nodes** displays node markers for the candidate membership on the
undeformed mesh. Tree selection also previews a saved set. Preview does not
change the archive. New and duplicated sets become selected in the tree after
Apply, and rename preserves the selected object identity.

Right-click a part or element set and choose **Create section on region…** to
open the solid-section editor with that region already selected. Materials and
assignment rules use the existing production section service. Set membership
changes affect the assignments that reference it; coverage is recalculated when
the project reopens.

## References and persistence

Rename updates typed named references in sections, constraints, contact pairs,
initial conditions, per-step supports, loads, fields and history requests, and
surface face-to-set mappings. This includes inactive definitions. Existing
surface node membership and face labels remain intact when its backing set is
renamed. Duplicates receive independent membership and do not retarget consumers
of the original.

Removal is refused when a definition still references the set. The editor lists
those definitions so they can be reassigned first. No definitions are removed
implicitly. Sets backing element surfaces can be renamed or duplicated; their
membership is protected until the surface workflow can update the dependent
surface data consistently. The dialog identifies that restriction.

Custom CalculiX keywords can contain references that are not represented by
typed model properties. Projects containing them are inspectable, but this
editor refuses mutation until those references can be tracked.

No project schema change was needed. Edits preserve the mesh, other definitions
and retained geometry, invalidate stale results, and use the shared new-snapshot
publisher. Failed operations leave the source archive unchanged. The CLI and
private companion expose the same application service.

```sh
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll \
  add-element-set source.pma authored.pma REGION ids '10,20,30:100:10'
dotnet portable/PrePoMax.Cli/bin/Release/net10.0/PrePoMax.Cli.dll \
  edit-element-set authored.pma renamed.pma REGION RENAMED keep -
```

Input bounds are 60,000 characters and 100,000 expanded IDs per explicit
expression, or 1,000 selected mesh parts. Whole-part selection and retaining an
existing set do not introduce an additional element-count cap. Preview remains
bounded to 10,000 nodes and inventory replies to 1 MiB. Large sparse memberships
that exceed the text bound remain intact through Keep existing membership.

## Verification

The portable suite passes **5,719 checks**, including **2,704 mesh I/O checks**.
New checks cover membership/stride parsing, sparse part IDs, section targets,
per-step and inactive reference updates, surface mapping preservation, duplicate
independence, dependent-removal refusal, malformed/oversized input, keyword
restrictions, result invalidation, source preservation and failed-publication
cleanup. Hierarchy checks now explicitly preserve the owner's reference branch
order and Features children.

GTK X11 with software WebGL passes the complete tree workflow on both a
two-step result fixture and a two-part mesh: create, preview, assign a section,
edit membership/rename, duplicate/remove, refuse dependent removal, Undo/Redo
and Save as/reopen. Native CalculiX 2.21 solves the authored two-part project;
DAT reports U1=0.001 at all eight loaded-face nodes after the new section expands
to both parts. Source bytes remain unchanged.

A relocated framework-dependent bundle also passes checksum and original-icon
asset verification, CLI set operations, the full GTK workflow and that native
solve through its packaged CLI. See the
[acceptance record](evidence/linux-element-sets-2026-10-29.txt).

```sh
bash scripts/test-portable.sh
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh /path/to/multipart-project.pma \
  --smoke-element-sets /path/to/new-authored-project.pma --smoke-timeout 90
```

Node-set/surface maintenance, additional mesh integration and constraint/contact
authoring remain open. This slice does not establish large-model, physical-GPU,
Wayland or clean-machine qualification.
