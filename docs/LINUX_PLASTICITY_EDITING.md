# Plastic material property authoring on Linux

Implemented 2026-10-29 as the next material-property parity slice. GTK
**Plasticity…**, the viewer companion and the CLI all use
`PrePoMax.Application.ProjectMaterials`; the production `Plastic` model class,
PMA persistence and CalculiX keyword writer remain the source of truth.

The editor lists supported constant elastic materials and can add or replace a
multiline yield-stress/plastic-strain table. It exposes the original Isotropic,
Kinematic and Combined hardening choices. A property can also be removed. Every
operation writes a new PMA, refuses an existing destination, reopens the result
and removes stale FRD/DAT data.

The bounded Linux editor accepts 1–1,000 rows. Yield stress must be finite and
positive. Plastic strain must be finite and nonnegative, the first strain must
be zero, and later strains must increase strictly. Temperature-dependent
materials and temperature columns remain outside this slice. The GTK table uses
one `yield stress, plastic strain` pair per line and current model units.

The equivalent CLI operations are:

```bash
prepomax-linux-cli plasticity model.pma
prepomax-linux-cli set-plasticity model.pma plastic.pma MATERIAL kinematic \
  '100:0,120:0.01,150:0.05'
prepomax-linux-cli set-plasticity plastic.pma combined.pma MATERIAL combined \
  '90:0,130:0.02'
prepomax-linux-cli remove-plasticity combined.pma elastic-only.pma MATERIAL
```

Native CalculiX 2.23 acceptance exports the Kinematic table through the
production writer, solves the two-step fixture, and retains maximum DISP/U1 of
0.001 and 0.002. Combined replacement and removal reopen without duplicate
properties; removal eliminates `*Plastic` from the production deck. The full
portable suite passes **5,583 checks**, including 40 application-service cases
for ordering, replacement, removal, bounds, invalid rows, result invalidation,
source preservation and staging cleanup.

GTK X11 acceptance passes set, edit and remove independently, including native
save-as, reopen and stale-result checks. Clean Ubuntu 24.04 package acceptance
uses the installed CLI to set/edit/remove the property, carries the Kinematic
property through field/history request edits, and solves that final project with
the distribution CalculiX package.

Run the GTK acceptance with destinations that do not exist:

```bash
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh source.pma \
  --smoke-plasticity plastic.pma --smoke-plasticity-action set
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh plastic.pma \
  --smoke-plasticity combined.pma --smoke-plasticity-action edit
GDK_BACKEND=x11 scripts/start-gtk-viewer.sh combined.pma \
  --smoke-plasticity elastic-only.pma --smoke-plasticity-action remove
```

Temperature tables, thermal properties, hyperelastic and other original
material models, and material-library workflows remain material parity gaps.
CAD repair is outside the project scope.
