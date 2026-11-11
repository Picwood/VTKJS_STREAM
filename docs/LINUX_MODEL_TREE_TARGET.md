# Model tree reference

Recorded: 2026-10-29, from the project owner's reference screenshot.

This is the target Model hierarchy for the Linux port. Preserve the labels,
order and parent/child relationships. The tree is the main authoring workspace;
new model workflows belong on their relevant collections and objects.

```text
Model
  Mesh
    Parts
    Node Sets
    Element Sets
    Surfaces
  Features
    Reference Points
    Cylinders
    Coordinate Systems
  Materials
  Sections
  Constraints
  Contacts
    Surface Interactions
    Contact Pairs
  Distributions
  Amplitudes
  Initial Conditions
  Steps
    <step>
      History Outputs
      Field Outputs
      BCs
      Loads
      Defined Fields
Analyses
```

The screenshot establishes the top-level Model/Mesh/Features layout and its
branch order. Contacts and per-step children follow the corresponding original
[ModelTree](../UserControls/ModelTree.cs). Analyses is a separate root beside
Model. Geometry and Results can have their own contexts; they do not change this
Model hierarchy.

Use the original domain icons where available. Empty collections retain their
names; populated collections may display counts. Selection, region preview,
object editing and context actions operate on the production model. Definitions
whose editors are not yet available remain inspectable with that limitation
identified. A branch or icon alone does not establish functional parity.

The [hierarchy checks](../portable/tests/MeshIO.Characterization/ProjectModelTreeChecks.cs)
cover Model branch order, Mesh collections, Features children, Contacts children,
Analyses placement and per-step ordering. See [model tree and sessions](LINUX_MODEL_TREE.md)
and [element-set authoring](LINUX_ELEMENT_SET_EDITING.md) for implemented behavior.
