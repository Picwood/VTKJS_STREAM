using CaeGlobals;

// Compiled unchanged by both the production harness and the legacy evaluator oracle.
internal static class EquationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var previous = MyNCalc.ExistingParameters;
        var dist = MyNCalc.Dist;
        var dx = MyNCalc.DistX;
        var dy = MyNCalc.DistY;
        var dz = MyNCalc.DistZ;
        try
        {
            MyNCalc.ExistingParameters = new CaeGlobals.OrderedDictionary<string, object>("test");
            MyNCalc.ExistingParameters.Add("a", 3d);
            MyNCalc.ExistingParameters.Add("part.Length", 12d);
            MyNCalc.Dist = (a, b) => Math.Abs(a - b);
            MyNCalc.DistX = (a, b) => a - b;
            MyNCalc.DistY = (a, b) => 2 * (a - b);
            MyNCalc.DistZ = (a, b) => 3 * (a - b);
            var cases = new (string, double)[] {
                ("", 0), ("=", 0), (" 42 ", 42), ("=2+3*4", 14), ("=(2+3)*4", 20),
                ("=7/2", 3.5), ("=7%3", 1), ("=-2*3", -6), ("=2<3", 1), ("=2>3", 0),
                ("=true and false", 0), ("=if(true,4,missing)", 4), ("=a*2", 6),
                ("=part.Length/2", 6), ("=[part.Length]/2", 6), ("=Pi+pi", 2*Math.PI),
                ("=Abs(-3)", 3), ("=aBs(-3)", 3), ("=Acos(1)", 0), ("=Asin(0)", 0),
                ("=Atan(0)", 0), ("=Ceiling(1.2)", 2), ("=Cos(0)", 1), ("=Exp(0)", 1),
                ("=Floor(1.8)", 1), ("=IEEERemainder(5,2)", 1), ("=Ln(1)", 0),
                ("=Log(8,2)", 3), ("=Log10(100)", 2), ("=Max(2,3)", 3), ("=Min(2,3)", 2),
                ("=Pow(2,3)", 8), ("=Round(2.5,0)", 2), ("=Sign(-3)", -1),
                ("=Sin(0)", 0), ("=Sqrt(9)", 3), ("=Tan(0)", 0), ("=Truncate(-1.8)", -1),
                ("=Dist(2,5)", 3), ("=DistX(2,5)", -3), ("=DistY(2,5)", -6), ("=DistZ(2,5)", -9)
            };
            foreach (var (formula, expected) in cases)
                check(Math.Abs(MyNCalc.SolveEquation(formula) - expected) < 1e-10, "equation " + formula);
            for (int i = 1; i <= 40; i++)
                check(Math.Abs(MyNCalc.SolveEquation($"=({i}+a)*2-{i}") - (i + 6)) < 1e-10, "parameter arithmetic corpus");
            foreach (string invalid in new[] { "=2+", "=missing+1", "2+3", "=Dist(1)" })
            {
                bool rejected = false;
                try { MyNCalc.SolveEquation(invalid); } catch (Exception) { rejected = true; }
                check(rejected, "invalid equation rejected: " + invalid);
            }
            check(MyNCalc.GetParameters("a+[part.Length]").SetEquals(new[] { "a", "part.Length" }), "parameter discovery");
            MyNCalc.ExistingParameters.Add("x", new[] { 1d, 2, 3 });
            check(MyNCalc.SolveArrayEquation("=x*2+a").SequenceEqual(new[] { 5d, 7, 9 }), "array parameter evaluation");
            MyNCalc.ExistingParameters.Remove("x");
            check(MyNCalc.SolveArrayEquation("=2+a").SequenceEqual(new[] { 5d }), "scalar array fallback");
        }
        finally
        {
            MyNCalc.ExistingParameters = previous;
            MyNCalc.Dist = dist; MyNCalc.DistX = dx; MyNCalc.DistY = dy; MyNCalc.DistZ = dz;
        }
    }
}
