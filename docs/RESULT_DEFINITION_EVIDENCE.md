# Portable result definitions

The CaeResults portable project now links 28 production sources. Ten added
sources cover field filters; the base, equation, limit, envelope and coordinate
transform output definitions; and base, linear, circular and symmetry result
transformations. The limit definition explicitly uses CaeGlobals.OrderedDictionary
to avoid ambiguity with the modern framework type. Legacy project source lists
already include these classes.

Twelve checks across en-US and it-IT exercise displacement geometry, circular
axis normalization, incomplete-axis handling, derived-field dependency names,
output component contracts and independent bounded filter settings. Run
`bash scripts/test-portable.sh`; all 2,943 checks pass on Linux.

This ports definitions and their existing calculations, not the full FeResults
aggregate. Actual equation/envelope/limit evaluation, result filtering, renderer
application of transformations and Windows build verification remain pending.
No definition semantics or serialization layouts were deliberately changed.
