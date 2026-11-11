using System.Text;
using System.Text.Json.Nodes;
using CaeGlobals;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Persistence;

internal static class UserKeywordProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "user keyword project " + Guid.NewGuid() + ".pma");
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var keyword = new CalculixUserKeyword("*Preprint, echo=NO, model=NO, history=NO, contact=NO");
            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("user keywords");
            model.CalculixUserKeywords.Add(new[] { 0 }, keyword);
            string expected = CalculixFileWriter.GetInputText(model, ConvertPyramidsToEnum.Wedges);

            StaticProjectArchive.Save(path, model);
            var archive = AnalysisArchive.Open(path);
            var metadata = JsonNode.Parse(archive.ModelMetadata!)!;
            check(metadata["Version"]!.GetValue<int>() == 28 && metadata["UserKeywords"]!.AsArray().Count == 1,
                "user-keyword project uses external-mesh metadata v28");
            var reopened = StaticProjectArchive.Open(path).Model;
            var restored = reopened.CalculixUserKeywords.Single();
            check(restored.Key.SequenceEqual(new[] { 0 }) && restored.Value.Data == keyword.Data &&
                restored.Value.Active && !restored.Value.IsSuppressed && restored.Value.Keywords.Count == 0,
                "user-keyword metadata preserves path, text and supported state");
            string exported = CalculixFileWriter.GetInputText(reopened, ConvertPyramidsToEnum.Wedges);
            check(exported.StartsWith(keyword.Data + Environment.NewLine) && exported.Length == expected.Length,
                "user-keyword project preserves production export");

            var legacyTwenty = metadata.DeepClone();
            legacyTwenty.AsObject().Remove("UserKeywords");
            legacyTwenty.AsObject().Remove("CoordinateSystems");
            legacyTwenty["Version"] = 20;
            check(StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(legacyTwenty.ToJsonString()), archive.ModelMesh)
                    .CalculixUserKeywords == null,
                "metadata v20 external-mesh projects remain readable without user keywords");

            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(model))!;
            check(inline["Version"]!.GetValue<int>() == 27 && inline["UserKeywords"]![0]!["Path"]![0]!.GetValue<int>() == 0,
                "inline metadata v27 records the user-keyword insertion path");
            void Reject(Action<JsonNode> mutate, string message)
            {
                var invalid = inline.DeepClone();
                mutate(invalid);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(json => json.AsObject().Remove("UserKeywords"), "metadata v23 requires user-keyword metadata");
            Reject(json => json["UserKeywords"]![0]!["Path"] = new JsonArray(), "user-keyword metadata rejects an empty path");
            Reject(json => json["UserKeywords"]![0]!["Path"]![0] = -1, "user-keyword metadata rejects a negative path index");
            Reject(json => json["UserKeywords"]![0]!["Path"]![0] = 1000000, "user-keyword metadata rejects an unavailable insertion path");
            Reject(json => json["Version"] = 19, "pre-v21 metadata rejects user keywords");

            keyword.Active = false;
            bool unsupported = false;
            try { StaticModelMetadata.Serialize(model); }
            catch (NotSupportedException) { unsupported = true; }
            check(unsupported, "metadata rejects inactive user keywords with ambiguous writer semantics");
            keyword.Active = true;
            keyword.Suppress();
            unsupported = false;
            try { StaticModelMetadata.Serialize(model); }
            catch (NotSupportedException) { unsupported = true; }
            check(unsupported, "metadata rejects suppressed user keywords");
            keyword.Unsuppress();
            keyword.AddKeyword(new CalculixUserKeyword("** nested"));
            unsupported = false;
            try { StaticModelMetadata.Serialize(model); }
            catch (NotSupportedException) { unsupported = true; }
            check(unsupported, "metadata rejects nested user-keyword trees");
        }
        finally { File.Delete(path); }
    }
}
