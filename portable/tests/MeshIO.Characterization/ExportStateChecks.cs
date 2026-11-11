using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output;

internal static class ExportStateChecks
{
    public static void Run(Action<bool, string> check)
    {
        var model = new StaticModelWorkflow(1).Model;
        var mesh = model.Mesh;
        var surfaces = mesh.Surfaces;
        var sets = mesh.ElementSets;
        var element = mesh.Elements[1];
        var pointMass = new PointMassSection("mass", "RIGHT", RegionTypeEnum.NodeSetName, 1, false)
            { ElementSetName = "original-point" };
        var distributed = new DistributedMassSection("distributed", "LOADED", RegionTypeEnum.SurfaceName, 2, false)
            { ElementSetName = "original-distributed" };
        model.Sections.Add(pointMass.Name, pointMass);
        model.Sections.Add(distributed.Name, distributed);
        CalculixExportState scope = null;
        try
        {
            using (scope = new CalculixExportState(model))
            {
                check(!ReferenceEquals(mesh.Surfaces, surfaces) && !ReferenceEquals(mesh.ElementSets, sets),
                    "export isolates temporary collections");
                check(mesh.Surfaces.ContainsKey("loaded") && mesh.ElementSets.ContainsKey("solid"),
                    "export copies retain case-insensitive comparers");
                mesh.Surfaces["LOADED"].ElementFaces[FeFaceName.S4] = "temporary";
                mesh.ElementSets["SOLID"].Labels[0] = 99;
                check(surfaces["LOADED"].ElementFaces[FeFaceName.S4] == "SOLID" && sets["SOLID"].Labels[0] == 1,
                    "export copies isolate nested faces and labels");
                mesh.ElementSets.Add("temporary", new FeElementSet("temporary", new[] { 1 }));
                scope.ReplacedElements.Add(1, element);
                mesh.Elements[1] = new LinearHexaElement(1, element.NodeIds.ToArray());
                pointMass.ElementSetName = "generated-point";
                distributed.ElementSetName = "generated-distributed";
                throw new InvalidOperationException("simulated keyword failure");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message == "simulated keyword failure") { }
        check(ReferenceEquals(mesh.Surfaces, surfaces) && ReferenceEquals(mesh.ElementSets, sets),
            "failed export restores collection identity");
        check(ReferenceEquals(mesh.Elements[1], element) && !sets.ContainsKey("temporary"),
            "failed export restores replaced element and discards generated sets");
        check(pointMass.ElementSetName == "original-point" && distributed.ElementSetName == "original-distributed",
            "failed export restores both mass section metadata values");
        scope.Dispose();
        check(ReferenceEquals(mesh.Surfaces, surfaces), "export cleanup is idempotent");
        using (new CalculixExportState(model)) pointMass.ElementSetName = "successful-export";
        check(pointMass.ElementSetName == "original-point" && ReferenceEquals(mesh.ElementSets, sets),
            "successful export also restores caller state");

        // A malformed element set makes copying fail after surfaces have been copied.
        sets.Add("invalid", null);
        bool rejected = false;
        try { using var unused = new CalculixExportState(model); }
        catch (NullReferenceException) { rejected = true; }
        finally { sets.Remove("invalid"); }
        check(rejected && ReferenceEquals(mesh.Surfaces, surfaces) && ReferenceEquals(mesh.ElementSets, sets),
            "copy failure leaves caller collections untouched");

        var ordered = new CaeGlobals.OrderedDictionary<string, int[]>("ordered", StringComparer.OrdinalIgnoreCase);
        ordered.Add("last", new[] { 3 });
        ordered.Insert(0, "first", new[] { 1 });
        ordered.Insert(1, "middle", new[] { 2 });
        var copied = ordered.CopyValues(values => values.ToArray());
        check(copied.Keys.SequenceEqual(new[] { "first", "middle", "last" }) && copied.ContainsKey("FIRST"),
            "explicit value copy preserves insertion ordering and key comparer");
        copied["FIRST"][0] = 10;
        check(ordered["first"][0] == 1, "explicit value copy owns mutable values");
    }
}
