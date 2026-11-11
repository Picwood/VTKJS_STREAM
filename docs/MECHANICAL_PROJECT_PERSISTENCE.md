# Mechanical project metadata version 2

Implemented 2026-09-17. Editable static metadata now supports FixedBC, CLoad
(node IDs or named node sets), GravityLoad and CentrifLoad in addition to the
existing displacement/rotation and surface-traction definitions. Numeric force
components, phase, region, centrifugal origin/axis/speed and applicable load
settings are restored into their production types. Equation callbacks maintain
derived magnitudes after reopening.

Elastic materials may include one Density property. The density/temperature
table and ordering relative to Elastic are preserved. Duplicate or unsupported
material properties are rejected; there is no silent material conversion.

This milestone introduced metadata version 2. Version 1 metadata remains readable, while
version 1 documents claiming version 2 types are rejected. The outer archive
remains version 2: its existing model.json payload already provides a separately
versioned metadata document. Unsupported model definitions continue to fail
before replacing an existing project. The one-part C3D8/numeric/static boundary
and other exclusions in STATIC_PROJECT_PERSISTENCE.md remain in force.

Validation:

- Portable suite: 3,711 checks, including 36 new assertions across en-US/it-IT
  covering boundary/load identity and values, force/gravity magnitudes, phase,
  centrifugal axis/speed, both elastic/density orderings, independent edits,
  repeated save/reopen and metadata-version compatibility.
- Native acceptance retains five existing cases and adds a unit cube with fixed
  support, axial node-set force F=1, density rho=2.5 and axial gravity g=0.8.
  The expected loaded-face displacement is
  `F*L/(E*A) + rho*g*L^2/(2*E) = 0.002` for E=1000 and Poisson ratio zero.
  The model is saved/reopened before export; English/Italian decks are compared
  excluding the random model hash. Actual FRD/DAT bytes are subsequently saved
  into the editable archive and verified on reopen.
- Centrifugal persistence has portable export-equivalence coverage, not a native
  centrifugal solve in this slice. Windows execution was not tested locally.

Native artifacts: `/tmp/prepomax calculix job drez12fa`. This extension does not
add arbitrary elements, multiple parts, nonlinear/contact metadata, derived
result definitions, legacy PMX import or Linux UI hosting.

## Metadata version 3 extension

[Multipart persistence](MULTIPART_PROJECT_PERSISTENCE.md) supersedes the single-part
restriction and writes version 3 metadata. Versions 1 and 2 remain readable.
