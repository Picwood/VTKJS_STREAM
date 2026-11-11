using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CaeMesh;
using CaeModel;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectCommandChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Directory.CreateTempSubdirectory("project-commands-").FullName;
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var material = model.Materials["ELASTIC"];
            material.Description = "keep this description";
            material.Properties.Insert(0, new Density([[2.5, 0]]));
            material.AddProperty(new Plastic([[100, 0, 0], [120, .1, 0]]) { Hardening = PlasticHardening.Combined });
            material.AddProperty(new ThermalExpansion([[1.2e-5, 0]]));
            model.Cylinders.Add("tool", new FeCylinder("tool") { Radius = 4, Height = 10, Direction = [0, 0, 2], Active = false });
            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("user keywords");
            model.CalculixUserKeywords.Add([0], new CalculixUserKeyword("*Preprint, echo=NO"));
            string source = Path.Combine(root, "initial.pma");
            StaticProjectArchive.Save(source, model, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures/archive-cube.frd")),
                geometrySource: new ProjectGeometrySource("retained.step", [1, 2, 3]));
            byte[] original = File.ReadAllBytes(source);
            var session = new ProjectCommandSession(Path.GetRelativePath(Environment.CurrentDirectory, root));
            session.Commit(source);
            var command = new MaterialElasticCommand(Guid.NewGuid(), 1, "ELASTIC", YoungsModulus: 2000);
            var receipt = session.Execute(command);
            string edited = session.ProjectPath!;
            var before = AnalysisArchive.Open(source);
            var after = AnalysisArchive.Open(edited);
            var expected = JsonNode.Parse(before.ModelMetadata!)!;
            expected["Materials"]![0]!["Properties"]![1]!["Data"]![0]![0] = 2000d;
            check(JsonNode.DeepEquals(expected, JsonNode.Parse(after.ModelMetadata!)),
                "partial material command changes exactly E across the whole metadata document; density/plasticity/thermal/order/sections/cylinders/user keywords survive");
            check(before.ModelMesh!.SequenceEqual(after.ModelMesh!) && before.GeometrySource!.SequenceEqual(after.GeometrySource!) &&
                before.GeometryFileName == after.GeometryFileName && after.Frd == null,
                "material command preserves managed geometry and exact mesh while invalidating results");
            check(receipt.SourceRevision == 1 && receipt.CommittedRevision == 2 && session.Revision == 2 &&
                receipt.SourceSha256 == Convert.ToHexString(SHA256.HashData(original)) &&
                receipt.ResultSha256 == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(edited))) &&
                session.CurrentCommandId == command.OperationId && session.Commands.Single().Equals(receipt),
                "receipt binds stable operation identity and exact command to source revision and both archive hashes");
            check(session.Execute(command) == receipt && session.Revision == 2 && session.ProjectPath == edited,
                "identical retry returns the receipt without a second edit");

            void Rejected(Action action, Type errorType, string message)
            {
                int revision = session.Revision;
                string current = session.ProjectPath!;
                byte[] bytes = File.ReadAllBytes(current);
                int receipts = session.Commands.Length;
                bool failed = false;
                try { action(); }
                catch (Exception error) when (errorType.IsInstanceOfType(error)) { failed = true; }
                check(failed && revision == session.Revision && current == session.ProjectPath && receipts == session.Commands.Length &&
                    bytes.SequenceEqual(File.ReadAllBytes(current)), message);
            }
            Rejected(() => session.Execute(command with { YoungsModulus = 3000 }), typeof(ProjectCommandConflictException), "reused operation ID cannot change its payload");
            Rejected(() => session.Execute(command with { OperationId = Guid.NewGuid() }), typeof(ProjectCommandConflictException), "stale new operation cannot publish");
            foreach (double invalid in new[] { 0, -1, double.NaN, double.PositiveInfinity })
                Rejected(() => session.Execute(new(Guid.NewGuid(), session.Revision, "ELASTIC", invalid)), typeof(ArgumentException), "invalid elastic command preserves bytes/revision/receipts");
            Rejected(() => session.Execute(new(Guid.NewGuid(), session.Revision, "ELASTIC")), typeof(ArgumentException), "empty patch is rejected atomically");
            Rejected(() => session.Execute(new(Guid.Empty, session.Revision, "ELASTIC", 3000)), typeof(ArgumentException), "empty operation identity is rejected");
            Rejected(() => session.Execute(new(Guid.NewGuid(), session.Revision, "missing", 3000)), typeof(NotSupportedException), "unknown material cannot narrow the model");
            Rejected(() => session.Commit(session.ProjectPath!), typeof(ArgumentException), "existing history files cannot be republished or overwritten");
            session.MoveHistory(false);
            check(session.ProjectPath == source && session.Revision == 3 && session.CurrentCommandId == null && ProjectResults.List(source).Length > 0,
                "undo restores source bytes and result validity");
            check(session.Execute(command) == receipt && session.Revision == 3 && session.ProjectPath == source,
                "retry after undo does not reactivate the undone command");
            session.MoveHistory(true);
            check(session.ProjectPath == edited && session.CurrentCommandId == command.OperationId && session.Revision == 4,
                "redo restores the same command identity with a new workspace revision");
            session.MoveHistory(false);
            var branch = session.Execute(new(Guid.NewGuid(), session.Revision, "ELASTIC", PoissonsRatio: .2));
            check(!session.CanRedo && ProjectMaterials.List(session.ProjectPath!).Single().YoungsModulus == 1000 &&
                ProjectMaterials.List(session.ProjectPath!).Single().PoissonsRatio == .2 && !File.Exists(edited),
                "branch patch preserves omitted E and discards redo without losing command receipts");
            Rejected(() => session.MoveHistory(true), typeof(ProjectCommandConflictException), "discarded redo cannot reappear");
            check(session.Execute(command) == receipt && session.Revision == branch.CommittedRevision,
                "retry after branch deletion returns the original receipt without replay");

            foreach (string field in new[] { "UnknownCustomDefinition", "Version" })
            {
                var metadata = JsonNode.Parse(before.ModelMetadata!)!;
                metadata[field] = 999;
                string unsupported = Path.Combine(root, field + ".pma");
                AnalysisArchive.Save(unsupported, before with { ModelMetadata = Encoding.UTF8.GetBytes(metadata.ToJsonString()) });
                byte[] untouched = File.ReadAllBytes(unsupported);
                Rejected(() => session.Commit(unsupported), typeof(InvalidDataException), "unsupported metadata cannot replace current project");
                string candidate = Path.Combine(root, "refused.pma");
                bool failed = false;
                try { ProjectMaterials.PatchElasticCopy(unsupported, candidate, "ELASTIC", 3000, null); }
                catch (InvalidDataException) { failed = true; }
                check(failed && !File.Exists(candidate) && untouched.SequenceEqual(File.ReadAllBytes(unsupported)),
                    "unsupported content is refused before material edit publication");
            }
            string equationPath = Path.Combine(root, "equation.pma");
            var parameters = CaeGlobals.MyNCalc.ExistingParameters;
            try
            {
                CaeGlobals.MyNCalc.ExistingParameters = new CaeGlobals.OrderedDictionary<string, object>("parameters");
                material.Properties.OfType<Elastic>().Single().YoungsPoissonsTemp[0][0].SetEquation("=500+500");
                bool equationRefused = false;
                try { StaticProjectArchive.Save(equationPath, model); }
                catch (NotSupportedException) { equationRefused = true; }
                check(equationRefused && !File.Exists(equationPath), "numeric persistence refuses equation content instead of silently evaluating it");
            }
            finally { CaeGlobals.MyNCalc.ExistingParameters = parameters; }
            for (int i = 0; i < 23; i++) session.Execute(new(Guid.NewGuid(), session.Revision, "ELASTIC", 3000 + i));
            check(session.Execute(command) == receipt && session.Commands.Length == 25 && !File.Exists(source),
                "operation identity survives eviction of the bounded history snapshots");
            check(!Directory.EnumerateFiles(root, ".edit-*.pma").Any(), "accepted and rejected commands leave no staging files");
        }
        finally { Directory.Delete(root, true); }
    }
}
