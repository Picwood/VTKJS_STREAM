using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;

internal static class FullWriterChecks
{
    public static void Run(Action<bool, string> check)
    {
        var workflow = new StaticModelWorkflow(1);
        var model = workflow.Model;
        var mesh = model.Mesh;
        var surfaces = mesh.Surfaces;
        var sets = mesh.ElementSets;
        var element = mesh.Elements[1];
        string baseline = workflow.Export();
        check(ReferenceEquals(surfaces, mesh.Surfaces) && ReferenceEquals(sets, mesh.ElementSets) &&
              ReferenceEquals(element, mesh.Elements[1]), "full writer restores caller identities on success");
        var keyword = new CalculixUserKeyword("** portable user keyword");
        model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("user");
        model.CalculixUserKeywords.Add(new[] { 0 }, keyword);
        check(workflow.Export().StartsWith("** portable user keyword" + Environment.NewLine),
            "full writer inserts user keyword at requested root index");
        check(keyword.Keywords.Count == 0 && keyword.Parent == null && !keyword.IsSuppressed,
            "full writer leaves user keyword state intact");
        model.CalculixUserKeywords.Clear();
        check(workflow.Export() == baseline, "full writer restores baseline after user keyword removal");

        var part = mesh.Parts.Values.First();
        part.Active = false;
        bool rejected = false;
        try { workflow.Export(); }
        catch (CaeException) { rejected = true; }
        finally { part.Active = true; }
        check(rejected && ReferenceEquals(element, mesh.Elements[1]),
            "prepared-model API rejects inactive parts without deleting them");

        string path = Path.Combine(Path.GetTempPath(), "portable export è " + Guid.NewGuid() + ".inp");
        try
        {
            FileInOut.Output.CalculixFileWriter.WritePreparedModel(path, model, ConvertPyramidsToEnum.Wedges);
            check(File.ReadAllText(path) == baseline, "production file writer matches generated input");
            check(!File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }),
                "production file writer emits UTF-8 without BOM");
            part.Active = false;
            try { FileInOut.Output.CalculixFileWriter.WritePreparedModel(path, model, ConvertPyramidsToEnum.Wedges); }
            catch (CaeException) { }
            finally { part.Active = true; }
            check(File.ReadAllText(path) == baseline, "rejected export preserves existing destination");
        }
        finally { File.Delete(path); }

        var load = (STLoad)workflow.Steps[0].Loads["force"];
        string region = load.SurfaceName;
        load.SurfaceName = "missing-surface";
        rejected = false;
        try { workflow.Export(); }
        catch (CaeException) { rejected = true; }
        finally { load.SurfaceName = region; }
        check(rejected, "full writer propagates invalid load-region failure");
        check(ReferenceEquals(surfaces, mesh.Surfaces) && ReferenceEquals(sets, mesh.ElementSets) &&
              ReferenceEquals(element, mesh.Elements[1]), "full writer restores caller identities after failure");
        check(workflow.Export() == baseline, "full writer remains repeatable after failed export");
    }
}
