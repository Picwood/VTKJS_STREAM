using System.Text;
using System.Text.Json.Nodes;
using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectMaterialIdentityChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Directory.CreateTempSubdirectory("material-identities-").FullName;
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var other = new Material("OTHER");
            other.AddProperty(new Elastic([[500, .1, 0]]));
            model.Materials.Add(other.Name, other);
            string source = Path.Combine(root, "source.pma");
            string frd = Path.Combine(AppContext.BaseDirectory, "fixtures/archive-cube.frd");
            StaticProjectArchive.Save(source, model, File.ReadAllBytes(frd));
            var opened = StaticProjectArchive.Open(source);
            var identities = opened.MaterialIdentities;
            Guid id = identities.Single(item => item.Name == "ELASTIC").Id;
            check(id != Guid.Empty && identities.Select(item => item.Id).Distinct().Count() == 2 &&
                identities.SequenceEqual(StaticProjectArchive.Open(source).MaterialIdentities), "material IDs persist across PMA reopen");
            string legacy = Path.Combine(root, "legacy.pma");
            AnalysisArchive.Save(legacy, AnalysisArchive.Open(source) with { MaterialIdentities = null });
            var migrated = StaticProjectArchive.Open(legacy).MaterialIdentities;
            check(migrated.SequenceEqual(StaticProjectArchive.Open(legacy).MaterialIdentities) && AnalysisArchive.Describe(legacy).Version == 3,
                "legacy archives remain readable with deterministic material identities");
            string upgraded = Path.Combine(root, "upgraded.pma");
            ProjectMaterials.PatchElasticCopy(legacy, upgraded, "ELASTIC", 2000, null);
            check(AnalysisArchive.Describe(upgraded).Version == 7 && migrated.SequenceEqual(StaticProjectArchive.Open(upgraded).MaterialIdentities),
                "first legacy edit stores the same identities in archive v7");

            var session = new ProjectCommandSession(root);
            session.Commit(source);
            var command = new MaterialRenameCommand(Guid.NewGuid(), session.Revision, id, "ALLOY");
            var receipt = session.Rename(command);
            var renamed = StaticProjectArchive.Open(session.ProjectPath!);
            var expected = JsonNode.Parse(AnalysisArchive.Open(source).ModelMetadata!)!;
            expected["Materials"]![0]!["State"]!["Values"]!["Name"] = "ALLOY";
            expected["Sections"]![0]!["Values"]!["MaterialName"] = "ALLOY";
            check(JsonNode.DeepEquals(expected, JsonNode.Parse(AnalysisArchive.Open(session.ProjectPath!).ModelMetadata!)) &&
                renamed.MaterialIdentities.Single(item => item.Id == id).Name == "ALLOY" && renamed.Frd == null,
                "rename updates exactly the material name and section references, preserves its ID and invalidates results");
            check(receipt.MaterialId == id && receipt.PreviousName == "ELASTIC" && receipt.CommittedRevision == 2 &&
                session.Rename(command) == receipt && session.Revision == 2, "rename receipt binds the entity and makes retries idempotent");
            session.MoveHistory(false);
            check(session.Materials.Single(item => item.Id == id).Name == "ELASTIC" && ProjectResults.List(session.ProjectPath!).Length > 0,
                "undo restores identity, name, section references and results");
            check(session.Rename(command) == receipt && session.Revision == 3 && session.Materials.Single(item => item.Id == id).Name == "ELASTIC",
                "rename retry after undo does not redo it");
            session.MoveHistory(true);
            session.Rename(new(Guid.NewGuid(), session.Revision, id, "alloy"));
            check(session.Materials.Single(item => item.Id == id).Name == "alloy" && ProjectMaterials.ListSolidSections(session.ProjectPath!).Single().Material == "alloy",
                "case-only rename retains ID and canonical section references");
            var patch = session.Execute(new(Guid.NewGuid(), session.Revision, MaterialId: id, YoungsModulus: 2300));
            check(patch.MaterialId == id && ProjectMaterials.List(session.ProjectPath!).Single(item => item.Name == "alloy").YoungsModulus == 2300,
                "elastic patches address the same entity after rename without using a display name");

            void Refused(Action action, Type type, string message)
            {
                int revision = session.Revision;
                byte[] bytes = File.ReadAllBytes(session.ProjectPath!);
                object[] commands = session.Commands;
                var ids = session.Materials;
                bool rejected = false;
                try { action(); }
                catch (Exception error) when (type.IsInstanceOfType(error)) { rejected = true; }
                check(rejected && revision == session.Revision && bytes.SequenceEqual(File.ReadAllBytes(session.ProjectPath!)) &&
                    commands.SequenceEqual(session.Commands) && ids.SequenceEqual(session.Materials), message);
            }
            foreach (string name in new[] { "OTHER", "other", "Bad Name", "Bad#", "123", " ALLOY ", "alloy" })
                Refused(() => session.Rename(new(Guid.NewGuid(), session.Revision, id, name)), typeof(ArgumentException), "invalid/colliding rename is atomic");
            Refused(() => session.Rename(command with { NewName = "CHANGED" }), typeof(ProjectCommandConflictException), "rename ID cannot be reused for a different payload");
            Refused(() => session.Execute(new(command.OperationId, command.ExpectedRevision, "alloy", 4000)), typeof(ProjectCommandConflictException), "operation IDs cannot be reused across command kinds");
            Refused(() => session.Execute(new(Guid.NewGuid(), session.Revision, "ELASTIC", 2400, MaterialId: id)), typeof(ProjectCommandConflictException), "mismatching ID and old display name cannot silently target another material");
            Refused(() => session.Rename(new(Guid.NewGuid(), 1, id, "STALE")), typeof(ProjectCommandConflictException), "new stale rename is rejected");

            Guid removedId = session.Materials.Single(item => item.Name == "OTHER").Id;
            string removed = Path.Combine(root, "removed.pma");
            ProjectMaterials.RemoveUnusedCopy(session.ProjectPath!, removed, "OTHER");
            session.Commit(removed);
            check(session.Materials.Length == 1 && session.Materials.Single().Id == id, "removal prunes only the deleted identity");
            string added = Path.Combine(root, "added.pma");
            ProjectMaterials.AddElasticCopy(session.ProjectPath!, added, "OTHER", 600, .2);
            session.Commit(added);
            check(session.Materials.Single(item => item.Name == "OTHER").Id != removedId, "recreating a name gets a new identity");
            Refused(() => session.Execute(new(Guid.NewGuid(), session.Revision, MaterialId: removedId, YoungsModulus: 900)), typeof(ProjectCommandConflictException), "deleted identity cannot target the replacement with the same name");
            var currentIds = session.Materials;
            string attached = Path.Combine(root, "attached.pma");
            ProjectExternalResults.AttachCopy(session.ProjectPath!, attached, frd);
            session.Commit(attached);
            check(currentIds.SequenceEqual(session.Materials), "external result attachment preserves material identities");
            string nodes = Path.Combine(root, "nodes.pma");
            ProjectNodeSets.AddCopy(session.ProjectPath!, nodes, "IDENTITY_CHECK", [1, 2]);
            session.Commit(nodes);
            check(currentIds.SequenceEqual(session.Materials), "unrelated node-set edit preserves material identities");
            session.MoveHistory(false);
            session.MoveHistory(false);
            session.MoveHistory(false);
            session.MoveHistory(false);
            check(session.Materials.Single(item => item.Id == removedId).Name == "OTHER", "undo removal restores the original entity ID");
            session.Rename(new(Guid.NewGuid(), session.Revision, id, "BRANCHED"));
            check(!session.CanRedo && session.Rename(command) == receipt && session.Materials.Single(item => item.Id == id).Name == "BRANCHED",
                "branching retains entity IDs and old receipts without resurrecting discarded redo");

            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], FileInOut.Output.Calculix.CalculixUserKeyword>("user keywords");
            model.CalculixUserKeywords.Add([0], new FileInOut.Output.Calculix.CalculixUserKeyword("*Solid section, elset=SOLID, material=ELASTIC"));
            string opaque = Path.Combine(root, "opaque.pma");
            StaticProjectArchive.Save(opaque, model);
            session.Commit(opaque);
            Guid opaqueId = session.Materials.Single(item => item.Name == "ELASTIC").Id;
            Refused(() => session.Rename(new(Guid.NewGuid(), session.Revision, opaqueId, "RENAMED")), typeof(NotSupportedException),
                "rename refuses possible references in opaque solver keywords before publication");

            var valid = AnalysisArchive.Open(source);
            foreach (Action<JsonNode> mutate in new Action<JsonNode>[] {
                json => json["Version"] = 99,
                json => json["Materials"]![0]!["Id"] = Guid.Empty.ToString(),
                json => json["Materials"]![1]!["Id"] = json["Materials"]![0]!["Id"]!.DeepClone(),
                json => json["Materials"]![0]!["Name"] = "MISSING",
                json => json["Materials"]!.AsArray().RemoveAt(0),
                json => json["Unknown"] = true })
            {
                var json = JsonNode.Parse(valid.MaterialIdentities!)!;
                mutate(json);
                string bad = Path.Combine(root, Guid.NewGuid() + ".pma");
                AnalysisArchive.Save(bad, valid with { MaterialIdentities = Encoding.UTF8.GetBytes(json.ToJsonString()) });
                Refused(() => session.Commit(bad), typeof(InvalidDataException), "checksummed but invalid identity payload cannot replace the open project");
                bool rejected = false;
                try { StaticProjectArchive.OpenModelForScene(bad); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, "scene path also validates material identity bindings");
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
