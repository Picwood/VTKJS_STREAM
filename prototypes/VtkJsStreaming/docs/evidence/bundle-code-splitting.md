# Production bundle code splitting

Date: 2026-09-01

Command:

```powershell
cd prototypes\VtkJsStreaming\src\client
npm run build
npm run test:e2e
```

## Result

Before this slice, Vite emitted one 825,017-byte minified application chunk (225,800 bytes gzip) and reported its greater-than-500-KB warning.

The production graph now contains 14 JavaScript chunks totaling 844,367 minified bytes. Code splitting does not claim to reduce total code; it bounds individual transfer/cache units:

- application entry: 92,598 bytes minified / 26,760 bytes gzip;
- largest VTK chunk: 215,278 bytes minified / 52,260 bytes gzip;
- largest allowed JavaScript chunk: 500,000 bytes;
- oversized-chunk warning: absent.

VTK.js has module-registration side effects and internal circular dependencies. The first size-split build exposed an execution-order failure in Chromium (`CoordinateSystem` was undefined). The final Rolldown configuration enables strict execution ordering, after which both Playwright tests passed, including progressive rendering and integrity-failure recovery.

The Vite build contains a production-only budget check that fails the build when any emitted JavaScript chunk exceeds 500,000 bytes. This turns the prior informational warning into an enforced regression gate.
