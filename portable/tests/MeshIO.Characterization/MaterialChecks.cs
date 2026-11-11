using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;
using System.Globalization;

internal static class MaterialChecks
{
    public static string CubeMaterial()
    {
        var material = new Material("ELASTIC");
        var elastic = new Elastic(new[] { new[] { 1000d, 0, 0 } });
        material.AddProperty(elastic);
        return new CalMaterial(material).GetKeywordString() + new CalElastic(elastic, false).GetKeywordString() +
               new CalElastic(elastic, false).GetDataString() + CubeSection();
    }
    public static string CubeSection()
    {
        var section = new SolidSection("solid", "ELASTIC", "SOLID", RegionTypeEnum.ElementSetName, 1, false);
        var keyword = new CalSolidSection(section);
        return keyword.GetKeywordString() + keyword.GetDataString();
    }
    public static string CubeLoad(double force, bool replace)
    {
        var load = new CLoad("right-force", "RIGHT", RegionTypeEnum.NodeSetName, force, 0, 0, false, false, 0);
        var keyword = new CalCLoad(load, null, ComplexLoadTypeEnum.None)
            { OpType = replace ? OpTypeEnum.New : OpTypeEnum.None };
        return keyword.GetKeywordString() + keyword.GetDataString();
    }
    public static string CubeBoundary()
    {
        var left = new DisplacementRotation("left", "LEFT", RegionTypeEnum.NodeSetName, false, false, 0)
            { U1 = new EquationContainer(typeof(StringLengthDOFConverter), 0) };
        var transverse = new DisplacementRotation("transverse", "ALLNODES", RegionTypeEnum.NodeSetName, false, false, 0)
            { U2 = new EquationContainer(typeof(StringLengthDOFConverter), 0),
              U3 = new EquationContainer(typeof(StringLengthDOFConverter), 0) };
        return BoundaryText(left) + BoundaryText(transverse);
    }
    private static string BoundaryText(DisplacementRotation boundary)
    {
        var keyword = new CalDisplacementRotation(boundary, null, null, ComplexLoadTypeEnum.None);
        return keyword.GetKeywordString() + keyword.GetDataString();
    }
    public static string CubeOutput(string kind)
    {
        CalculixKeyword keyword = kind switch
        {
            "nodes" => new CalNodeFile(new NodalFieldOutput("displacement", NodalFieldVariable.U), 1),
            "elements" => new CalElFile(new ElementFieldOutput("stress-strain", ElementFieldVariable.S | ElementFieldVariable.E), 1),
            "history" => new CalNodePrint(new NodalHistoryOutput("right-history", NodalHistoryVariable.U,
                "RIGHT", RegionTypeEnum.NodeSetName), "RIGHT", 1),
            "end" => new CalEndStep(),
            _ => throw new ArgumentException(nameof(kind))
        };
        return keyword.GetKeywordString() + keyword.GetDataString();
    }
    public static void Run(Action<bool, string> check)
    {
        check(CubeSection().Contains("Elset=SOLID, Material=ELASTIC"), "solid section links region and material");
        var plane = new CalSolidSection(new SolidSection("plane", "ELASTIC", "SOLID", RegionTypeEnum.ElementSetName, .125, true));
        check(double.Parse(plane.GetDataString(), CultureInfo.InvariantCulture) == .125, "2D section emits invariant thickness");
        check(CubeLoad(.25, false).Contains("RIGHT, 1, 0.25"), "nodal-set load exports axial component");
        check(CubeLoad(.5, true).Contains("op=New") && CubeLoad(.5, true).Contains("RIGHT, 1, 0.5"),
              "second-step load replaces earlier load");
        string boundary = CubeBoundary();
        check(boundary.Contains("LEFT, 1, 1, 0") && boundary.Contains("ALLNODES, 2, 2, 0") &&
              boundary.Contains("ALLNODES, 3, 3, 0"), "cube boundary constrains intended translations");
        check(!boundary.Contains("LEFT, 2") && !boundary.Contains("ALLNODES, 1"), "cube boundary leaves other translations free");
        var imposed = new DisplacementRotation("imposed", "RIGHT", RegionTypeEnum.NodeSetName, false, false, 0)
            { U1 = new EquationContainer(typeof(StringLengthDOFConverter), .00125) };
        check(BoundaryText(imposed).Contains("RIGHT, 1, 1, 0.00125"), "prescribed displacement uses invariant formatting");
        check(imposed.GetConstrainedDirections().SequenceEqual(new[] { 1 }) && imposed.IsProperlyDefined(out _),
              "prescribed boundary model validates selected degree of freedom");
        check(CubeOutput("nodes").Contains("*Node file") && CubeOutput("nodes").TrimEnd().EndsWith("U"),
              "nodal displacement output keyword");
        check(CubeOutput("elements").Contains("*El file") && CubeOutput("elements").Contains("S, E"),
              "element stress and strain output flags");
        check(CubeOutput("history").Contains("Nset=RIGHT") && CubeOutput("history").TrimEnd().EndsWith("U"),
              "nodal history request targets right-face set");
        check(CubeOutput("end").Trim() == "*End step", "step termination keyword");
        var material = new Material("steel");
        var elastic = new Elastic(new[] { new[] { 1000.5, .25, 20 }, new[] { 900.25, .3, 100 } });
        material.AddProperty(elastic);
        check(ReferenceEquals(material.GetProperty<Elastic>(), elastic), "material owns selected elastic property");
        var keyword = new CalElastic(elastic, true);
        var rows = keyword.GetDataString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var values = rows.Select(row => row.Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray()).ToArray();
        check(values.Length == 2 && values[0].SequenceEqual(new[] { 1000.5, .25, 20 }) &&
              values[1].SequenceEqual(new[] { 900.25, .3, 100 }), "temperature-dependent elastic keyword uses invariant values");
        check(new CalElastic(elastic, false).GetDataString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length == 1,
              "constant elastic keyword emits first row only");
        check(1.0.ToCalculiX16String(true) == "1.", "CalculiX forced decimal separator");
        check(1.2345678901234567e100.ToCalculiX16String().Length <= 16,
              "CalculiX numeric token respects width");
    }
}
