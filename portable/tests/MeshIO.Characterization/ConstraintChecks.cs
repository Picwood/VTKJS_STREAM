using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class ConstraintChecks
{
    public static void Run(Action<bool, string> check)
    {
        string nl = Environment.NewLine;
        var tie = new Tie("bond", .125, false, "MASTER", RegionTypeEnum.SurfaceName,
                          "SLAVE", RegionTypeEnum.SurfaceName, false);
        var writer = new CalTie(tie);
        check(writer.GetKeywordString() == "*Tie, Name=bond, Position tolerance=0.125, Adjust=No" + nl,
            "tie explicit tolerance exports invariant keyword");
        check(writer.GetDataString() == "SLAVE, MASTER" + nl, "tie exports slave before master");
        tie.SwapMasterSlave();
        check(writer.GetDataString() == "MASTER, SLAVE" + nl, "tie export follows region swap");
        var automatic = new Tie("auto", "MASTER", RegionTypeEnum.SurfaceName, "SLAVE", RegionTypeEnum.SurfaceName, false);
        check(new CalTie(automatic).GetKeywordString() == "*Tie, Name=auto" + nl, "automatic tie omits tolerance and adjust overrides");
        bool rejected = false;
        try { new Tie("invalid", "same", RegionTypeEnum.SurfaceName, "same", RegionTypeEnum.SurfaceName, false); }
        catch (CaeException) { rejected = true; }
        check(rejected, "tie rejects identical surfaces");

        var body = new RigidBody("rigid", "RP", "NODES", RegionTypeEnum.NodeSetName, false);
        var ids = new Dictionary<string, int[]> { ["RP"] = new[] { 101, 102 } };
        check(new CalRigidBody(body, ids, null).GetKeywordString() ==
              "*Rigid body, Nset=NODES, Ref node=101, Rot node=102" + nl, "rigid body resolves reference nodes");
        body.RegionType = RegionTypeEnum.SurfaceName;
        check(new CalRigidBody(body, ids, "SURFACE_NODES").GetKeywordString().Contains("Nset=SURFACE_NODES,"),
            "rigid body resolves surface node set");

        var equation = new CalEquation(new double[] { 3, 1, .5, 2, 1, -.5 });
        check(equation.GetDataString() == "2" + nl + "3, 1, 0.5, 2, 1, -0.5" + nl,
            "equation coefficients use decimal points in all locales");
        foreach (int terms in new[] { 4, 5, 8 })
        {
            var values = Enumerable.Range(1, terms).SelectMany(i => new double[] { i, 1, .25 }).ToArray();
            string data = new CalEquation(values).GetDataString();
            check(data.EndsWith(nl) && data.Split(nl, StringSplitOptions.RemoveEmptyEntries).Length == 1 + (terms + 3) / 4,
                "equation continuation and final newline for " + terms + " terms");
        }
        rejected = false;
        try { new CalEquation(Array.Empty<double>()); }
        catch (NotSupportedException) { rejected = true; }
        check(rejected, "empty equation rejected at construction");
        rejected = false;
        try { new CalEquation(new double[] { 1, 2 }); }
        catch (NotSupportedException) { rejected = true; }
        check(rejected, "incomplete equation term rejected");

        var spring = new PointSpring("spring", 12, 10, 0, 30, false, true);
        var springData = new PointSpringData(spring);
        check(springData.GetSpringDirections().SequenceEqual(new[] { 1, 3 }) &&
              springData.GetSpringStiffnessValues().SequenceEqual(new[] { 10d, 30d }), "spring preserves active stiffness directions");
        var planar = new PointSpring("planar", 12, -10, 20, 30, true, true);
        check(planar.K1.Value == 0 && planar.K3.Value == 0 && planar.K2.Value == 20,
            "planar positive spring clamps negative and out-of-plane stiffness");
    }
}
