using System.Globalization;
using CaeMesh;
using FileInOut.Input;
using FileInOut.Output;
using QuantumConcepts.Formats.StereoLithography;

if (args.Length == 2 && args[0] == "--write-node-selection-fixture")
{
    PrePoMax.Persistence.StaticProjectArchive.Save(args[1], new StaticModelWorkflow(1).Model);
    return;
}
if (args.Length == 2 && args[0] == "--write-large-node-selection-fixture")
{
    var model = new StaticModelWorkflow(1, length: 1e6, width: 1e6, height: 1e6).Model;
    foreach (var entry in model.Mesh.Nodes.ToArray())
    {
        var node = entry.Value;
        node.X += 123456789.123; node.Y -= 234567890.456; node.Z += 345678901.789;
        model.Mesh.Nodes[entry.Key] = node;
    }
    PrePoMax.Persistence.StaticProjectArchive.Save(args[1], model);
    return;
}

void WriteReopenedModel(string path, CaeModel.FeModel model)
{
    PrePoMax.Persistence.StaticProjectArchive.Save(path + ".pma", model);
    var reopened = PrePoMax.Persistence.StaticProjectArchive.Open(path + ".pma");
    CalculixFileWriter.WriteActiveModel(path, reopened.Model, FileInOut.Output.Calculix.ConvertPyramidsToEnum.Wedges);
}

if (args.Length == 5 && args[0] == "--edit-step-force")
{
    PrePoMax.Application.ProjectLoads.SaveForceCopy(args[1], args[2], args[3], "force",
        double.Parse(args[4], CultureInfo.InvariantCulture), 0, 0);
    return;
}

