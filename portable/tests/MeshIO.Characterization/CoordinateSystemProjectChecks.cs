using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Persistence;

internal static class CoordinateSystemProjectChecks
{
    public static void Run(Action<bool, string> check)
    {
        string path = Path.Combine(Path.GetTempPath(), "coordinate system project " + Guid.NewGuid() + ".pma");
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var coordinateSystem = new CoordinateSystem("fixture-frame", false)
            {
                Active = false,
                Type = CoordinateSystemTypeEnum.Cylindrical,
                X1 = new EquationContainer(typeof(StringLengthConverter), 1),
                Y1 = new EquationContainer(typeof(StringLengthConverter), 2),
                Z1 = new EquationContainer(typeof(StringLengthConverter), 3),
                X2 = new EquationContainer(typeof(StringLengthConverter), 2),
                Y2 = new EquationContainer(typeof(StringLengthConverter), 2),
                Z2 = new EquationContainer(typeof(StringLengthConverter), 3),
                X3 = new EquationContainer(typeof(StringLengthConverter), 1),
                Y3 = new EquationContainer(typeof(StringLengthConverter), 3),
                Z3 = new EquationContainer(typeof(StringLengthConverter), 3)
            };
            if (!coordinateSystem.IsProperlyDefined(out string? error)) throw new Exception(error);
            model.Mesh.CoordinateSystems.Add(coordinateSystem.Name, coordinateSystem);
            model.StepCollection.StepsList[0].BoundaryConditions["left"].CoordinateSystemName = coordinateSystem.Name;
            string expected = CalculixFileWriter.GetInputText(model, ConvertPyramidsToEnum.Wedges);

            StaticProjectArchive.Save(path, model);
            var archive = AnalysisArchive.Open(path);
            var metadata = JsonNode.Parse(archive.ModelMetadata!)!;
            check(metadata["Version"]!.GetValue<int>() == 28 && metadata["CoordinateSystems"]!.AsArray().Count == 1,
                "coordinate-system project uses external-mesh metadata v28");
            var reopened = StaticProjectArchive.Open(path).Model;
            var restored = reopened.Mesh.CoordinateSystems[coordinateSystem.Name];
            check(restored.Type == CoordinateSystemTypeEnum.Cylindrical && !restored.Active &&
                restored.Center().Coor.SequenceEqual(new[] { 1d, 2d, 3d }) &&
                restored.PointX().SequenceEqual(new[] { 2d, 2d, 3d }) &&
                restored.PointXY().SequenceEqual(new[] { 1d, 3d, 3d }),
                "coordinate-system metadata preserves type, state and defining points");
            check(reopened.StepCollection.StepsList[0].BoundaryConditions["left"].CoordinateSystemName == coordinateSystem.Name,
                "coordinate-system metadata preserves boundary references");
            string Canonical(string value) => Regex.Replace(value.Replace("\r\n", "\n"),
                @"(?m)^Hash: [^,\n]+, Date: \d{4}-\d{2}-\d{2},", "Hash: <id>, Date: <date>,");
            string actualDeck = Canonical(CalculixFileWriter.GetInputText(reopened, ConvertPyramidsToEnum.Wedges));
            string expectedDeck = Canonical(expected);
            check(actualDeck == expectedDeck, "coordinate-system project preserves production export");

            var inline = JsonNode.Parse(StaticModelMetadata.Serialize(model))!;
            check(inline["Version"]!.GetValue<int>() == 27, "inline coordinate-system metadata uses version 27");
            void Reject(Action<JsonNode> mutate, string message)
            {
                var invalid = inline.DeepClone();
                mutate(invalid);
                bool rejected = false;
                try { StaticModelMetadata.Deserialize(Encoding.UTF8.GetBytes(invalid.ToJsonString())); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, message);
            }
            Reject(json => json.AsObject().Remove("CoordinateSystems"), "metadata v23 requires coordinate systems");
            Reject(json => json["CoordinateSystems"]![0]!["Values"]!["Type"] = 99,
                "coordinate-system metadata rejects unknown types");
            Reject(json => json["CoordinateSystems"]![0]!["Values"]!["X2"] = 1,
                "coordinate-system metadata rejects coincident defining points");
            Reject(json => json["CoordinateSystems"]![0]!["Values"]!["Z1"] = "NaN",
                "coordinate-system metadata rejects nonfinite coordinates");
            Reject(json => json["Steps"]![0]!["Boundaries"]![0]!["Values"]!["CoordinateSystemName"] = "missing-frame",
                "coordinate-system metadata rejects dangling boundary references");
            Reject(json => json["Version"] = 21, "pre-v23 metadata rejects coordinate systems");

            coordinateSystem.CenterCreatedFrom = CsPointCreatedFromEnum.OnPoint;
            coordinateSystem.CenterCreationIds = [1];
            bool unsupported = false;
            try { StaticModelMetadata.Serialize(model); }
            catch (NotSupportedException) { unsupported = true; }
            check(unsupported, "metadata rejects selection-derived coordinate systems");
        }
        finally { File.Delete(path); }
    }
}
