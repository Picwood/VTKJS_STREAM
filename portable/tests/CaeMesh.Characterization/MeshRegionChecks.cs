using CaeGlobals;
using CaeMesh;

internal static class MeshRegionChecks
{
    public static void Run(Action<bool, string> check)
    {
        void Vector(double[] actual, double[] expected, string label) =>
            check(actual.Length == expected.Length && actual.Zip(expected).All(p => Math.Abs(p.First - p.Second) < 1e-10), label);
        var history = new Selection();
        history.Add(new SelectionNodeIds(vtkSelectOperation.Add, false, new[] { 11, 12 }));
        var surface = new FeSurface("Surface", new[] { 101, 102 }, history)
        {
            Area = 4.5, NodeSetName = "Nodes", SurfaceFaceTypes = FeSurfaceFaceTypes.SolidFaces,
            TemporarySurface = true, Active = false, Visible = false,
            ParentMultiRegion = new RemeshingParameters("Region", RegionTypeEnum.Selection, new MeshingParameters("Parameters"))
        };
        surface.AddElementFace(FeFaceName.S1, "Elements");
        var copy = new FeSurface(surface);
        check(copy.Area == 4.5 && copy.NodeSetName == "Nodes" && copy.SurfaceFaceTypes == FeSurfaceFaceTypes.SolidFaces &&
            copy.TemporarySurface && !copy.Active && !copy.Visible, "surface copied metadata");
        check(copy.ParentMultiRegion == null && copy.ParentMasterMultiRegion == null && copy.ParentSlaveMultiRegion == null,
            "surface copy excludes temporary parent references");
        copy.FaceIds[0] = 99; copy.ElementFaces[FeFaceName.S1] = "Other";
        ((SelectionNodeIds)copy.CreationData.Nodes[0]).ItemIds[0] = 99;
        check(surface.FaceIds[0] == 101 && surface.ElementFaces[FeFaceName.S1] == "Elements" &&
            ((SelectionNodeIds)history.Nodes[0]).ItemIds[0] == 11, "surface copy owns arrays dictionary and history");
        copy.CreatedFrom = FeSurfaceCreatedFrom.NodeSet;
        check(copy.FaceIds == null && copy.CreationData == null && copy.ElementFaces == null && copy.Area == -1 &&
            !copy.TemporarySurface, "surface creation mode clears derived state");
        var nodeSurface = new FeSurface("NodeSurface", "SourceNodes");
        check(nodeSurface.CreatedFrom == FeSurfaceCreatedFrom.NodeSet && nodeSurface.CreatedFromNodeSetName == "SourceNodes", "node set surface construction");
        bool rejected = false;
        try { nodeSurface.AddElementFace(FeFaceName.Empty, "Elements"); } catch (CaeException) { rejected = true; }
        check(rejected, "surface rejects empty face identifier");
        surface.ClearElementFaces();
        check(surface.ElementFaces == null && surface.Area == 0, "surface face clearing");

        var reference = new FeReferencePoint("Reference", 1, 2, 3);
        Vector(reference.Coor(), new[] { 1d, 2, 3 }, "reference coordinates");
        reference.UpdateCoordinates(new[] { new[] { -2d, 4 }, new[] { 1d, 7 }, new[] { -3d, 1 } });
        Vector(reference.Coor(), new[] { 1d, 4, -1 }, "reference bounding box center");
        reference.UpdateCoordinates(new[] { 2d, 3, 4 });
        Vector(reference.Coor(), new[] { 2d, 3, 4 }, "reference centroid update");
        check(reference.TryCheckEquations(), "reference equations valid");
        reference.Reset();
        Vector(reference.Coor(), new double[3], "reference reset");

        var cs = new CoordinateSystem("Frame", false);
        check(cs.IsProperlyDefined(out _), "default coordinate frame validity");
        Vector(cs.GetLocalCoordinates(new[] { 2d, 3, 4 }), new[] { 2d, 3, 4 }, "identity coordinate transform");
        // Origin (1,2,3), X along global Y, Y along negative global X.
        cs.X1.SetEquationFromValue(1); cs.Y1.SetEquationFromValue(2); cs.Z1.SetEquationFromValue(3);
        cs.X2.SetEquationFromValue(1); cs.Y2.SetEquationFromValue(3); cs.Z2.SetEquationFromValue(3);
        cs.X3.SetEquationFromValue(0); cs.Y3.SetEquationFromValue(2); cs.Z3.SetEquationFromValue(3);
        check(cs.IsProperlyDefined(out _), "rotated translated frame validity");
        Vector(cs.GetLocalCoordinates(new[] { -2d, 4, 7 }), new[] { 2d, 3, 4 }, "rotated translated local coordinates");
        Vector(cs.GetOrientedVectorAtPoint(new[] { 2d, 3, 4 }, null), new[] { -3d, 2, 4 }, "rotated vector excludes translation");
        cs.Type = CoordinateSystemTypeEnum.Cylindrical;
        Vector(cs.GetLocalCoordinates(new[] { -2d, 2, 7 }), new[] { 3d, Math.PI / 2, 4 }, "cylindrical radius angle and height");
        Vector(cs.GetOrientedVectorAtPoint(new[] { 1d, 0, 0 }, new[] { -2d, 2, 7 }), new[] { -1d, 0, 0 }, "cylindrical radial vector");
        cs.PointXCreatedFrom = CsPointCreatedFromEnum.OnPoint;
        check(!cs.IsProperlyDefined(out string error) && !string.IsNullOrEmpty(error), "incomplete frame selection rejected");
        cs.Reset();
        cs.X2.SetEquationFromValue(0);
        check(!cs.IsProperlyDefined(out _), "coincident frame points rejected");
    }
}
