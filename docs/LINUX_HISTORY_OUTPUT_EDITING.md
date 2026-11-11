# Linux history output request authoring

Implemented and accepted on 2026-10-28. This closes the named-region history
request manager slice from the original PrePoMax static workflow. CAD repair is
outside this project scope.

## Supported workflow

GTK **History outputs…**, the viewer companion and `prepomax-linux-cli` share
`ProjectHistoryOutputs`. They list, add, edit/rename, duplicate, propagate,
activate/deactivate and remove requests while retaining step/request order.
Propagation replaces the same name in place in each later static step or appends
an independent typed copy when the name is absent.

| Request | Variables | Named targets |
|---|---|---|
| Nodal | `RF`, `U`, `V`, `NT`, `RFL` | node set, surface or reference point |
| Element | `S`, `E`, `ME`, `PEEQ`, `HFL`, `ENER`, `ELSE`, `ELKE`, `EVOL`, `EBHE`, `SDV` | element set |
| Contact | `CDIS`, `CSTR`, `CELS`, `CNUM`, `CF` | contact pair |
| Section | `SOF`, `SOM`, `SOAREA` | element surface |

Nodal, element and contact requests expose CalculiX totals (`No`, `Yes`,
`Only`) and the global flag. Section print does not use those options. Editing
retains the original request type. Names are unique within a step ignoring case,
and each step is bounded to 1,000 requests. Positional user keywords make
editing read-only until their insertion paths can be remapped.

This slice uses existing named regions. Original selection-owned temporary set
creation and cleanup remains part of broader graphical selection/model-tree work.

Every operation writes a new PMA, refuses source overwrite, preserves shared
regions, retained CAD/topology and mesh controls, and removes stale FRD, DAT and
binary results. Persistence now restores all four history subtypes instead of
treating every request as nodal. The generic entity shape already carried its
concrete kind, so no PMA metadata version change was required.

## CLI

```text
prepomax-linux-cli history-outputs PROJECT.pma
prepomax-linux-cli add-history-output PROJECT.pma NEW.pma STEP NAME TYPE VARIABLES REGION_TYPE REGION [OPTIONS]
prepomax-linux-cli edit-history-output PROJECT.pma NEW.pma STEP OLD_NAME NEW_NAME TYPE VARIABLES REGION_TYPE REGION [OPTIONS]
prepomax-linux-cli duplicate-history-output PROJECT.pma NEW.pma STEP NAME NEW_NAME
prepomax-linux-cli history-output-active PROJECT.pma NEW.pma STEP NAME true|false
prepomax-linux-cli propagate-history-output PROJECT.pma NEW.pma STEP NAME
prepomax-linux-cli remove-history-output PROJECT.pma NEW.pma STEP NAME
```

Types are `nodal`, `element`, `contact` and `section`. Variables are
comma-separated. Region types are `node-set`, `element-set`, `surface`,
`reference-point` and `contact-pair`. Options are `--totals yes|only|no` and
`--global true|false`.

## Acceptance

- The portable Release build has zero warnings/errors and the full suite passes
  **5,521 checks**, including 40 history request checks.
- Archive tests round-trip all four types and verify the production `*Node print`,
  `*El print`, `*Contact print` and `*Section print` writers.
- Native CalculiX 2.23 passes the complete operation workflow. The authored
  two-step request produces DAT and retains maximum U1 of 0.001 and 0.002.
- GTK X11 software-WebGL passes all seven operations with save/reopen, exact
  definition comparison, result invalidation and renderer shutdown.
- Fresh Ubuntu 24.04 passes installed operations/export with distribution
  CalculiX 2.21 and Gmsh 4.12.1. Upgrade from `0.1.4~preview+history.1` to `.2`
  preserves the authored project byte-for-byte; purge removes integration.

Recorded evidence: [history-output acceptance](evidence/linux-history-outputs-2026-10-28.txt).

[History-result browsing, plotting and export](LINUX_HISTORY_RESULTS.md) is now
complete. Selection-owned regions, non-static compatibility and model-tree/undo
integration remain open.