if (args.Length == 3 && args[0] == "--prepare-cad-cube")
{
    string root = Path.Combine(Path.GetTempPath(), "cad-cube-acceptance-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    try
    {
        var mesh = PrePoMax.Persistence.StaticProjectArchive.Open(args[1]).Model.Mesh;
        string stage = args[1];
        for (int axis = 0; axis < 3; axis++)
        {
            string set = "ZERO_" + axis;
            string next = Path.Combine(root, "set-" + axis + ".pma");
            PrePoMax.Application.ProjectNodeSets.AddCopy(stage, next, set,
                mesh.Nodes.Values.Where(n => Math.Abs(n.Coor[axis]) < 1e-10).Select(n => n.Id).ToArray());
            stage = Path.Combine(root, "boundary-" + axis + ".pma");
            PrePoMax.Application.ProjectBoundaries.AddCopy(next, stage, "load-1", "support-" + axis, set, axis + 1, 0);
        }
        var geometry = PrePoMax.Persistence.StaticProjectArchive.Open(args[1]).GeometrySource;
        string pressureSurface = mesh.Surfaces.ContainsKey("XMAX") ? "XMAX" : "CAD_FACE_" +
            geometry!.Topology!.Entities.Single(entity => entity.Dimension == 2 &&
                Math.Abs(entity.Bounds[0] - 1) < 1e-6 && Math.Abs(entity.Bounds[3] - 1) < 1e-6).Tag;
        PrePoMax.Application.ProjectPressures.AddCopy(stage, args[2], "load-1", "traction", pressureSurface, -1);
    }
    finally { Directory.Delete(root, true); }
    return;
}

if (args.Length == 3 && args[0] == "--prepare-imported-inp")
{
    string root = Path.Combine(Path.GetTempPath(), "inp-acceptance-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    try
    {
        string first = Path.Combine(root, "1.pma"), second = Path.Combine(root, "2.pma"), third = Path.Combine(root, "3.pma");
        PrePoMax.Application.ProjectBoundaries.AddCopy(args[1], first, "load-1", "left", "LEFT", 1, 0);
        PrePoMax.Application.ProjectBoundaries.AddCopy(first, second, "load-1", "transverse", "ALLNODES", 2, 0);
        PrePoMax.Application.ProjectBoundaries.SaveDirectionCopy(second, third, "load-1", "transverse", 3, 0);
        PrePoMax.Application.ProjectLoads.AddCopy(third, args[2], "load-1", "force", "RIGHT", 1, 0, 0);
    }
    finally { Directory.Delete(root, true); }
    return;
}

if (args.Length == 3 && args[0] == "--prepare-imported-tetra")
{
    string root = Path.Combine(Path.GetTempPath(), "import-acceptance-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    try
    {
        string stage = Path.Combine(root, "1.pma");
        PrePoMax.Application.ProjectNodeSets.AddCopy(args[1], stage, "LEFT", new[] { 1, 3, 4 });
        string next = Path.Combine(root, "2.pma");
        PrePoMax.Application.ProjectNodeSets.AddCopy(stage, next, "RIGHT", new[] { 2 });
        stage = Path.Combine(root, "3.pma");
        PrePoMax.Application.ProjectBoundaries.AddCopy(next, stage, "load-1", "left", "LEFT", 1, 0);
        next = Path.Combine(root, "4.pma");
        PrePoMax.Application.ProjectBoundaries.AddCopy(stage, next, "load-1", "transverse", "ALLNODES", 2, 0);
        stage = Path.Combine(root, "5.pma");
        PrePoMax.Application.ProjectBoundaries.SaveDirectionCopy(next, stage, "load-1", "transverse", 3, 0);
        PrePoMax.Application.ProjectLoads.AddCopy(stage, args[2], "load-1", "force", "RIGHT", 1, 0, 0);
    }
    finally { Directory.Delete(root, true); }
    return;
}

if (args.Length is 2 or 3 && args[0] == "--profile-result-session")
{
    long budget = args.Length == 3 ? checked(long.Parse(args[2], CultureInfo.InvariantCulture) * 1024 * 1024)
        : PrePoMax.Application.ProjectResultSession.DefaultRetentionBudgetBytes;
    ResultSessionProfile.Run(args[1], budget);
    return;
}

if (args.Length == 2 && args[0] is "--write-result-project" or "--write-binary-result-project")
{
    var model = new StaticModelWorkflow(2).Model;
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures/archive-cube.frd");
    if (args[0] == "--write-result-project")
        PrePoMax.Persistence.StaticProjectArchive.Save(args[1], model, File.ReadAllBytes(path));
    else
    {
        var collection = new CaeResults.ResultsCollection();
        collection.Add("Fixture results", CaeResults.FrdFileReader.Read(path));
        string temporary = Path.GetTempFileName();
        try
        {
            using (var stream = File.Create(temporary))
                CaeResults.ResultsCollection.WriteToFileStream(collection, stream, System.IO.Compression.CompressionLevel.Optimal);
            var payload = PrePoMax.Persistence.ResultPayload.Create(2_005_001, ["Fixture results"], File.ReadAllBytes(temporary));
            PrePoMax.Persistence.StaticProjectArchive.Save(args[1], model, binaryResults: payload);
        }
        finally { File.Delete(temporary); }
    }
    return;
}

if (args.SequenceEqual(new[] { "-i", "analysis" }))
{
    if (File.Exists("test-sleep")) await Task.Delay(60000);
    if (File.Exists("test-results"))
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "archive-cube.frd"), "analysis.frd");
        File.WriteAllBytes("analysis.dat", [1]);
    }
    Environment.ExitCode = File.Exists("test-success") ? 0 : 17;
    return;
}
if (args.Length == 4 && args[0] == "--attach-project-results")
{
    var model = PrePoMax.Persistence.StaticProjectArchive.Open(args[1]).Model;
    byte[] frd = File.ReadAllBytes(args[2]);
    byte[] dat = File.ReadAllBytes(args[3]);
    PrePoMax.Persistence.StaticProjectArchive.Save(args[1], model, frd, dat);
    var reopened = PrePoMax.Persistence.StaticProjectArchive.Open(args[1]);
    if (!reopened.Frd.SequenceEqual(frd) || !reopened.Dat.SequenceEqual(dat))
        throw new InvalidDataException("Project result round trip failed.");
    Console.WriteLine("PASS: editable project and native result payloads reopened: " + args[1]);
    return;
}

if (args.Length == 4 && args[0] == "--write-multipart-cube")
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(args[3]);
    WriteReopenedModel(args[1], MultiPartProjectChecks.Create());
    return;
}

