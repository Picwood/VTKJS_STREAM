using CaeMesh;
using CaeMesh.Meshing;
using CaeGlobals;

internal static class GmshSetupChecks
{
    public static void Run(Action<bool, string> check)
    {
        var history = new Selection();
        history.Add(new SelectionNodeIds(vtkSelectOperation.Add, false, new[] { 7 }));
        var extrusion = new ExtrudeMesh("Extrusion")
        {
            Direction = new[] { 1d, 2, 3 }, ExtrudeCenter = new[] { 4d, 5, 6 },
            LayerSizes = new[] { .25, .75 }, NumOfElementsPerLayer = new[] { 2, 3 },
            CreationData = history, CreationIds = new[] { 7 }, Active = false,
            NumberOfElements = 5, NumberOfThreads = 2, TransfiniteAngleDeg = 90
        };
        var copy = new ExtrudeMesh(extrusion);
        check(copy.Direction != null && copy.Direction.SequenceEqual(extrusion.Direction) &&
            copy.ExtrudeCenter != null && copy.ExtrudeCenter.SequenceEqual(extrusion.ExtrudeCenter), "extrusion copied geometry");
        copy.Direction[0] = 99; copy.ExtrudeCenter[0] = 99;
        copy.LayerSizes[0] = 99; copy.NumOfElementsPerLayer[0] = 99; copy.CreationIds[0] = 99;
        ((SelectionNodeIds)copy.CreationData.Nodes[0]).ItemIds[0] = 99;
        check(extrusion.Direction[0] == 1 && extrusion.ExtrudeCenter[0] == 4 && extrusion.LayerSizes[0] == .25 &&
            extrusion.NumOfElementsPerLayer[0] == 2 && extrusion.CreationIds[0] == 7 &&
            ((SelectionNodeIds)history.Nodes[0]).ItemIds[0] == 7, "extrusion owns geometry layers and selection");
        check(!copy.Active && copy.NumberOfElements == 5 && copy.NumberOfThreads == 2 &&
            Math.Abs(copy.TransfiniteAngleRad - Math.PI / 2) < 1e-12, "Gmsh configuration metadata");
        copy.CopyFrom(new ExtrudeMesh("Empty"));
        check(copy.Direction == null && copy.ExtrudeCenter == null, "extrusion null source clears destination");
        var revolution = new RevolveMesh("Revolution") { AxisCenter = new[] { 1d, 2, 3 }, AxisDirection = new[] { 0d, 0, 1 } };
        var revolved = new RevolveMesh(revolution);
        check(revolved.AxisCenter != null && revolved.AxisDirection != null && revolved.AxisCenter.SequenceEqual(revolution.AxisCenter), "revolution copied axis");
        revolved.AxisCenter[0] = 99; revolved.AxisDirection[0] = 99;
        check(revolution.AxisCenter[0] == 1 && revolution.AxisDirection[0] == 0, "revolution independent axis");
        revolved.CopyFrom(new RevolveMesh("Empty"));
        check(revolved.AxisCenter == null && revolved.AxisDirection == null, "revolution null source clears destination");
        var sweep = new SweepMesh("Sweep")
        {
            Direction = new[] { 1d, 0, 0 }, SweepCenter = new[] { 2d, 3, 4 }, SideSurfaceIds = new[] { 8, 9 },
            LayerGroupEdgeIds = new[] { new[] { new[] { 10, 11 }, null }, null },
            NumberOfLayerSmoothSteps = 3, NumberOfGlobalSmoothSteps = 4
        };
        var swept = new SweepMesh("Destination") { LayerGroupEdgeIds = new[] { new[] { new[] { -1 } } } };
        swept.CopyFrom(sweep);
        check(swept.Direction != null && swept.SweepCenter != null && swept.SideSurfaceIds != null &&
            swept.LayerGroupEdgeIds[0][0].SequenceEqual(new[] { 10, 11 }) && swept.LayerGroupEdgeIds[0][1] == null &&
            swept.LayerGroupEdgeIds[1] == null, "sweep copied geometry and nested topology");
        swept.Direction[0] = 99; swept.SweepCenter[0] = 99; swept.SideSurfaceIds[0] = 99; swept.LayerGroupEdgeIds[0][0][0] = 99;
        check(sweep.Direction[0] == 1 && sweep.SweepCenter[0] == 2 && sweep.SideSurfaceIds[0] == 8 &&
            sweep.LayerGroupEdgeIds[0][0][0] == 10, "sweep copy leaves source untouched");
        check(swept.NumberOfLayerSmoothSteps == 3 && swept.NumberOfGlobalSmoothSteps == 4, "sweep smooth settings");
        swept.CopyFrom(new SweepMesh("Empty"));
        check(swept.Direction == null && swept.SweepCenter == null && swept.SideSurfaceIds == null && swept.LayerGroupEdgeIds == null,
            "sweep null source clears destination");
        foreach (GmshSetupItem item in new GmshSetupItem[] { new ShellGmsh("Shell"), new TetrahedralGmsh("Solid"), new TransfiniteMesh("Transfinite") })
        {
            item.NumberOfElements = 0; item.NumberOfThreads = -1; item.ElementScaleFactor = -1;
            check(item.NumberOfElements == 1 && item.NumberOfThreads == 1 && item.ElementScaleFactor == 1, "Gmsh configuration clamps");
            item.CopyFrom(extrusion);
            check(item.LayerSizes.SequenceEqual(extrusion.LayerSizes) && !ReferenceEquals(item.LayerSizes, extrusion.LayerSizes), "Gmsh shared configuration copy");
        }
    }
}
