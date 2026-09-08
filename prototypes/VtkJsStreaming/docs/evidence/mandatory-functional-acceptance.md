# Mandatory functional acceptance ledger

Date: 2026-09-01

This ledger maps every mandatory gate in specification section 15.1 to executable checks or measured evidence. It does not extend the Linux functional result into a physical-GPU performance claim.

| Mandatory gate | Result | Evidence |
|---|---|---|
| Windows and Linux x64 server/client | Pass | Windows correctness and benchmark records plus [Ubuntu 22.04 Linux qualification](linux-x64-functional.md) |
| Renderer/UI dependency isolation | Pass | `scripts/audit-vtkjs-prototype-isolation.ps1` inspected 6 project files and 32 C# source files; zero forbidden references |
| Loopback-only browser requests | Pass | `RenderApi` unit coverage rejects HTTPS, localhost aliases, IPv6, LAN, and public hosts; the complete Playwright workflow records every HTTP/WebSocket request and requires hostname `127.0.0.1` |
| Deterministic small fixture and ID mapping | Pass | Contract/fixture tests assert stable IDs, hashes, counts, packed connectivity, and pick mappings; browser asserts 2 actors, 2 chunks, 6 points, 2 polygons, and 152 bytes |
| 3,000,000 nodes / 5,993,002 triangles | Pass | [WP5 benchmark report](../benchmark-report.md) and committed three-run raw summaries |
| Progressive first-pixel rendering | Pass | Playwright observes one resident chunk while the deliberately delayed second chunk is incomplete; WP5 separately distinguishes first-visible and full-load timing |
| Scalar-only field update | Pass | Browser and WP5 network assertions require only scalar resource requests and unchanged topology tokens/URLs |
| Visibility retains topology | Pass | Playwright requires zero resource refetch and unchanged resident pipelines during visibility changes |
| Complete point-pick result | Pass | Browser/server tests assert actor, triangle, source element, candidate source node, and reconstructed world point; WP5 includes 30 physical samples per run |
| Visible recoverable resource failure | Pass | Playwright corrupts the same resource twice, requires a visible size/hash error, then proves a clean reload succeeds |
| Safe disconnect/reconnect | Pass | Playwright disconnects the event stream and requires a clean authenticated ten-resource snapshot reload with restored selection state |
| Five-cycle lifecycle retention | Pass | WP5 reports zero actors/chunks after every unload and 1.193% retained memory after five cycles, below the 10% target |
| Reproducible commands | Pass | Prototype README, benchmark report, Windows scripts, Linux lifecycle/browser scripts, and the isolation audit provide exact commands and environment boundaries |

## Fresh regression results

- Prototype isolation audit: 6 projects / 32 C# files / 0 forbidden references.
- Vitest: 12 files / 47 tests passed.
- Playwright: 2 tests passed, including the all-request loopback assertion.
- .NET: 78 tests passed across the contract and server assemblies.
- Production build: every emitted JavaScript chunk below the enforced 500,000-byte limit.

The separate Linux physical-GPU 3M benchmark remains pending. That is a cross-platform performance reference activity, not an unproven mandatory functional behavior.