if (args.Length == 9 && args[0] is "--write-static-block" or "--write-mechanical-block")
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(args[3]);
    double Number(int index) => double.Parse(args[index], CultureInfo.InvariantCulture);
    var workflow = new StaticModelWorkflow(int.Parse(args[2], CultureInfo.InvariantCulture),
        Number(4), Number(5), Number(6), Number(7), Number(8));
    if (args[0] == "--write-mechanical-block")
    {
        workflow.Material.AddProperty(new CaeModel.Density(new[] { new[] { 2.5, 0d } }));
        foreach (var step in workflow.Steps)
        {
            step.BoundaryConditions["left"] = new CaeModel.FixedBC("left", "LEFT", CaeGlobals.RegionTypeEnum.NodeSetName, false);
            step.Loads.Clear();
            step.AddLoad(new CaeModel.CLoad("force", "RIGHT", CaeGlobals.RegionTypeEnum.NodeSetName, Number(8) / 4, 0, 0, false, false, 0));
            step.AddLoad(new CaeModel.GravityLoad("gravity", "SOLID", CaeGlobals.RegionTypeEnum.ElementSetName, .8, 0, 0, false, false, 0));
        }
    }
    WriteReopenedModel(args[1], workflow.Model);
    return;
}
if (args.Length == 4 && args[0] == "--write-static-cube")
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(args[3]);
    var workflow = new StaticModelWorkflow(int.Parse(args[2], CultureInfo.InvariantCulture));
    WriteReopenedModel(args[1], workflow.Model);
    return;
}
if (args.Length == 2 && args[0] == "--write-material")
{
    File.WriteAllText(args[1], MaterialChecks.CubeMaterial());
    File.WriteAllText(args[1] + ".mesh", MeshKeywordChecks.CubeMesh());
    File.WriteAllText(args[1] + ".step", StaticStepChecks.CubeStep());
    foreach (string kind in new[] { "nodes", "elements", "history", "end" })
        File.WriteAllText(args[1] + "." + kind, MaterialChecks.CubeOutput(kind));
    File.WriteAllText(args[1] + ".boundary", MaterialChecks.CubeBoundary());
    File.WriteAllText(args[1] + ".load1", MaterialChecks.CubeLoad(.25, false));
    File.WriteAllText(args[1] + ".load2", MaterialChecks.CubeLoad(.5, true));
    return;
}
if (args.Length == 4 && args[0] == "--run-solver")
{
    var logs = new CaeJob.Native.NativeLogFiles(Path.Combine(args[2], "stdout.log"), Path.Combine(args[2], "stderr.log"));
    var job = new CaeJob.Native.NativeJob(CaeJob.Native.NativeTool.Resolve("ccx", args[1]),
        new[] { "-i", args[3] }, args[2], TimeSpan.FromMinutes(2), logs: logs);
    var result = await job.RunAsync();
    Console.WriteLine($"Solver job: {result.Status}; elapsed {result.Elapsed}");
    if (result.Error != null) Console.Error.WriteLine(result.Error);
    Environment.ExitCode = result.Process?.ExitCode ?? 1;
    return;
}

if (args.Length >= 3 && args[0] == "--run-native")
{
    string tool = CaeJob.Native.NativeTool.Resolve("gmsh", args[1]);
    var job = new CaeJob.Native.NativeJob(tool, args.Skip(3), args[2], TimeSpan.FromSeconds(120));
    var result = await job.RunAsync();
    if (result.Process != null)
    {
        Console.Write(result.Process.StandardOutput);
        Console.Error.Write(result.Process.StandardError);
    }
    if (result.Error != null) Console.Error.WriteLine(result.Error);
    Environment.ExitCode = result.Process?.ExitCode ?? 1;
    return;
}

