using CaeGlobals;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectElementSetChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "element-set parity é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = MultiPartProjectChecks.Create();
            model.Materials["ELASTIC"].AddProperty(new Density([[1, 0]]));
            model.Materials["stiffer"].AddProperty(new Density([[1, 0]]));
            model.Sections["section2"].RegionType = RegionTypeEnum.ElementSetName;
            model.Sections["section2"].RegionName = "SOLID2";
            model.StepCollection.AddStep(new StaticStep("load-2", false));
            foreach (var step in model.StepCollection.StepsList)
            {
                step.AddLoad(new GravityLoad("gravity", "SOLID2", RegionTypeEnum.ElementSetName, 0, -1, 0, false, false, 0));
                step.AddLoad(new CentrifLoad("spin", "SOLID2", RegionTypeEnum.ElementSetName,
                    [0, 0, 0], [0, 0, 1], 1, false, false, false, 0) { Active = false });
                step.AddHistoryOutput(new ElementHistoryOutput("set-history", ElementHistoryVariable.S, "SOLID2", RegionTypeEnum.ElementSetName));
            }
            string source = Path.Combine(root, "source.pma");
            var geometry = new ProjectGeometrySource("retained.step", [1, 2, 3]);
            StaticProjectArchive.Save(source, model, [1], [2], geometrySource: geometry);
            byte[] original = File.ReadAllBytes(source);
            var data = ProjectElementSets.Read(source, "solid2");
            check(data.Elements == 2 && data.SampleElementIds.SequenceEqual(new[] { 1, 2 }) && data.Selected!.Name == "SOLID2",
                "element-set inventory reports exact mesh IDs and canonical selected identity");
            check(data.Sets.Single(set => set.Name == "SOLID2").Dependents.Length == 7 &&
                data.Sets.Single(set => set.Name == "SOLID").MembershipLocked,
                "element-set inventory includes inactive and per-step dependencies and surface membership protection");
            string added = Path.Combine(root, "added.pma");
            ProjectElementSets.AddCopy(source, added, new("authored", "1:2, 1 2"));
            var reopened = StaticProjectArchive.Open(added);
            check(reopened.Model.Mesh.ElementSets["authored"].Labels.SequenceEqual(new[] { 1, 2 }) &&
                !reopened.Model.Mesh.ElementSets["authored"].CreatedFromParts,
                "element-set IDs and inclusive ranges deduplicate and persist");
            check(reopened.Frd == null && reopened.Dat == null && reopened.BinaryResults == null &&
                reopened.GeometrySource!.Data.SequenceEqual(geometry.Data),
                "element-set edits invalidate stale results and preserve retained geometry");
            check(reopened.Model.Mesh.Nodes.Count == 16 && reopened.Model.Mesh.Elements.Count == 2 &&
                reopened.Model.Sections.Keys.SequenceEqual(model.Sections.Keys) && reopened.Model.Mesh.NodeSets.Keys.SequenceEqual(model.Mesh.NodeSets.Keys),
                "element-set creation preserves mesh, sections and node sets");
            var preview = ProjectElementSets.Preview(source, new("preview", "2"));
            check(preview.Nodes.SequenceEqual(Enumerable.Range(9, 8)), "element-set preview resolves owning nodes rather than confusing element IDs with nodes");
            check(ProjectElementSets.Preview(source, new("preview", "1:2:2")).Nodes.SequenceEqual(Enumerable.Range(1, 8)),
                "element-set range increments have inclusive upper bounds without forcing an off-stride endpoint");
            string parts = Path.Combine(root, "parts.pma");
            ProjectElementSets.AddCopy(source, parts, new("part-selection", Parts: model.Mesh.Parts.Keys.Select(name => name.ToUpperInvariant()).ToArray()));
            var partSet = StaticProjectArchive.Open(parts).Model.Mesh.ElementSets["part-selection"];
            check(partSet.CreatedFromParts && partSet.Labels.SequenceEqual(new[] { 1, 17 }) &&
                ProjectElementSets.Read(parts).Sets.Single(set => set.Name == partSet.Name).Elements == 2,
                "whole-part element sets retain sparse part IDs and resolve actual element membership");
            string assigned = Path.Combine(root, "assigned.pma");
            ProjectSections.AddCopy(added, assigned, new("authored-section", "ELASTIC", RegionTypeEnum.ElementSetName, "authored"));
            check(ProjectSections.Read(assigned).Sections.Last().Elements == 2 && ProjectService.Inspect(assigned).CanSolve,
                "an authored element set is immediately usable for a solid section with complete coverage");
            string membership = Path.Combine(root, "membership.pma");
            ProjectElementSets.SaveCopy(assigned, membership, "authored", new("renamed-authored", "2"));
            reopened = StaticProjectArchive.Open(membership);
            check(reopened.Model.Mesh.ElementSets["renamed-authored"].Labels.SequenceEqual(new[] { 2 }) &&
                reopened.Model.Sections["authored-section"].RegionName == "renamed-authored" &&
                reopened.Model.Mesh.ElementSets.Keys.Last() == "renamed-authored",
                "membership edit and rename preserve assignment references and set ordering");
            string renamed = Path.Combine(root, "renamed.pma");
            ProjectElementSets.SaveCopy(source, renamed, "SOLID2", new("renamed-solid"));
            reopened = StaticProjectArchive.Open(renamed);
            check(reopened.Model.Sections["section2"].RegionName == "renamed-solid" &&
                reopened.Model.StepCollection.StepsList.All(step => step.Loads["gravity"].RegionName == "renamed-solid" &&
                    step.Loads["spin"].RegionName == "renamed-solid" && step.HistoryOutputs["set-history"].RegionName == "renamed-solid"),
                "set rename updates sections, loads and history outputs in every step, including inactive definitions");
            string deck = CalculixFileWriter.GetInputText(reopened.Model, ConvertPyramidsToEnum.Wedges);
            check(deck.Contains("*Elset, Elset=renamed-solid") && deck.Contains("*El print, Elset=renamed-solid"),
                "renamed element-set references reach the production CalculiX writers");
            string surfaceRename = Path.Combine(root, "surface-rename.pma");
            ProjectElementSets.SaveCopy(source, surfaceRename, "SOLID", new("surface-backing"));
            reopened = StaticProjectArchive.Open(surfaceRename);
            check(reopened.Model.Mesh.Surfaces["LOADED"].ElementFaces.Values.All(name => name == "surface-backing") &&
                reopened.Model.Mesh.Surfaces["LOADED"].NodeSetName == model.Mesh.Surfaces["LOADED"].NodeSetName &&
                reopened.Model.Sections["solid"].RegionName == "surface-backing",
                "surface-backing set rename updates face references while preserving surface node membership");
            string duplicate = Path.Combine(root, "duplicate.pma");
            ProjectElementSets.DuplicateCopy(parts, duplicate, "part-selection", "part-copy");
            check(StaticProjectArchive.Open(duplicate).Model.Mesh.ElementSets["part-copy"].CreatedFromParts,
                "duplicating a part-derived set preserves its representation");
            string independent = Path.Combine(root, "independent.pma");
            ProjectElementSets.SaveCopy(duplicate, independent, "part-copy", new("part-copy", "1"));
            reopened = StaticProjectArchive.Open(independent);
            check(!reopened.Model.Mesh.ElementSets["part-copy"].CreatedFromParts &&
                reopened.Model.Mesh.ElementSets["part-selection"].Labels.SequenceEqual(new[] { 1, 17 }),
                "part-derived membership can become explicit without modifying the original duplicate source");
            string removed = Path.Combine(root, "removed.pma");
            ProjectElementSets.RemoveCopy(independent, removed, "part-copy");
            check(!StaticProjectArchive.Open(removed).Model.Mesh.ElementSets.ContainsKey("part-copy"),
                "unreferenced element-set removal persists without changing its source");

            string refused = Path.Combine(root, "refused.pma");
            void Reject(Action action, string message)
            {
                bool rejected = false;
                try { action(); } catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException) { rejected = true; }
                check(rejected && !File.Exists(refused), message);
            }
            Reject(() => ProjectElementSets.RemoveCopy(source, refused, "SOLID2"), "referenced element-set removal refuses to cascade through model definitions");
            Reject(() => ProjectElementSets.RemoveCopy(source, refused, "SOLID"), "surface-backing element-set removal is refused");
            Reject(() => ProjectElementSets.SaveCopy(source, refused, "SOLID", new("SOLID", "2")),
                "surface-backing membership edits are refused before publishing inconsistent surface metadata");
            foreach (string ids in new[] { "", "0", "-1", "1::2", "2:1", "1:2:0", "1:2147483647", "2147483648", "99", "1;2", new string('1', 60001) })
                Reject(() => ProjectElementSets.AddCopy(source, refused, new("invalid", ids)), "malformed, absent, unknown or oversized element selection publishes no archive");
            foreach (string name in new[] { "", " bad", "bad\nname", "solid2", model.Mesh.Parts.Keys.First() })
                Reject(() => ProjectElementSets.AddCopy(source, refused, new(name, "1")), "element-set name validation prevents invalid or colliding solver regions");
            Reject(() => ProjectElementSets.AddCopy(source, refused, new("invalid", Parts: [])), "empty part selection is refused");
            Reject(() => ProjectElementSets.AddCopy(source, refused, new("invalid", Parts: ["missing"])), "unknown mesh part selection is refused");
            Reject(() => ProjectElementSets.AddCopy(source, refused, new("invalid", "1", [model.Mesh.Parts.Keys.First()])), "ambiguous membership sources are refused");
            Reject(() => ProjectElementSets.AddCopy(source, refused, new("invalid")), "creation cannot silently infer existing membership");
            Reject(() => ProjectElementSets.SaveCopy(source, refused, "missing", new("invalid", "1")), "editing a missing set is refused");
            Reject(() => ProjectElementSets.DuplicateCopy(source, refused, "SOLID2", "solid"), "duplicate name collision is refused case-insensitively");
            bool overwrite = false;
            try { ProjectElementSets.AddCopy(source, source, new("no-overwrite", "1")); } catch (IOException) { overwrite = true; }
            check(overwrite && File.ReadAllBytes(source).SequenceEqual(original) && Directory.GetFiles(root, ".edit-*").Length == 0,
                "element-set operations preserve original bytes and clean failed staging files");
            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("keywords");
            model.CalculixUserKeywords.Add([0], new CalculixUserKeyword("*Preprint, echo=NO"));
            string keywords = Path.Combine(root, "keywords.pma");
            StaticProjectArchive.Save(keywords, model);
            check(ProjectElementSets.Read(keywords).EditingError != null, "element-set inspection explains untracked user-keyword reference restrictions");
            Reject(() => ProjectElementSets.SaveCopy(keywords, refused, "SOLID2", new("renamed")), "untracked user-keyword references block element-set mutation");
        }
        finally { Directory.Delete(root, true); }
    }
}
