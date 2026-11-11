# Legacy PMX analysis-model conversion

Validated: 2026-10-21.

The standalone PMX tool now converts the supported analysis model and validated
result stream from an
unprotected compressed PrePoMax 2.0.6-or-newer project into a new portable PMA:

```sh
dotnet build portable/PrePoMax.PmxInspect -c Release
dotnet portable/PrePoMax.PmxInspect/bin/Release/net10.0/PrePoMax.PmxInspect.dll \
  /path/to/source.pmx --convert /path/to/new-project.pma
```

The source is opened read-only. Source and destination must differ, and the
destination must not exist. Parsing uses `System.Formats.Nrbf`; it never enables
`BinaryFormatter` or instantiates arbitrary serialized legacy types. Each
supported record is mapped explicitly into current production CaeMesh/CaeModel
objects. The destination is published only after PMA serialization, reopen and
production CalculiX-export equivalence succeed.

The current conversion slice supports general 3D FEA models with PMA-supported
solid topology and formulations; parts, node/element sets and surfaces; numeric
reference points; the six supported material property types; solid sections;
tabular amplitudes; auxiliary cylinders; supported surface interactions/contact
pairs; ties, rigid bodies and surface springs; and supported static-step loads,
boundaries and output requests. Unit-bearing literal values are evaluated in the
legacy model's unit system. Bounded active, unsuppressed leaf CalculiX user
keywords retain their ordered insertion paths and exact text. Bounded, finite,
coordinate-defined 3D rectangular and cylindrical coordinate systems preserve
their name, active state, type and three defining points. Formula expressions
beginning with `=` are rejected.

Empty legacy element sets are omitted because current PMA metadata requires
nonempty sets. Duplicate element-set memberships are normalized in original
order. The converter remains fail-closed on unsupported types, unknown enums,
malformed arrays, missing links, nonempty geometry, mesh
setup, distributions, initial conditions, custom step controls, selection-derived
or 2D coordinate systems, or other state outside the PMA contract.

## Supplied-project evidence

The recorded `BRAKE_537037-V2-complete-FRANKNRX.pmx` conversion produced a
metadata-v18 PMA and reopens through `StaticProjectArchive` and
`PrePoMax.Cli inspect`; current conversions write metadata v28. The source
SHA-256 is unchanged. The archive-v4 output is about 702 MiB and contains:

- 4,592,568 nodes, 960,847 quadratic solid elements and 14 parts.
- 83 node sets, 131 nonempty element sets and 46 surfaces.
- 2 reference points, 6 materials, 4 solid sections, one amplitude and 4 cylinders.
- 2 surface interactions, 12 contact pairs and 15 constraints.
- 3 nonlinear static steps with the supported current loads, boundaries and outputs.

On this workstation the model-and-results conversion took about 98 seconds and
peaked near 11.4 GiB resident memory. The converter omitted 78 empty legacy
element sets. The PMA contains no FRD or DAT payload; its checksummed
`results.bin` entry retains the validated versioned CaeResults stream.

## Broader real-fixture evidence

The independent `MAG120-BENDING-BRAKE-V2.pmx` v2.5.1-dev save now completes the
same fail-closed path after contact field-output support removed its last
capability blocker. Conversion produced a 120,238,287-byte archive-v3,
metadata-v20 PMA in 32.07 seconds with an 8,191,308 KiB resident-memory peak. It
contains 3,069,296 nodes, 693,174 elements, 14 parts, 44 node sets, 108 element
sets, 44 surfaces, 4 materials, 6 solid sections, 2 reference points, one
amplitude, 7 cylinders, one surface interaction, 11 contact pairs, 14
constraints and 3 static steps.

`PrePoMax.Cli inspect` independently reopened the PMA and recovered the exact
mesh, part and material counts. A separate CLI export regenerated the complete
259,273,988-byte CalculiX deck. Its only byte differences from the archived deck
were the eight generated hash characters in the heading, so canonical production
export equivalence holds. Undeformed scene export completed in 7.35 seconds at a
1,822,628 KiB peak. The authenticated renderer-host probe accepted the scene as
14 actors, 513,064 points and 1,026,148 triangles, then exited without leaving a
server process. The source PMX SHA-256 remained
`5e0038bb13623546cd70eebe7af581fb0e79e611c9192568234a95c262dd60ec`.

