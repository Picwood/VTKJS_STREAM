using System.Security.Cryptography;
using System.Text;
using CaeGlobals;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectStepChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "static step authoring é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source.pma");
            var model = new StaticModelWorkflow(1).Model;
            var original = (StaticStep)model.StepCollection.StepsList.Single();
            original.SolverType = SolverTypeEnum.Spooles; original.OutputFrequency = 2;
            byte[] geometryBytes = Encoding.UTF8.GetBytes("retained source for step workflow");
            var topology = CadTopologyCodec.ReadGmsh(Encoding.UTF8.GetBytes(CadTopologyChecks.Msh), geometryBytes);
            var geometry = new ProjectGeometrySource("source.step", geometryBytes, topology);
            byte[] frd = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "archive-cube.frd"));
            StaticProjectArchive.Save(source, model, frd, geometrySource: geometry);
            byte[] before = SHA256.HashData(File.ReadAllBytes(source));
            var summary = ProjectSteps.List(source).Single();
            check(summary.Name == "load-1" && summary.Loads == original.Loads.Count && summary.HistoryOutputs == 1,
                "static step inspection exposes sequence, controls and child-definition counts");
            var settings = new StaticStepSettings("load-2", true, IncrementationTypeEnum.Automatic, 2, .5, 1e-5, 1, 200);
            string added = Path.Combine(root, "added.pma");
            ProjectSteps.AddCopy(source, added, settings);
            var project = StaticProjectArchive.Open(added);
            var first = (StaticStep)project.Model.StepCollection.StepsList[0];
            var second = (StaticStep)project.Model.StepCollection.StepsList[1];
            check(second.Name == "load-2" && second.BoundaryConditions.Count == original.BoundaryConditions.Count &&
                  second.Loads.Count == original.Loads.Count && second.Nlgeom && second.TimePeriod == 2 && second.InitialTimeIncrement == .5,
                "adding a static step copies the prior supports/loads and applies validated controls");
            check(second.FieldOutputs.Count == 2 && second.HistoryOutputs.Count == 0 &&
                  ((NodalFieldOutput)second.FieldOutputs["NF-Output-1"]).Variables == (NodalFieldVariable.U | NodalFieldVariable.RF) &&
                  ((ElementFieldOutput)second.FieldOutputs["EF-Output-1"]).Variables == (ElementFieldVariable.E | ElementFieldVariable.S),
                "new step default outputs match the original StaticStep constructor without copying prior output requests");
            check(project.Frd == null && project.Dat == null && project.BinaryResults == null &&
                  project.GeometrySource!.Data.SequenceEqual(geometryBytes) &&
                  CadTopologyCodec.Serialize(project.GeometrySource.Topology!, geometryBytes).SequenceEqual(CadTopologyCodec.Serialize(topology, geometryBytes)),
                "step edits invalidate old results while retaining exact CAD source and topology");
            var copied = StaticModelMetadata.CopyStaticStep(original);
            ((STLoad)copied.Loads["force"]).F1.SetEquationFromValue(9);
            var copiedBc = copied.BoundaryConditions.Values.OfType<DisplacementRotation>().First();
            copiedBc.U1.SetEquationFromValue(.25);
            check(((STLoad)original.Loads["force"]).F1.Value != 9 &&
                  original.BoundaryConditions.Values.OfType<DisplacementRotation>().First().U1.Value != .25 &&
                  !ReferenceEquals(copied.FieldOutputs.Values.First(), original.FieldOutputs.Values.First()) &&
                  !ReferenceEquals(copied.HistoryOutputs.Values.First(), original.HistoryOutputs.Values.First()),
                "typed step copy independently owns equations, loads, supports and output requests");
            // The shared codec must cover every currently editable mechanical load kind.
            copied.AddLoad(new CLoad("nodal", "RIGHT", RegionTypeEnum.NodeSetName, 1, 2, 3, false, false, 0));
            copied.AddLoad(new DLoad("pressure", "LOADED", RegionTypeEnum.SurfaceName, 2, false, false, 0));
            copied.AddLoad(new GravityLoad("gravity", "ALL", RegionTypeEnum.ElementSetName, false, false, 0));
            copied.AddLoad(new CentrifLoad("spin", "ALL", RegionTypeEnum.ElementSetName, false, false, false, 0));
            copied.AddBoundaryCondition(new FixedBC("fixed-copy", "LEFT", RegionTypeEnum.NodeSetName, false));
            copied.AddFieldOutput(new ContactFieldOutput("contact-copy", ContactFieldVariable.CDIS));
            var broadCopy = StaticModelMetadata.CopyStaticStep(copied);
            check(broadCopy.Loads.Values.Select(load => load.GetType()).SequenceEqual(copied.Loads.Values.Select(load => load.GetType())) &&
                  broadCopy.FieldOutputs.Values.Last() is ContactFieldOutput && broadCopy.BoundaryConditions.Values.Last() is FixedBC,
                "typed step cloning covers force, traction, pressure, gravity, centrifugal, fixed and contact-output definitions");
            string fresh = Path.Combine(root, "fresh.pma");
            ProjectSteps.AddCopy(source, fresh, new("fresh"), copyPrevious: false);
            var freshStep = StaticProjectArchive.Open(fresh).Model.StepCollection.StepsList.Last();
            check(freshStep.BoundaryConditions.Count == 0 && freshStep.Loads.Count == 0 && freshStep.FieldOutputs.Count == 2,
                "static step creation can omit prior supports and loads");
            string duplicate = Path.Combine(root, "duplicate.pma");
            ProjectSteps.DuplicateCopy(source, duplicate, "load-1", "load-copy");
            var duplicates = StaticProjectArchive.Open(duplicate).Model.StepCollection.StepsList;
            check(duplicates.Count == 2 && duplicates[1].Name == "load-copy" && duplicates[1].HistoryOutputs.Count == 1 &&
                  duplicates[1].SolverType == original.SolverType && duplicates[1].OutputFrequency == original.OutputFrequency,
                "duplicate appends a full independent step including output requests and solver settings");
            string edited = Path.Combine(root, "edited.pma");
            ProjectSteps.SaveCopy(duplicate, edited, "load-1", settings with { Name = "renamed", IncrementationType = IncrementationTypeEnum.Direct });
            var editedSteps = StaticProjectArchive.Open(edited).Model.StepCollection.StepsList;
            var editedFirst = (StaticStep)editedSteps[0];
            check(editedFirst.Name == "renamed" && editedFirst.Direct && editedFirst.IncrementationType == IncrementationTypeEnum.Direct &&
                  editedFirst.SolverType == SolverTypeEnum.Spooles && editedFirst.OutputFrequency == 2 && editedFirst.HistoryOutputs.Count == 1 &&
                  editedSteps[1].Name == "load-copy" && ((StaticStep)editedSteps[1]).TimePeriod == 1,
                "step editing renames in place, synchronizes direct mode and preserves unrelated settings and later steps");
            string input = Path.Combine(root, "edited.inp"); ProjectService.Export(edited, input);
            check(File.ReadAllText(input).Contains("*Static, Solver=Spooles, Direct") && File.ReadAllText(input).Contains("*Step, Nlgeom"),
                "edited static controls reach production CalculiX keywords");
            string disabled = Path.Combine(root, "disabled.pma");
            ProjectSteps.SetActiveCopy(source, disabled, "load-1", false);
            check(!ProjectSteps.List(disabled).Single().Active && ProjectService.Inspect(disabled).ActiveAnalysisSteps == 0,
                "deactivation persists and exposes that the project has no runnable step");
            string jobRoot = Path.Combine(root, "jobs"); bool noRun = false;
            try { ProjectService.PrepareSolve(disabled, jobRoot, Environment.ProcessPath); }
            catch (NotSupportedException) { noRun = true; }
            check(noRun && !Directory.Exists(jobRoot), "no-active-step solve is rejected before creating job files or resolving a solver");
            string enabled = Path.Combine(root, "enabled.pma"); ProjectSteps.SetActiveCopy(disabled, enabled, "load-1", true);
            check(ProjectService.Inspect(enabled).ActiveAnalysisSteps == 1, "reactivation restores the runnable-step count");
            string removed = Path.Combine(root, "removed.pma"); ProjectSteps.RemoveCopy(duplicate, removed, "load-1");
            check(ProjectSteps.List(removed).Single().Name == "load-copy" &&
                  StaticProjectArchive.Open(removed).Model.Mesh.NodeSets.Keys.SequenceEqual(model.Mesh.NodeSets.Keys),
                "step removal preserves other steps and their shared named mesh regions");
            string empty = Path.Combine(root, "empty.pma"); ProjectSteps.RemoveCopy(source, empty, "load-1");
            check(ProjectSteps.List(empty).Length == 0 && ProjectService.Inspect(empty).ActiveAnalysisSteps == 0,
                "removing the last step preserves an editable model with an empty step collection");
            string restarted = Path.Combine(root, "restarted.pma"); ProjectSteps.AddCopy(empty, restarted, new("restart"));
            check(ProjectSteps.List(restarted).Single() is { Name: "restart", Loads: 0, Boundaries: 0, FieldOutputs: 2 },
                "a new static sequence can start after removing all steps");
            foreach (var invalid in new[]
            {
                settings with { Name = " " }, settings with { Name = "LOAD-1" }, settings with { Name = "bad\nname" },
                settings with { TimePeriod = double.NaN }, settings with { InitialTimeIncrement = 3 },
                settings with { MinTimeIncrement = 1 }, settings with { MaxTimeIncrement = .25 },
                settings with { MaxIncrements = 0 }, settings with { MaxIncrements = 1000001 },
                settings with { IncrementationType = (IncrementationTypeEnum)99 }
            })
            {
                string rejectedPath = Path.Combine(root, "bad.pma"); bool rejected = false;
                try { ProjectSteps.AddCopy(source, rejectedPath, invalid); }
                catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(rejectedPath), "invalid or duplicate static settings fail without publishing a project");
            }
            foreach (Action action in new Action[]
            {
                () => ProjectSteps.SaveCopy(duplicate, Path.Combine(root, "bad.pma"), "load-1", new("LOAD-COPY")),
                () => ProjectSteps.DuplicateCopy(source, Path.Combine(root, "bad.pma"), "load-1", "LOAD-1"),
                () => ProjectSteps.RemoveCopy(source, Path.Combine(root, "bad.pma"), "missing"),
                () => ProjectSteps.SetActiveCopy(source, Path.Combine(root, "bad.pma"), "missing", false)
            })
            {
                bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "bad.pma")), "step operations reject conflicting names or missing selections");
            }
            bool overwrite = false;
            try { ProjectSteps.RemoveCopy(source, source, "load-1"); }
            catch (IOException) { overwrite = true; }
            check(overwrite && SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(before),
                "step operations refuse destination overwrite and preserve the source archive exactly");
            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("user keywords");
            model.CalculixUserKeywords.Add(new[] { 0 }, new CalculixUserKeyword("*Preprint, echo=NO"));
            string keywords = Path.Combine(root, "keywords.pma"); StaticProjectArchive.Save(keywords, model);
            bool keywordRejected = false;
            try { ProjectSteps.AddCopy(keywords, Path.Combine(root, "bad.pma"), new("next")); }
            catch (NotSupportedException) { keywordRejected = true; }
            check(keywordRejected && ProjectSteps.Read(keywords).EditingError != null && ProjectSteps.List(keywords).Length == 1,
                "step edits fail closed and remain inspectable when positional user keywords need insertion-path remapping");
            check(!Directory.EnumerateFiles(root, ".edit-*.pma").Any(), "step save/failure paths leave no staging archives");
        }
        finally { Directory.Delete(root, true); }
    }
}
