using CaeGlobals;
using CaeMesh;
using CaeModel;

internal static class SurfaceTractionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var block = new StaticModelWorkflow(1, 2.5, 1.25, .5);
        var load = new STLoad("vector", "LOADED", RegionTypeEnum.SurfaceName, 12, -8, 4, false, true, 30)
            { AmplitudeName = "ramp" };
        var loads = SurfaceTractionConverter.GetNodalCLoadsFromConstantSurfaceTraction(block.Mesh, load);
        check(loads.Select(l => l.NodeId).Order().SequenceEqual(new[] { 2, 3, 6, 7 }), "traction targets loaded face nodes");
        check(loads.All(l => l.F1.Value == 3 && l.F2.Value == -2 && l.F3.Value == 1), "traction conserves all force components");
        check(loads.All(l => l.Complex && l.PhaseDeg.Value == 30 && l.AmplitudeName == "ramp"), "traction preserves phase and amplitude");
        SurfaceTractionConverter.GetDistributedNodalValuesFromSurface(block.Mesh, "LOADED", out var weights, out double area);
        check(Math.Abs(area - .625) < 1e-12 && Math.Abs(weights.Values.Sum() - area) < 1e-12, "traction uses geometric face area");

        var nodes = new[] { new FeNode(1, 0, 0, 0), new FeNode(2, 2, 0, 0),
            new FeNode(3, 5, 0, 0), new FeNode(4, 0, 1, 0) }.ToDictionary(n => n.Id);
        var mesh = new FeMesh(nodes, new Dictionary<int, FeElement> {
            [1] = new LinearTriangleElement(1, new[] { 1, 2, 4 }),
            [2] = new LinearTriangleElement(2, new[] { 2, 3, 4 }) }, MeshRepresentation.Mesh);
        mesh.ElementSets.Add("edges", new FeElementSet("edges", new[] { 1, 2 }));
        var surface = new FeSurface("edges");
        surface.AddElementFace(FeFaceName.S3, "edges");
        mesh.Surfaces.Add(surface.Name, surface);
        SurfaceTractionConverter.GetDistributedNodalValuesFromSurface(mesh, "edges", out weights, out area, id => id == 1 ? 2 : 3);
        check(area == 13 && weights[1] == 2 && weights[2] == 6.5 && weights[3] == 4.5,
            "shell edges integrate thickness and accumulate shared nodes");
        var edgeLoad = new STLoad("edge", "edges", RegionTypeEnum.SurfaceName, 26, 0, 0, true, false, 0);
        loads = SurfaceTractionConverter.GetNodalCLoadsFromConstantSurfaceTraction(mesh, edgeLoad, id => id == 1 ? 2 : 3);
        check(loads.ToDictionary(l => l.NodeId)[2].F1.Value == 13 && loads.Sum(l => l.F1.Value) == 26,
            "shell traction distributes total force by weighted edge area");
        void Reject(Action action, string name)
        {
            try { action(); check(false, name); }
            catch (CaeException) { check(true, name); }
        }
        Reject(() => SurfaceTractionConverter.GetNodalCLoadsFromConstantSurfaceTraction(mesh, edgeLoad), "shell traction requires thickness");
        Reject(() => SurfaceTractionConverter.GetNodalCLoadsFromConstantSurfaceTraction(mesh, edgeLoad, _ => 0), "shell traction rejects zero thickness");
        block.Mesh.Surfaces["LOADED"].ClearElementFaces();
        Reject(() => SurfaceTractionConverter.GetNodalCLoadsFromConstantSurfaceTraction(block.Mesh, load), "nonzero traction rejects empty surface");
        load.FMagnitude = new EquationContainer(typeof(StringForceConverter), 0);
        check(SurfaceTractionConverter.GetNodalCLoadsFromConstantSurfaceTraction(block.Mesh, load).Length == 0,
            "zero traction produces no nodal forces");
    }
}
