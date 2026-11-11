using CaeGlobals;
using CaeMesh;

internal static class GeometryConfigurationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var history = new Selection();
        history.Add(new SelectionNodeIds(vtkSelectOperation.Add, false, new[] { 7 }));
        var settings = new RemeshingParameters("Region", RegionTypeEnum.Selection,
            new MeshingParameters("Parameters") { MaxH = 2.5, MinH = .25, CreationIds = new[] { 7 }, CreationData = history });
        settings.UseMmg = false;
        var part = new GeometryPart("Geometry", 3, new[] { 1, 2, 3 }, new[] { 4 }, new[] { typeof(LinearTriangleElement) })
        {
            MeshingParameters = settings, CADFileData = "BREP payload α\nline two",
            FreeNodeIds = new[] { 1 }, ErrorNodeIds = new[] { 2 },
            FreeEdgeCellIds = new[] { 3 }, ErrorEdgeCellIds = new[] { 4 },
            Active = false, Visible = false, Valid = false, Internal = true, Modified = true
        };
        var copy = (GeometryPart)part.DeepCopy();
        check(!copy.Active && !copy.Visible && !copy.Valid && copy.Internal && copy.Modified, "geometry inherited flags");
        check(copy.IsCADPart && copy.CADFileData == part.CADFileData && copy.HasErrors && copy.HasFreeEdges, "geometry CAD and diagnostic state");
        check(copy.MeshingParameters is RemeshingParameters r && r.RegionName == "Region" && r.RegionType == RegionTypeEnum.Selection && !r.UseMmg, "remeshing subtype and exact settings");
        copy.MeshingParameters.MaxH = 9;
        copy.MeshingParameters.CreationIds[0] = 99;
        ((SelectionNodeIds)copy.MeshingParameters.CreationData.Nodes[0]).ItemIds[0] = 99;
        check(settings.MaxH == 2.5 && settings.CreationIds[0] == 7 && ((SelectionNodeIds)history.Nodes[0]).ItemIds[0] == 7, "geometry configuration deep isolation");
        copy.FreeNodeIds[0] = 99; copy.ErrorNodeIds[0] = 99; copy.FreeEdgeCellIds[0] = 99; copy.ErrorEdgeCellIds[0] = 99;
        copy.CADFileData = null;
        check(part.FreeNodeIds[0] == 1 && part.ErrorNodeIds[0] == 2 && part.FreeEdgeCellIds[0] == 3 && part.ErrorEdgeCellIds[0] == 4 && part.IsCADPart, "geometry diagnostics and CAD isolation");
        var compound = new CompoundGeometryPart("Compound", new[] { "Source" }, new[] { "Child" })
            { MeshingParameters = settings, CADFileData = part.CADFileData, Visible = false, Active = false };
        var compoundCopy = (CompoundGeometryPart)compound.DeepCopy();
        check(compoundCopy.PartType == PartType.Compound && !compoundCopy.Visible && !compoundCopy.Active && compoundCopy.CADFileData == part.CADFileData, "compound subtype and inherited state");
        compoundCopy.CreatedFromPartNames[0] = "Changed"; compoundCopy.SubPartNames[0] = "Changed";
        compoundCopy.MeshingParameters.MinH = 1;
        check(compound.CreatedFromPartNames[0] == "Source" && compound.SubPartNames[0] == "Child" && settings.MinH == .25, "compound array and settings isolation");
        var local = new LocalMeshSize("Local") { MeshSize = 2, CreationData = history, CreationIds = new[] { 7 } };
        var localCopy = local.DeepCopy();
        localCopy.CreationData.Nodes.Clear(); localCopy.CreationIds[0] = 99;
        check(history.Nodes.Count == 1 && local.CreationIds[0] == 7, "local size selection isolation");
        check(local.GetNumberOfElements(5, 1, 3) == 3 && local.GetMeshSize(5, 1, 3) == 2, "local size rounding and limits");
        var parameters = new MeshingParameters("Sizing") { MaxH = 2.5, MinH = .25, Grading = .3 };
        var directory = Path.Combine(Path.GetTempPath(), "prepomax géométrie " + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string brep = Path.Combine(directory, "part α.brep");
            File.WriteAllText(brep, part.CADFileData);
            copy.CADFileDataFromFile(brep);
            check(copy.CADFileData == part.CADFileData, "CAD payload file round trip");
            string path = Path.Combine(directory, "mesh parameters.txt");
            parameters.WriteToFile(path, 10);
            var lines = File.ReadAllLines(path);
            check(lines[3] == "2.5" && lines[5] == "0.25" && lines[9] == "0.3", "invariant absolute mesher configuration");
            parameters.AdvancedView = true; parameters.RelativeSize = true;
            parameters.FactorMax = .15; parameters.FactorMin = .025;
            parameters.WriteToFile(path, 10);
            lines = File.ReadAllLines(path);
            check(lines[3] == "1.5" && lines[5] == "0.25", "relative mesher configuration");
            parameters.AdvancedView = false;
            parameters.WriteToFile(path, 10);
            check(File.ReadAllLines(path)[3] == "2.5", "basic mode uses absolute sizing");
        }
        finally { Directory.Delete(directory, true); }
    }
}
