# Portable mesh-based result interpolation

The portable CaeResults project now links 38 production source files, adding
ResultsInterpolator, Triangle, Plane, Edge3 and the interpolation/closest-point
enums. An unused Priority_Queue import was removed. No new package, replacement
algorithm or renderer dependency was required.

Eight checks across en-US and it-IT verify affine scalar interpolation on a
triangle, projection from a point two units above the surface, closest-node
values and ownership of converted source values. The complete suite passes
3,073 checks on Linux, including 260 result checks.

This establishes the tested triangle cases only. Quad and higher-order face
conversion, signed distances, degenerate and empty meshes, broader numerical
accuracy and full FeResults integration still need validation. The legacy
Windows build was not run on this host.