The PMA, exported deck, scene and machine-readable conversion, inspection,
timing and render reports are external acceptance artifacts under
`~/.cache/prepomax/wayland-acceptance/`; they are not repository fixtures.

The v2.5.1-dev `NEXTREMOIN.pmx` save exercises the new user-keyword contract.
Its active root-index-16 `*Preprint, echo=NO, model=NO, history=NO, contact=NO`
line is inventoried without object instantiation and stored in metadata v22.
The 188,147,312-byte PMA saved and reopened in 42.43 seconds at a 12,350,560
KiB peak. It contains 4,623,915 nodes, 928,499 elements, 14 parts, 50 node
sets, 121 element sets, 50 surfaces, 5 materials, 6 sections, 11 contact pairs,
15 constraints and 3 static steps. Independent CLI reopen recovered the exact
model counts, and the 389,253,226-byte archived deck contains the keyword at
its requested production-writer insertion point. The source hash remained
`d7b5314ff64a41317bede88904c0d3eabe6f3ffd41798ea7c42c83f716b8dadc`.

The v2.4.0 `BENDING_NEON36.pmx` save exercises coordinate-system and older
binary-result compatibility. Its coordinate-defined rectangular frame and two
ordered `*END PART` keywords are stored in metadata v24. The 529,076,433-byte
PMA contains 2,281,565 nodes, 931,033 quadratic elements, 15 parts and one
binary result set. Conversion took 43.46 seconds at a 9,063,804 KiB peak;
independent CLI reopen took 12.06 seconds at 6,109,304 KiB. The older unnamed
result-part layout now reconstructs and consumes visualization data correctly,
and `PrePoMax.Cli fields` returns 62 selections. Independent export reproduced
all 245,843,628 deck bytes except eight generated hash characters. Scene export
and authenticated renderer publication passed with 15 actors, 811,686 points
and 1,623,416 triangles. The source hash remained
`803db0bae4ee2f8a3acf177e1edcd7b1a290264783fbb8f281d0aa9f09018ebe`.

## Remaining boundary

Command history, annotations and UI/controller state are not converted. PMA v4
now stores the validated result stream in a bounded, versioned envelope, and the
portable application restores its field catalog and exports result scenes from
its independent result mesh. Nonempty legacy geometry, protected projects,
layouts older than 2.0.6 and other PMX fixtures still need separate coverage
before this becomes a general legacy-import feature. The Linux GTK Open dialog
now exposes this bounded converter, requests a new PMA destination and opens the
completed archive. It preserves the current scene when conversion is refused.
See [GTK PMX import](LINUX_PMX_GTK_IMPORT.md).

On the supplied fixture, `--results` consumes all 544,409,218 result-tail bytes,
matches the single result name against serialized metadata, validates every
Deflate block, and decodes 16 fields across four static frames. The fields contain
72 component arrays with 127,825,128 finite values and no non-finite samples. The
history object is present but contains no sets. This pass takes about 11 seconds
and peaks near 707 MiB resident memory on this workstation.

`PrePoMax.Cli fields` exposes 124 raw and computed field/component selections
from the converted PMA. A step-1/increment-4 MISES contour exports as a 44 MiB
renderer scene, including quadratic-face tessellation. Result loading takes
7.93 seconds and peaks at 2,851,868 KiB on this fixture after bounded archive
streaming, down from about 47 seconds and 6.6 GiB. The same frame's deterministic fingerprint is
`A99283AB16B32876FBD17BA0293CCD9672DCEABDFEA999B4CB259B97F89AC4A2`
over 1,775,349 sorted node/value pairs; its range is 1.2014192E-05 to
644.1169 MPa and its mean is 31.6010954670436 MPa.
See [result streaming](LINUX_RESULT_STREAMING.md) for the validation boundary
and before/after measurements.

Automated synthetic validation passes 22 record cases, 20 mesh cases, 3 result
cases and 5 conversion cases, including coordinate-system, user-keyword and result preservation, truncated-result rejection, source
preservation, PMA reopen and overwrite refusal. The complete portable suite
passes 5,141 checks with a warning-free build.
