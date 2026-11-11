# Portable result-history export

Updated: 2026-09-15.

The portable CaeResults project now includes the production ResultHistoryOutputWriter
(48 linked source files). DAT histories can be attached to FeResults and exported
through the existing history table extraction path.

Numeric cells now use invariant formatting. Titles, headers and data cells quote
delimiters, embedded quotes and line breaks, doubling embedded quotes. This fixes
invalid CSV for quoted names and ambiguous numeric columns under Italian settings.
The section markers and table organization are retained. These formatting fixes
also affect the legacy Windows build.

Eighteen checks across en-US and it-IT parse exports with the framework's independent
TextFieldParser. They cover comma, semicolon and tab separators, Unicode/quoted/
multiline names and signed displacement values. Four more checks validate nonempty
DAT history samples after an aggregate binary payload round trip.

Run `bash scripts/test-portable.sh`: 3,201 checks, including 354 result checks.
The optional `python3 scripts/test-native-calculix.py` also exports fresh native
DAT data through FeResults, reads the resulting cube.history.csv and checks all
four right-face axial displacements equal 0.001 in both locales. That file stays
in the printed job directory beside the solver logs and results.

This validates the covered displacement histories. Other history types, missing
samples, all delimiter choices and downstream spreadsheet applications remain
unvalidated. The file remains a sequence of titled component tables, not a single
rectangular CSV table. Full project-file persistence and desktop wiring are still
separate work.
