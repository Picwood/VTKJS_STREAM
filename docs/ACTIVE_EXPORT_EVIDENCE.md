# Active-part export preparation

Implemented 2026-09-16. GetActiveInputText and WriteActiveModel now prepare an
independent analysis-mesh snapshot before invoking the production CalculiX writer.
Inactive parts are removed using the existing FeMesh.RemoveParts algorithm, which
filters affected node/element sets and surfaces and updates reference points.
The strict GetInputText/WritePreparedModel API still rejects inactive parts.

FeMesh.CreateExportSnapshot reuses the existing explicit part/node/element copies
and adds owned node sets, element sets, surfaces, reference points and coordinate
systems. Reference-point and coordinate equations are evaluated into independent
export values; this is not a general editable mesh/project clone. Meshing setup
items are omitted because input generation does not use them.

FeModel.CreateActiveExportView borrows model definitions and owns the mesh snapshot.
Export remains synchronous and requires exclusive model access. Temporary mass
section metadata is still managed by CalculixExportState. This is not arbitrary
transaction rollback or concurrent model editing support. The legacy Windows
Write method remains available with its prior whole-model cloning behavior.

Validation:

- The portable suite passes 3,617 checks. Fourteen new assertions across en-US
  and it-IT cover nested snapshot ownership, inactive element/unreferenced-node
  removal, mixed-region filtering, source identity after failed export and
  repeatable complete input generation.
- Native cube/two-step, long-bar, compression and zero-load acceptance uses
  WriteActiveModel. These fixtures validate the snapshot path with active parts;
  inactive-part filtering itself is covered by the portable two-part fixture.
- git diff --check passes. Windows compilation was not run.

Remaining: general model/project cloning, save/reopen persistence, broader native
constraint/contact/pressure workflows, Linux application host and packaging.
