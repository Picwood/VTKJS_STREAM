using System.Security.Cryptography;
using System.Text;
using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectSectionChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "solid section parity é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = MultiPartProjectChecks.Create();
            byte[] bytes = Encoding.UTF8.GetBytes("retained source for section editing");
            var topology = CadTopologyCodec.ReadGmsh(Encoding.UTF8.GetBytes(CadTopologyChecks.Msh), bytes);
            var geometry = new ProjectGeometrySource("solid.step", bytes, topology);
            string source = Path.Combine(root, "source.pma");
            byte[] frd = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "archive-cube.frd"));
            StaticProjectArchive.Save(source, model, frd, geometrySource: geometry);
            byte[] before = SHA256.HashData(File.ReadAllBytes(source));
            var data = ProjectSections.Read(source);
            check(data.Coverage is { ActiveElements: 2, UnassignedElements: 0, OverlappingElements: 0, CanSolve: true } &&
                  data.Sections.All(section => section.EffectiveElements == 1),
                "section inspection reports effective assignments and complete active-element coverage");
            check(data.Regions.Single(region => region.Name == "ALL_PARTS").Elements == 2 &&
                  data.Regions.Any(region => region.Name == "second_block" && region.RegionType == "PartName"),
                "solid region targets expand part-derived sets using sparse part IDs");
            string added = Path.Combine(root, "added.pma");
            ProjectSections.AddCopy(source, added, new("override", "stiffer", RegionTypeEnum.ElementSetName, "ALL_PARTS"));
            var addedData = ProjectSections.Read(added);
            check(addedData.Coverage.OverlappingElements == 2 && addedData.Coverage.CanSolve &&
                  addedData.Sections.Last().EffectiveElements == 2 && addedData.Sections.Take(2).All(section => section.EffectiveElements == 0),
                "later active section overrides earlier overlapping element and part assignments");
            var reopened = StaticProjectArchive.Open(added);
            check(reopened.Frd == null && reopened.Dat == null && reopened.BinaryResults == null &&
                  reopened.GeometrySource!.Data.SequenceEqual(bytes) &&
                  CadTopologyCodec.Serialize(reopened.GeometrySource.Topology!, bytes).SequenceEqual(CadTopologyCodec.Serialize(topology, bytes)),
                "section edits discard stale results and preserve exact source-bound topology");
            string edited = Path.Combine(root, "edited.pma");
            ProjectSections.SaveCopy(added, edited, "override", new("renamed", "ELASTIC", RegionTypeEnum.PartName, "second_block"));
            var editedModel = StaticProjectArchive.Open(edited).Model;
            check(editedModel.Sections.Keys.SequenceEqual(new[] { "solid", "section2", "renamed" }) &&
                  editedModel.Sections["renamed"].MaterialName == "ELASTIC" && editedModel.Sections["renamed"].RegionName == "second_block",
                "section editing changes name, material and region while retaining list order");
            check(CalculixText(editedModel).Contains("*Solid section, Elset=second_block, Material=ELASTIC"),
                "section edits reach the original production solid-section keyword writer");
            string duplicate = Path.Combine(root, "duplicate.pma");
            ProjectSections.DuplicateCopy(source, duplicate, "section2", "copy");
            var duplicated = ProjectSections.Read(duplicate);
            check(duplicated.Sections.Last() is { Name: "copy", Material: "stiffer", Region: "second_block", Active: true, EffectiveElements: 1 } &&
                  duplicated.Coverage.OverlappingElements == 1, "duplicate appends an independently named section with original assignment and state");
            string inactive = Path.Combine(root, "inactive.pma");
            ProjectSections.SetActiveCopy(duplicate, inactive, "copy", false);
            check(ProjectSections.Read(inactive).Coverage.OverlappingElements == 0 &&
                  ProjectSections.Read(inactive).Sections.Last().EffectiveElements == 0,
                "deactivation restores the earlier active section assignment");
            string active = Path.Combine(root, "active.pma"); ProjectSections.SetActiveCopy(inactive, active, "copy", true);
            check(ProjectSections.Read(active).Coverage.OverlappingElements == 1, "reactivation restores the later section override");
            string removed = Path.Combine(root, "removed.pma"); ProjectSections.RemoveCopy(source, removed, "section2");
            check(ProjectSections.Read(removed).Coverage.UnassignedElements == 1 && !ProjectService.Inspect(removed).CanSolve,
                "section removal persists an incomplete model and reports uncovered active elements");
            var incomplete = StaticProjectArchive.Open(removed).Model;
            check(incomplete.Materials.Keys.SequenceEqual(model.Materials.Keys) &&
                  incomplete.Mesh.ElementSets.Keys.SequenceEqual(model.Mesh.ElementSets.Keys) &&
                  incomplete.StepCollection.StepsList.Single().Loads.Count == model.StepCollection.StepsList.Single().Loads.Count,
                "section removal retains shared materials, regions, loads and analysis definitions");
            bool refusedSolve = false;
            try { ProjectService.PrepareSolve(removed, Path.Combine(root, "jobs"), Environment.ProcessPath); }
            catch (NotSupportedException) { refusedSolve = true; }
            check(refusedSolve && !Directory.Exists(Path.Combine(root, "jobs")),
                "incomplete section coverage prevents job creation and solver launch");
            string recovered = Path.Combine(root, "recovered.pma");
            ProjectSections.AddCopy(removed, recovered, new("restored", "stiffer", RegionTypeEnum.PartName, "second_block"));
            check(ProjectService.Inspect(recovered).CanSolve, "recreating a missing section restores solve readiness");
            string empty = Path.Combine(root, "empty.pma"); ProjectSections.RemoveCopy(removed, empty, "solid");
            check(ProjectSections.Read(empty).Sections.Length == 0 && ProjectSections.Read(empty).Coverage.UnassignedElements == 2,
                "removing all sections leaves an editable model with explicit assignment gaps");
            string noMaterials = Path.Combine(root, "no-materials.pma");
            var emptyModel = StaticProjectArchive.Open(empty).Model; emptyModel.Materials.Clear();
            StaticProjectArchive.Save(noMaterials, emptyModel, geometrySource: geometry);
            string standalone = Path.Combine(root, "new-material.pma");
            ProjectMaterials.AddElasticCopy(noMaterials, standalone, "created", 3000, 0);
            check(ProjectSections.Read(standalone).Materials.Single().Name == "created" && ProjectSections.Read(standalone).Sections.Length == 0,
                "elastic material creation works without any pre-existing section or material");
            string allAssigned = Path.Combine(root, "all-assigned.pma");
            ProjectSections.AddCopy(standalone, allAssigned, new("all", "created", RegionTypeEnum.ElementSetName, "ALL_PARTS"));
            check(ProjectService.Inspect(allAssigned).CanSolve && ProjectSections.Read(allAssigned).Sections.Single().Elements == 2,
                "a part-derived element-set section can restore the entire empty assignment workflow");
            // Inactive mesh parts do not require section coverage.
            var partial = StaticProjectArchive.Open(removed);
            partial.Model.Mesh.Parts["second_block"].Active = false;
            check(ProjectSections.Coverage(partial.Model) is { ActiveElements: 1, UnassignedElements: 0, CanSolve: true },
                "inactive mesh parts are excluded from required section coverage");
            partial.Model.Materials["ELASTIC"].Active = false;
            check(ProjectSections.Coverage(partial.Model) is { InvalidMaterials: 1, CanSolve: false },
                "an inactive assigned material prevents solve readiness");
            partial.Model.Mesh.Parts["second_block"].Active = true;
            string materialInactive = Path.Combine(root, "material-inactive.pma");
            StaticProjectArchive.Save(materialInactive, partial.Model);
            check(!ProjectService.Inspect(materialInactive).CanSolve, "inactive material readiness persists through project reopen");
            var bad = StaticProjectArchive.Open(source).Model;
            bad.Sections["solid"].RegionName = "missing";
            check(ProjectSections.Coverage(bad).InvalidRegions == 1, "coverage flags missing named section regions");
            foreach (var invalid in new[]
            {
                new SolidSectionSettings("SOLID", "ELASTIC", RegionTypeEnum.ElementSetName, "SOLID"),
                new SolidSectionSettings(" ", "ELASTIC", RegionTypeEnum.ElementSetName, "SOLID"),
                new SolidSectionSettings("bad\nname", "ELASTIC", RegionTypeEnum.ElementSetName, "SOLID"),
                new SolidSectionSettings("new", "missing", RegionTypeEnum.ElementSetName, "SOLID"),
                new SolidSectionSettings("new", "ELASTIC", RegionTypeEnum.ElementSetName, "missing"),
                new SolidSectionSettings("new", "ELASTIC", RegionTypeEnum.NodeSetName, "RIGHT")
            })
            {
                bool rejected = false;
                try { ProjectSections.AddCopy(source, Path.Combine(root, "bad.pma"), invalid); }
                catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "bad.pma")), "invalid section names, materials and incompatible regions fail before publication");
            }
            bool renameConflict = false;
            try { ProjectSections.SaveCopy(source, Path.Combine(root, "bad.pma"), "solid", new("SECTION2", "ELASTIC", RegionTypeEnum.ElementSetName, "SOLID")); }
            catch (ArgumentException) { renameConflict = true; }
            check(renameConflict, "section rename refuses case-insensitive collisions");
            bool overwrite = false;
            try { ProjectSections.RemoveCopy(source, source, "solid"); }
            catch (IOException) { overwrite = true; }
            check(overwrite && SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(before),
                "section operations refuse overwrite and preserve original project bytes");
            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("keywords");
            model.CalculixUserKeywords.Add(new[] { 0 }, new CalculixUserKeyword("*Preprint, echo=NO"));
            string keywords = Path.Combine(root, "keywords.pma"); StaticProjectArchive.Save(keywords, model);
            bool keywordRefused = false;
            try { ProjectSections.RemoveCopy(keywords, Path.Combine(root, "bad.pma"), "solid"); }
            catch (NotSupportedException) { keywordRefused = true; }
            check(keywordRefused && ProjectSections.Read(keywords).EditingError != null,
                "section edits with positional user keywords stay inspectable but fail closed");
            check(!Directory.EnumerateFiles(root, ".edit-*.pma").Any(), "section edit and failure paths leave no staging projects");
        }
        finally { Directory.Delete(root, true); }
    }

    private static string CalculixText(FeModel model) =>
        FileInOut.Output.CalculixFileWriter.GetInputText(model, ConvertPyramidsToEnum.Wedges);
}
