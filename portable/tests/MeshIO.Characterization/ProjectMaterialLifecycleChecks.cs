using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CaeModel;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectMaterialLifecycleChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Directory.CreateTempSubdirectory("material-lifecycle-").FullName;
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            model.Materials["ELASTIC"].AddProperty(new Density([[2.5, 0]]));
            model.Materials["ELASTIC"].AddProperty(new Plastic([[100, 0, 0], [120, .1, 0]]) { Hardening = PlasticHardening.Combined });
            string source = Path.Combine(root, "source.pma");
            StaticProjectArchive.Save(source, model, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures/archive-cube.frd")),
                geometrySource: new ProjectGeometrySource("retained.step", [1, 2, 3]));
            var session = new ProjectCommandSession(root);
            session.Commit(source);
            Guid originalId = session.Materials.Single().Id;
            var create = new MaterialCreateCommand(Guid.NewGuid(), session.Revision, "NEW", 700, .2);
            var receipt = session.Create(create);
            string created = session.ProjectPath!;
            var original = AnalysisArchive.Open(source);
            var after = AnalysisArchive.Open(created);
            var reduced = JsonNode.Parse(after.ModelMetadata!)!;
            var added = reduced["Materials"]![1]!.DeepClone();
            reduced["Materials"]!.AsArray().RemoveAt(1);
            check(JsonNode.DeepEquals(JsonNode.Parse(original.ModelMetadata!), reduced),
                "creation changes only the appended material across the full metadata document");
            check(added["State"]!["Values"]!["Name"]!.GetValue<string>() == "NEW" &&
                added["Properties"]!.AsArray().Count == 1 && added["Properties"]![0]!["Kind"]!.GetValue<string>() == "Elastic" &&
                added["Properties"]![0]!["Data"]![0]![0]!.GetValue<double>() == 700 && added["Properties"]![0]!["Data"]![0]![1]!.GetValue<double>() == .2,
                "created material has exactly the requested elastic property");
            Guid newId = receipt.MaterialId;
            check(newId != Guid.Empty && newId != originalId && session.Materials.Single(item => item.Id == newId).Name == "NEW" &&
                session.Materials.Single(item => item.Id == originalId).Name == "ELASTIC", "creation allocates a new ID without changing existing IDs");
            check(original.ModelMesh!.SequenceEqual(after.ModelMesh!) && original.GeometrySource!.SequenceEqual(after.GeometrySource!) && after.Frd == null,
                "creation retains mesh/geometry and invalidates old results");
            check(receipt.SourceRevision == 1 && receipt.CommittedRevision == 2 && receipt.Kind == "material.elastic.create/1" &&
                receipt.SourceSha256 == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))) &&
                receipt.ResultSha256 == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(created))), "creation receipt binds the source, destination and entity");
            check(session.Create(create) == receipt && session.Revision == 2, "create retry does not allocate a second material or ID");
            session.MoveHistory(false);
            check(session.Materials.Length == 1 && ProjectResults.List(session.ProjectPath!).Length > 0, "undo creation restores results and prunes the new entity");
            check(session.Create(create) == receipt && session.Materials.Length == 1, "create retry after undo does not resurrect the entity");
            session.MoveHistory(true);
            check(session.Materials.Any(item => item.Id == newId), "redo creation restores the same entity ID");

            void Refused(Action action, Type type, string message)
            {
                int revision = session.Revision;
                string current = session.ProjectPath!;
                byte[] bytes = File.ReadAllBytes(current);
                object[] commands = session.Commands;
                var identities = session.Materials;
                bool rejected = false;
                try { action(); }
                catch (Exception error) when (type.IsInstanceOfType(error)) { rejected = true; }
                check(rejected && session.Revision == revision && current == session.ProjectPath && bytes.SequenceEqual(File.ReadAllBytes(current)) &&
                    commands.SequenceEqual(session.Commands) && identities.SequenceEqual(session.Materials), message);
            }
            foreach (string name in new[] { "Bad Name", "Bad#", "123", " NEW ", "", "Missing" })
                Refused(() => session.Create(new(Guid.NewGuid(), session.Revision, name, 700, .2)), typeof(ArgumentException), "malformed creation name is refused without normalization or publication");
            Refused(() => session.Create(new(Guid.NewGuid(), session.Revision, "elastic", 700, .2)), typeof(NotSupportedException), "creation rejects a case-insensitive collision");
            foreach (double e in new[] { 0, -1, double.NaN, double.PositiveInfinity })
                Refused(() => session.Create(new(Guid.NewGuid(), session.Revision, "INVALID", e, .2)), typeof(ArgumentException), "invalid modulus creation is atomic");
            foreach (double nu in new[] { -1, .5, double.NaN, double.NegativeInfinity })
                Refused(() => session.Create(new(Guid.NewGuid(), session.Revision, "INVALID", 700, nu)), typeof(ArgumentException), "invalid Poisson ratio creation is atomic");
            Refused(() => session.Create(create with { OperationId = Guid.NewGuid() }), typeof(ProjectCommandConflictException), "new create at an old revision is rejected");
            Refused(() => session.Create(create with { Name = "CHANGED" }), typeof(ProjectCommandConflictException), "create ID cannot identify a different request");
            Refused(() => session.Remove(new(create.OperationId, create.ExpectedRevision, newId)), typeof(ProjectCommandConflictException), "operation IDs are shared across all command kinds");
            Refused(() => session.Assign(new(Guid.NewGuid(), session.Revision, "missing", newId)), typeof(NotSupportedException), "assignment rejects an unknown section");
            var assigned = session.Assign(new(Guid.NewGuid(), session.Revision, "solid", newId));
            check(assigned.MaterialId == newId && assigned.Kind == "material.assign-solid-section/1" &&
                ProjectMaterials.ListSolidSections(session.ProjectPath!).Single().Material == "NEW", "assignment resolves the material ID to its canonical name");
            check(session.Assign(assigned.Command) == assigned && session.Revision == assigned.CommittedRevision, "assignment retry does not create another revision");
            Refused(() => session.Remove(new(Guid.NewGuid(), session.Revision, newId)), typeof(NotSupportedException), "assigned material removal is rejected without dangling sections");
            session.Assign(new(Guid.NewGuid(), session.Revision, "solid", originalId));
            string beforeRemove = session.ProjectPath!;
            var remove = new MaterialRemoveCommand(Guid.NewGuid(), session.Revision, newId);
            var removed = session.Remove(remove);
            var expectedRemoval = JsonNode.Parse(AnalysisArchive.Open(beforeRemove).ModelMetadata!)!;
            expectedRemoval["Materials"]!.AsArray().RemoveAt(1);
            check(JsonNode.DeepEquals(expectedRemoval, JsonNode.Parse(AnalysisArchive.Open(session.ProjectPath!).ModelMetadata!)) &&
                session.Materials.Length == 1 && session.Materials.Single().Id == originalId, "removal changes exactly the selected unused material and its identity");
            check(removed.MaterialId == newId && removed.MaterialName == "NEW" && removed.Kind == "material.remove-unused/1" && session.Remove(remove) == removed,
                "removal receipt survives deletion of its addressed entity");
            session.MoveHistory(false);
            check(session.Materials.Any(item => item.Id == newId) && session.Remove(remove) == removed && session.Materials.Any(item => item.Id == newId),
                "undo restores the removed ID and a removal retry does not delete it again");
            session.MoveHistory(true);
            var replacement = session.Create(new(Guid.NewGuid(), session.Revision, "NEW", 800, .25));
            check(replacement.MaterialId != newId && session.Create(create) == receipt && session.Remove(remove) == removed &&
                session.Materials.Any(item => item.Id == replacement.MaterialId), "recreating a name allocates a different ID and old retries leave it alone");
            Refused(() => session.Remove(new(Guid.NewGuid(), session.Revision, newId)), typeof(ProjectCommandConflictException), "old deleted ID cannot remove a replacement with the same name");
            Refused(() => session.Remove(new(Guid.NewGuid(), session.Revision, Guid.Empty)), typeof(ArgumentException), "empty removal identity is rejected");
            session.MoveHistory(false);
            session.MoveHistory(false);
            session.Assign(new(Guid.NewGuid(), session.Revision, "solid", newId));
            check(!session.CanRedo && session.Materials.Any(item => item.Id == newId) && session.Remove(remove) == removed &&
                ProjectMaterials.ListSolidSections(session.ProjectPath!).Single().Material == "NEW", "branch assignment preserves original entity identity and cannot replay old removal");

            var failed = new MaterialCreateCommand(Guid.NewGuid(), session.Revision, "RECOVERED", -1, .2);
            Refused(() => session.Create(failed), typeof(ArgumentException), "failed creation does not reserve its operation ID");
            var recovered = session.Create(failed with { YoungsModulus = 100 });
            check(session.Materials.Any(item => item.Id == recovered.MaterialId), "corrected create can use an ID whose previous request failed");
            var current = StaticProjectArchive.Open(session.ProjectPath!);
            current.Model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], FileInOut.Output.Calculix.CalculixUserKeyword>("user keywords");
            current.Model.CalculixUserKeywords.Add([0], new FileInOut.Output.Calculix.CalculixUserKeyword("*Solid section, elset=SOLID, material=RECOVERED"));
            string opaque = Path.Combine(root, "opaque.pma");
            StaticProjectArchive.Save(opaque, current);
            session.Commit(opaque);
            Refused(() => session.Remove(new(Guid.NewGuid(), session.Revision, recovered.MaterialId)), typeof(NotSupportedException),
                "unused removal refuses possible references in raw solver keywords");
            model.Sections["solid"].MaterialName = "elastic";
            string aliased = Path.Combine(root, "aliased.pma");
            StaticProjectArchive.Save(aliased, model);
            session.Commit(aliased);
            Refused(() => session.Remove(new(Guid.NewGuid(), session.Revision, session.Materials.Single().Id)), typeof(NotSupportedException),
                "case-insensitive section references prevent removal");
            check(!Directory.EnumerateFiles(root, ".edit-*.pma").Any(), "material lifecycle failures leave no edit staging files");
        }
        finally { Directory.Delete(root, true); }
    }
}
