using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output.Calculix;
using System.Text;

// Real FeModel assembly exported by the production CalculixFileWriter.
internal sealed class StaticModelWorkflow
{
    public FeModel Model { get; } = new FeModel("Static block", null, false);
    public ModelProperties Properties => Model.Properties;
    public FeMesh Mesh => Model.Mesh;
    public Material Material { get; }
    public SolidSection Section { get; }
    public StepCollection StepCollection => Model.StepCollection;
    public List<Step> Steps => StepCollection.StepsList;

    public StaticModelWorkflow(int stepCount, double length = 1, double width = 1, double height = 1,
                               double youngsModulus = 1000, double totalForce = 1)
    {
        if (stepCount < 1 || stepCount > 2) throw new ArgumentOutOfRangeException(nameof(stepCount));
        foreach (double value in new[] { length, width, height, youngsModulus })
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException("dimensionsOrModulus");
        if (!double.IsFinite(totalForce)) throw new ArgumentOutOfRangeException(nameof(totalForce));
        double[][] coordinates = { new double[] {0,0,0}, new double[] {1,0,0}, new double[] {1,1,0},
            new double[] {0,1,0}, new double[] {0,0,1}, new double[] {1,0,1}, new double[] {1,1,1}, new double[] {0,1,1} };
        var nodes = coordinates.Select((p, i) => new FeNode(i + 1, p[0] * length, p[1] * width, p[2] * height)).ToDictionary(n => n.Id);
        Properties.ModelSpace = ModelSpaceEnum.ThreeD;
        Model.SetMesh(new FeMesh(nodes, new Dictionary<int, FeElement>
            { [1] = new LinearHexaElement(1, Enumerable.Range(1, 8).ToArray()) }, MeshRepresentation.Mesh));
        Mesh.NodeSets.Add("LEFT", new FeNodeSet("LEFT", new[] { 1, 4, 5, 8 }));
        Mesh.NodeSets.Add("RIGHT", new FeNodeSet("RIGHT", new[] { 2, 3, 6, 7 }));
        Mesh.NodeSets.Add("ALLNODES", new FeNodeSet("ALLNODES", Enumerable.Range(1, 8).ToArray()));
        Mesh.ElementSets.Add("SOLID", new FeElementSet("SOLID", new[] { 1 }));
        var surface = new FeSurface("LOADED", "RIGHT");
        surface.AddElementFace(FeFaceName.S4, "SOLID");
        Mesh.Surfaces.Add(surface.Name, surface);
        Material = new Material("ELASTIC");
        Material.AddProperty(new Elastic(new[] { new[] { youngsModulus, 0, 0 } }));
        Section = new SolidSection("solid", Material.Name, "SOLID", RegionTypeEnum.ElementSetName, 1, false);
        Model.Materials.Add(Material.Name, Material);
        Model.Sections.Add(Section.Name, Section);
        for (int i = 0; i < stepCount; i++)
        {
            var step = new StaticStep("load-" + (i + 1), false) { OutputFrequency = 1 };
            step.AddBoundaryCondition(new DisplacementRotation("left", "LEFT", RegionTypeEnum.NodeSetName, false, false, 0)
                { U1 = new EquationContainer(typeof(StringLengthDOFConverter), 0) });
            step.AddBoundaryCondition(new DisplacementRotation("transverse", "ALLNODES", RegionTypeEnum.NodeSetName, false, false, 0)
                { U2 = new EquationContainer(typeof(StringLengthDOFConverter), 0), U3 = new EquationContainer(typeof(StringLengthDOFConverter), 0) });
            step.AddLoad(new STLoad("force", "LOADED", RegionTypeEnum.SurfaceName, totalForce * (i + 1), 0, 0, false, false, 0));
            step.AddFieldOutput(new NodalFieldOutput("displacement", NodalFieldVariable.U));
            step.AddFieldOutput(new ElementFieldOutput("stress-strain", ElementFieldVariable.S | ElementFieldVariable.E));
            step.AddHistoryOutput(new NodalHistoryOutput("right-history", NodalHistoryVariable.U, "RIGHT", RegionTypeEnum.NodeSetName));
            StepCollection.AddStep(step);
        }
    }

    public string Export()
    {
        return FileInOut.Output.CalculixFileWriter.GetInputText(Model, ConvertPyramidsToEnum.Wedges);
    }

    public static void Run(Action<bool, string> check)
    {
        SurfaceTractionChecks.Run(check);
        var model = new StaticModelWorkflow(2);
        FeModelChecks.Run(model.Model, check);
        string first = model.Export();
        check(first == model.Export(), "complete static export is repeatable");
        check(model.Mesh.Nodes.Count == 8 && model.Mesh.Elements.Count == 1 && model.Mesh.NodeSets.Count == 3,
            "complete export preserves mesh and regions");
        check(first.Split("*Step").Length == 3 && first.Split("*End step").Length == 3,
            "complete static export writes two balanced steps");
        check(first.Contains("2, 1, 0.25") && first.Contains("2, 1, 0.5"), "complete export uses each step load state");
        var force = (STLoad)model.Steps[1].Loads["force"];
        force.F1 = new EquationContainer(typeof(StringForceConverter), 3);
        string changed = model.Export();
        check(changed.Contains("2, 1, 0.75") && !changed.Contains("2, 1, 0.5"),
            "editing a step load changes the generated input");
    }
}