if (args.Length > 0)
{
    if (args.Length != 3 || (args[0] != "--native-surface" && args[0] != "--native-volume"))
        throw new ArgumentException("Usage: --native-surface input.stl output-prefix");
    if (args[0] == "--native-volume") NativeVolumeChecks.Run(args[1], args[2]);
    else NativeSurfaceChecks.Run(args[1], args[2]);
    return;
}

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception("FAIL: " + message); checks++; }
var originalCulture = CultureInfo.CurrentCulture;
string directory = Path.Combine(Path.GetTempPath(), "prepomax mesh géométrie " + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    foreach (string culture in new[] { "en-US", "it-IT" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        MaterialChecks.Run(Check);
        BoundaryChecks.Run(Check);
        MechanicalLoadChecks.Run(Check);
        ThermalLoadChecks.Run(Check);
        ShellPretensionChecks.Run(Check);
        HydrostaticChecks.Run(Check);
        PressureKeywordChecks.Run(Check);
        TemperatureChecks.Run(Check);
        TractionChecks.Run(Check);
        StaticStepChecks.Run(Check);
        MeshKeywordChecks.Run(Check);
        StaticModelWorkflow.Run(Check);
        ConstraintChecks.Run(Check);
        ExportStateChecks.Run(Check);
        ExportCopyChecks.Run(Check);
        FullWriterChecks.Run(Check);
        ActiveExportChecks.Run(Check);
        AnalysisArchiveChecks.Run(Check);
        StaticProjectChecks.Run(Check);
        MechanicalProjectChecks.Run(Check);
        MaterialPropertyProjectChecks.Run(Check);
        ReferenceAmplitudeProjectChecks.Run(Check);
        CylinderProjectChecks.Run(Check);
        ContactProjectChecks.Run(Check);
        ConstraintProjectChecks.Run(Check);
        UserKeywordProjectChecks.Run(Check);
        CoordinateSystemProjectChecks.Run(Check);
        MultiPartProjectChecks.Run(Check);
        QuadraticProjectChecks.Run(Check);
        ProjectSceneChecks.Run(Check);
        ProjectResultChecks.Run(Check);
        await ProjectServiceChecks.Run(Check);
        ProjectModelTreeChecks.Run(Check);
        ProjectElementSetChecks.Run(Check);
        ProjectRegionMaintenanceChecks.Run(Check);
        ProjectMeshNodeSelectionChecks.Run(Check);
        ProjectLoadChecks.Run(Check);
        ProjectGravityChecks.Run(Check);
        ProjectCentrifugalChecks.Run(Check);
        ProjectPressureChecks.Run(Check);
        ProjectSurfaceChecks.Run(Check);
        ProjectBoundaryChecks.Run(Check);
        ProjectNodeSetChecks.Run(Check);
        ProjectRemovalChecks.Run(Check);
        ProjectMeshImportChecks.Run(Check);
        await GmshImportChecks.Run(Check);
        CadTopologyChecks.Run(Check);
        await CadFaceSizingChecks.Run(Check);
        ProjectStepChecks.Run(Check);
        ProjectSectionChecks.Run(Check);
        ProjectFieldOutputChecks.Run(Check);
        ProjectHistoryOutputChecks.Run(Check);
        ProjectHistoryResultChecks.Run(Check);
        ProjectPlasticityChecks.Run(Check);
        ProjectCommandChecks.Run(Check);
        ProjectMaterialIdentityChecks.Run(Check);
        ProjectMaterialLifecycleChecks.Run(Check);
        InpImportChecks.Run(Check);
        ModelPropertiesChecks.Run(Check);
        StepCollectionChecks.Run(Check);
        string volumeFixture = Path.Combine(directory, "tetra è.mesh");
        File.WriteAllText(volumeFixture, "MeshVersionFormatted 1\nDimension 3\nVertices\n4\n0.25 0 0 0\n1.25 0 0 0\n0.25 1 0 0\n0.25 0 1 0\nTetrahedra\n1\n1 2 3 4 1\nEnd\n");
        var tetraMesh = MmgFileReader.Read3D(volumeFixture, ElementsToImport.Solid, MeshRepresentation.Mesh);
        Check(tetraMesh.Nodes.Count == 4 && tetraMesh.Elements.Count == 1 && tetraMesh.Parts.Count == 1, "Medit tetra import counts");
        Check(tetraMesh.Nodes[1].X == .25 && tetraMesh.Nodes[2].X == 1.25, "Medit invariant coordinates");
        Check(Math.Abs(((LinearTetraElement)tetraMesh.Elements.Values.Single()).GetVolume(tetraMesh.Nodes) - 1d / 6) < 1e-10, "Medit analytic tetra volume");
        Check(MmgFileReader.Read3D(Path.Combine(directory, "missing.mesh"), ElementsToImport.Solid, MeshRepresentation.Mesh) == null, "Medit missing-file contract");
        GmshWriterChecks.Run(directory, Check);
        string source = Path.Combine(directory, "surface α.stl");
        // Independent ASCII fixture: two triangles sharing a diagonal, with positive Z winding.
        File.WriteAllText(source, """
solid square
facet normal 0 0 1
outer loop
vertex 0.5 0.25 0
vertex 2.5 0.25 0
vertex 2.5 3.25 0
endloop
endfacet
facet normal 0 0 1
outer loop
vertex 0.5 0.25 0
vertex 2.5 3.25 0
vertex 0.5 3.25 0
endloop
endfacet
endsolid square
""");
        var mesh = StlFileReader.Read(source);
        Check(mesh.Nodes.Count == 4 && mesh.Elements.Count == 2 && mesh.Parts.Count == 1, "ASCII import welds shared vertices");
        Check(mesh.Parts.Values.Single() is GeometryPart && mesh.Elements.Values.All(e => e is LinearTriangleElement), "STL imports geometry triangles");
        double area = 0;
        foreach (var element in mesh.Elements.Values) { element.GetCG(mesh.Nodes, out double a); area += a; }
        Check(Math.Abs(area - 6) < 1e-10, "imported analytic area");
        Check(Math.Abs(mesh.BoundingBox.GetDiagonal() - Math.Sqrt(13)) < 1e-10, "imported bounds");
        string output = Path.Combine(directory, "export β.stl");
        StlFileWriter.Write(output, mesh, mesh.Parts.Keys.ToArray());
        using (var stream = File.OpenRead(output))
        {
            var document = STLDocument.Read(stream, true);
            Check(document.Facets.Count == 2, "export facet count");
            Check(document.Facets.All(f => f.Normal.Z == 1 && f.Normal.X == 0 && f.Normal.Y == 0), "export unit normals preserve winding");
        }
        var roundTrip = StlFileReader.Read(output);
        Check(roundTrip.Nodes.Count == 4 && roundTrip.Elements.Count == 2, "mesh export reimport counts");
        Check(roundTrip.Nodes.Values.Select(n => (n.X, n.Y, n.Z)).ToHashSet().SetEquals(mesh.Nodes.Values.Select(n => (n.X, n.Y, n.Z))), "mesh export reimport coordinates");
        // Independent binary encoding exercises binary detection and per-facet attribute bytes.
        string binary = Path.Combine(directory, "binary.stl");
        using (var writer = new BinaryWriter(File.Create(binary)))
        {
            writer.Write(new byte[80]); writer.Write((uint)1);
            foreach (float f in new float[] { 0, 0, 1, .5f, .25f, 0, 2.5f, .25f, 0, .5f, 3.25f, 0 }) writer.Write(f);
            writer.Write((ushort)0);
        }
        var binaryMesh = StlFileReader.Read(binary);
        Check(binaryMesh.Nodes.Count == 3 && binaryMesh.Elements.Count == 1, "binary STL imports into aggregate");
        binaryMesh.Elements.Values.Single().GetCG(binaryMesh.Nodes, out double binaryArea);
        Check(Math.Abs(binaryArea - 3) < 1e-10, "binary STL analytic area");
        string rawOutput = Path.Combine(directory, "raw triangles.stl");
        StlFileWriter.Write(rawOutput, new List<double[][]> { new[] { new[] { 0d, 0, 0 }, new[] { 1d, 0, 0 }, new[] { 0d, 1, 0 } } });
        Check(StlFileReader.Read(rawOutput).Elements.Count == 1, "raw triangle writer round trip");
        var separated = new List<double[][]>
        {
            new[] { new[] { 0d, 0, 0 }, new[] { 1d, 0, 0 }, new[] { 0d, 1, 0 } },
            new[] { new[] { 10d, 0, 0 }, new[] { 11d, 0, 0 }, new[] { 10d, 1, 0 } }
        };
        StlFileWriter.Write(rawOutput, separated);
        var disconnected = StlFileReader.Read(rawOutput);
        Check(disconnected.Parts.Count == 2 && disconnected.Nodes.Count == 6, "disconnected STL extracts two parts");
        string selectedName = disconnected.Parts.Keys.First();
        StlFileWriter.Write(output, disconnected, new[] { selectedName });
        var selected = StlFileReader.Read(output);
        Check(selected.Elements.Count == 1 && selected.Nodes.Count == 3, "selected-part export excludes other part");
        Check(selected.Nodes.Values.Select(n => (n.X, n.Y, n.Z)).ToHashSet().SetEquals(
            disconnected.Parts[selectedName].NodeLabels.Select(id => disconnected.Nodes[id]).Select(n => (n.X, n.Y, n.Z))), "selected-part export preserves coordinates");
        StlFileWriter.Write(rawOutput, new List<double[][]>
        {
            new[] { new[] { 0d, 0, 0 }, new[] { 1d, 0, 0 }, new[] { 0d, 1, 0 } },
            new[] { new[] { 1.0000001, 0, 0 }, new[] { 1d, 1, 0 }, new[] { 0d, 1, 0 } }
        });
        var welded = StlFileReader.Read(rawOutput);
        Check(welded.Nodes.Count == 4 && welded.Elements.Count == 2 && welded.Parts.Count == 1, "near-coincident vertices weld by model-relative tolerance");
        Check(StlFileReader.Read(Path.Combine(directory, "missing.stl")) == null, "missing STL preserves null contract");
    }
    Console.WriteLine($"PASS: {checks} mesh I/O characterization checks.");
}
finally { CultureInfo.CurrentCulture = originalCulture; Directory.Delete(directory, true); }
