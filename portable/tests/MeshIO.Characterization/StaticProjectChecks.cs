using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CaeGlobals;
using CaeModel;
using PrePoMax.Persistence;
using FileInOut.Output;
using FileInOut.Output.Calculix;

internal static class StaticProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        var original = new StaticModelWorkflow(2, 2.5, 1.25, .5, 2000, 3.75).Model;
        string path = Path.Combine(Path.GetTempPath(), "editable è " + Guid.NewGuid() + ".pma");
        try
        {
            byte[] frd = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "archive-cube.frd"));
            var geometry = new ProjectGeometrySource("source cube.step", Encoding.UTF8.GetBytes("ISO-10303-21;\nEND-ISO-10303-21;\n"));
            StaticProjectArchive.Save(path, original, frd, geometrySource: geometry);
            var opened = StaticProjectArchive.Open(path);
            var model = opened.Model;
            check(model.Materials["ELASTIC"].Properties.Count == 1 && model.Sections["solid"].MaterialName == "ELASTIC",
                "editable project preserves materials and section assignment");
            check(model.StepCollection.StepsList.Count == 2 && model.Mesh.Nodes[2].X == 2.5 && model.Mesh.Surfaces.ContainsKey("LOADED"),
                "editable project preserves geometry regions and steps");
            check(model.StepCollection.StepsList[0].FieldOutputs.Count == 2 && model.StepCollection.StepsList[0].HistoryOutputs.Count == 1,
                "editable project preserves field and history requests");
            check(opened.Frd.SequenceEqual(frd), "editable project retains result payload");
            check(opened.GeometrySource is { } retained && retained.FileName == geometry.FileName && retained.Data.SequenceEqual(geometry.Data) &&
                AnalysisArchive.Describe(path).Version == 7,
                "editable project retains a checksummed named geometry source");
            var force = (STLoad)model.StepCollection.StepsList[0].Loads["force"];
            force.F1 = new EquationContainer(typeof(StringForceConverter), 8);
            check(CalculixFileWriter.GetInputText(model, ConvertPyramidsToEnum.Wedges).Contains("2, 1, 2"), "reopened load is editable and changes input");
            check(((STLoad)original.StepCollection.StepsList[0].Loads["force"]).F1.Value == 3.75, "reopened model owns independent definitions");
            StaticProjectArchive.Save(path, opened with { Frd = null });
            check(((STLoad)StaticProjectArchive.Open(path).Model.StepCollection.StepsList[0].Loads["force"]).F1.Value == 8,
                "edited project can be saved and reopened again");
            check(opened.MaterialIdentities.SequenceEqual(StaticProjectArchive.Open(path).MaterialIdentities),
                "opened-project resave preserves material identities");
            var validArchive = AnalysisArchive.Open(path);
            byte[] inlineMetadata = StaticModelMetadata.Serialize(model);
            string input = Encoding.UTF8.GetString(validArchive.Input);
            input = Regex.Replace(input, @"Date: \d{4}-\d{2}-\d{2},", "Date: 2001-01-01,");
            input = input.Replace("\r\n", "\n").Replace("\n", "\r\n");
            AnalysisArchive.Save(path, validArchive with { Input = Encoding.UTF8.GetBytes(input) });
            check(StaticProjectArchive.Open(path).Model.StepCollection.StepsList.Count == 2,
                "project reopens across heading dates and platform newlines");
            void RejectMetadata(Action<JsonNode> change, string label)
            {
                var json = JsonNode.Parse(inlineMetadata)!;
                change(json);
                AnalysisArchive.Save(path, validArchive with { ModelMetadata = Encoding.UTF8.GetBytes(json.ToJsonString()), ModelMesh = null });
                bool failed = false;
                try { StaticProjectArchive.Open(path); }
                catch (InvalidDataException) { failed = true; }
                check(failed, label);
            }
            RejectMetadata(json => json["NodeSets"]![0]!["Labels"]![0] = 99999,
                "project rejects dangling node region");
            RejectMetadata(json => json["Steps"]![0]!["State"]!["Values"]!["SolverType"] = 99999,
                "project rejects unknown solver enum");
            RejectMetadata(json => json["Steps"]![0]!["Loads"]![0]!["Values"]!["F1"] = 100,
                "project rejects metadata inconsistent with archived input");
            RejectMetadata(json => json["Unexpected"] = true, "project rejects unknown metadata fields");
            AnalysisArchive.Save(path, validArchive);
            byte[] before = File.ReadAllBytes(path);
            original.StepCollection.StepsList[0].AddLoad(new MomentLoad("unsupported", "RIGHT", RegionTypeEnum.NodeSetName, 1, 0, 0, false, false, 0));
            bool rejected = false;
            try { StaticProjectArchive.Save(path, original); }
            catch (NotSupportedException) { rejected = true; }
            check(rejected && File.ReadAllBytes(path).SequenceEqual(before), "unsupported model definition rejected without overwriting project");
            var archive = AnalysisArchive.Open(path);
            AnalysisArchive.Save(path, archive with { ModelMetadata = Encoding.UTF8.GetBytes("{\"Version\":99}"), ModelMesh = null });
            rejected = false;
            try { StaticProjectArchive.Open(path); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, "editable metadata rejects unsupported schema version");
        }
        finally { File.Delete(path); }
    }
}
