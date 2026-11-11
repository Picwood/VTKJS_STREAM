# Broader PMX fixture matrix

Validated: 2026-10-21.

Seven distinct real PMX saves from two PrePoMax versions were inspected
read-only with the standalone bounded NRBF and binary-mesh readers. An eighth
NEXTREME location has the same SHA-256 as `nextreme-doc` and was not inspected
twice. The two MAG locations and two MOBIE locations have different hashes and
serialized graphs, so each remains an independent saved-project fixture.

| Fixture alias | Format | File bytes | NRBF records | Mesh nodes | Elements | Parts | Conversion status |
|---|---:|---:|---:|---:|---:|---:|---|
| `mag-doc` | v2.5.1 dev | 72,198,584 | 9,415 | 3,069,296 | 693,174 | 14 | Converted/reopened/rendered (PMA metadata v20) |
| `mag-wine` | v2.5.1 dev | 72,210,964 | 9,728 | 3,069,296 | 693,174 | 14 | Eligible |
| `mobie-doc` | v2.5.1 dev | 74,337,820 | 11,624 | 3,065,425 | 682,582 | 14 | Eligible |
| `mobie-wine` | v2.5.1 dev | 74,341,778 | 11,825 | 3,065,425 | 682,582 | 14 | Eligible |
| `nextreme-doc` | v2.5.1 dev | 104,068,887 | 27,609 | 4,595,560 | 914,905 | 14 | Eligible |
| `nextremoin-wine` | v2.5.1 dev | 106,354,652 | 25,903 | 4,623,915 | 928,499 | 14 | Converted/reopened (PMA metadata v22) |
| `neon36-v240` | v2.4.0 | 469,842,043 | 9,205 | 2,281,565 | 931,033 | 15 | Converted/reopened/rendered (PMA metadata v24) |

All seven default inventories and complete mesh passes succeeded. Mesh
validation covered finite coordinates, unique IDs, connectivity, exact part
membership, named sets, surface faces and quadratic-solid formulations. Peak
resident memory was 333–451 MiB per mesh pass, and each completed in 2.8–5.3
seconds on this workstation. SHA-256 before and after every pass matched.

The five eligible fixtures previously failed only because each static analysis
contains three `ContactFieldOutput` definitions. PMA metadata v19 now preserves
contact output variables plus active, last-iteration, contact-element and global
settings; metadata v20 is the corresponding external-mesh form. The PMX
capability gate and converter accept the definition explicitly. Synthetic PMX
conversion exercises it, and metadata tests reject unknown flag bits and use in
pre-v19 documents.

The complete reports, source hashes and timing logs are local external-fixture
artifacts under
`~/.cache/prepomax/wayland-acceptance/pmx-broader-fixtures/`. These projects are
not repository fixtures. On 2026-10-20, `mag-doc` completed the first real
conversion from this broader set. It produced a 120,238,287-byte archive-v3,
metadata-v20 PMA containing 3,069,296 nodes, 693,174 elements, 14 parts, 4
materials, 6 sections, 11 contact pairs, 14 constraints and 3 static steps. The
converter saved and reopened it in 32.07 seconds at an 8,191,308 KiB peak. An
independent `PrePoMax.Cli inspect` reopen completed in 9.80 seconds at 5,273,520
KiB. CLI export reproduced all 259,273,988 solver-input bytes except the expected
eight-character generated heading hash, and the archive's canonical
save/reopen/export comparison passed. Scene export completed in 7.35 seconds at
1,822,628 KiB; authenticated renderer publication reported 14 actors, 513,064
points and 1,026,148 triangles and shut the server down cleanly. The source hash
remained
`5e0038bb13623546cd70eebe7af581fb0e79e611c9192568234a95c262dd60ec`.

PMA metadata v21 now preserves up to 1,000 bounded active, unsuppressed leaf
CalculiX user keywords in insertion order; metadata v22 is its external-mesh
form. NEXTREMOIN's root-index-16 `*Preprint` definition now survives conversion,
save, reopen and the 389,253,226-byte production export. Its 188,147,312-byte
PMA contains 4,623,915 nodes, 928,499 elements, 14 parts, 5 materials, 6
sections, 11 contact pairs, 15 constraints and 3 static steps. Conversion took
42.43 seconds at a 12,350,560 KiB peak; independent CLI reopen took 13.06
seconds at 7,672,356 KiB. The source SHA-256 remained
`d7b5314ff64a41317bede88904c0d3eabe6f3ffd41798ea7c42c83f716b8dadc`.

Metadata v23 adds bounded, finite, coordinate-defined 3D rectangular and
cylindrical coordinate systems; metadata v24 is its external-mesh form. NEON's
single rectangular system and two active root-level `*END PART` keywords now
survive conversion. Its 529,076,433-byte archive contains 2,281,565 nodes,
931,033 elements, 15 parts, 5 materials, 6 sections, 11 contact pairs, 13
constraints, 2 static steps and one binary result set. Conversion took 43.46
seconds at a 9,063,804 KiB peak. Independent CLI reopen took 12.06 seconds;
the results reader exposes 62 selections after reconstructing the unnamed
pre-v2.4.4 result parts. The 245,843,628-byte independent export is canonically
identical to the archived deck, scene export produced 51,949,466 bytes, and
authenticated renderer publication reported 15 actors, 811,686 points and
1,623,416 triangles. The source SHA-256 remained
`803db0bae4ee2f8a3acf177e1edcd7b1a290264783fbb8f281d0aa9f09018ebe`.

Four further v2.5.1 fixtures remain eligible for the same conversion. No fixture
in this matrix is blocked by the current analysis-model contract.
