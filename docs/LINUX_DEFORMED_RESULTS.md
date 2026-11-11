# Static displacement scaling

Implemented and validated 2026-09-23. The GTK result view now provides a
**Displacement scale (0 = undeformed)** numeric control and **Apply scale**.
Selecting a field/frame applies the current scale; changing the scale and clicking
Apply republishes the selected frame. Mesh geometry mode always uses reference
coordinates. Scale controls are disabled when no result field is selected or
while opening/solving.

The portable exporter computes `x_display = x_reference + scale * U` using DISP
U1/U2/U3 from the selected static step, increment and time. Contour scalars remain
the selected field/component and are never multiplied by the displacement scale.
Reference coordinates are read from the archive for each export, so scaling is
not cumulative. Node IDs, element IDs and triangle connectivity remain unchanged.
The source archive is not modified.

Zero is the default and requires no displacement vector. Nonzero scaling requires
a complete finite displacement vector matching the selected frame. Unknown frames,
missing/non-finite components, non-finite output coordinates and scales outside
[-1000000, 1000000] are rejected. Negative scales are supported deliberately.
The explicit limit bounds the viewer control; it does not establish physical
validity. The display is on the supported linear hexahedral mesh, with no automatic
scale selection, animation or harmonic/complex deformation.

## Validation

- Portable suite: **3,805 checks**, including 794 mesh I/O assertions. The new
  assertions cover both static frames at scales 100, -100 and 0; expected nodal
  coordinates and unchanged scalar values; exact reference-scene bytes at zero;
  rejection of NaN and out-of-range scales before output publication. Checks run
  in English and Italian locales.
- `scripts/test-viewer-fields.py` verifies actual renderer bounds for positive,
  negative and zero scaling, unchanged analytical contour range, invalid-scale
  bootstrap preservation, and restoration of geometry metadata.
- GTK `--smoke --smoke-field DISP/U1 --deformation-scale 100` loads a selected
  scaled frame and receives the browser's stable-scene acknowledgement, followed
  by renderer shutdown. This is functional acceptance, not manual visual review.
- Existing unscaled field transport and portable checks continue to pass. Renderer
  integration used explicit .NET 10 roll-forward for the unchanged net8.0 server.

Use the generated two-step archive and commands in
[result contour evidence](LINUX_RESULT_CONTOURS.md). The GTK launch accepts
`--deformation-scale` as an initial value; interactive users can edit the control.
The companion `field` command accepts optional `deformationScale`, defaulting to
zero so existing clients keep their original behavior.

Next: broaden model-to-results user workflows. The later GTK host decision,
clipboard/accessibility groundwork, packaging and recorded DRM-backed WebGL
acceptance close PEND-006. Manual contour/deformation review, screen-reader,
mixed-monitor and large-model GPU qualification remain open.
