using System.Security.Cryptography;
using System.Text;
using CaeGlobals;
using CaeModel;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectFieldOutputChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "field output parity é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(2).Model;
            foreach (var step in model.StepCollection.StepsList.OfType<StaticStep>())
                step.AddFieldOutput(new ContactFieldOutput("contact", ContactFieldVariable.CDIS | ContactFieldVariable.CSTR));
            byte[] sourceBytes = Encoding.UTF8.GetBytes("field output retained geometry");
            var topology = CadTopologyCodec.ReadGmsh(Encoding.UTF8.GetBytes(CadTopologyChecks.Msh), sourceBytes);
            var geometry = new ProjectGeometrySource("field.step", sourceBytes, topology);
            byte[] frd = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "archive-cube.frd"));
            string source = Path.Combine(root, "source.pma");
            StaticProjectArchive.Save(source, model, frd, geometrySource: geometry);
            byte[] before = SHA256.HashData(File.ReadAllBytes(source));

            var data = ProjectFieldOutputs.Read(source);
            check(data.Steps.SequenceEqual(new[] { "load-1", "load-2" }) && data.Outputs.Length == 6 &&
                  data.VariableChoices["Nodal"].Contains("RF") && data.VariableChoices["Element"].Contains("SDV") &&
                  data.VariableChoices["Contact"].Contains("CSTR"),
                "field-output inspection exposes ordered steps, requests and valid variable choices");

            string added = Path.Combine(root, "added.pma");
            ProjectFieldOutputs.AddCopy(source, added, "load-1", new("forces", FieldOutputKind.Nodal,
                ["RF", "U"], LastIterations: true, ContactElements: true, Global: false));
            var addedProject = StaticProjectArchive.Open(added);
            var addedOutput = (NodalFieldOutput)((StaticStep)addedProject.Model.StepCollection.StepsList[0]).FieldOutputs["forces"];
            check(addedOutput.Variables == (NodalFieldVariable.RF | NodalFieldVariable.U) && addedOutput.LastIterations &&
                  addedOutput.ContactElements && !addedOutput.Global,
                "nodal field-output add persists variables and advanced file-output flags");
            check(addedProject.Frd == null && addedProject.Dat == null && addedProject.BinaryResults == null &&
                  addedProject.GeometrySource!.Data.SequenceEqual(sourceBytes) &&
                  CadTopologyCodec.Serialize(addedProject.GeometrySource.Topology!, sourceBytes)
                      .SequenceEqual(CadTopologyCodec.Serialize(topology, sourceBytes)),
                "field-output edits discard stale results and preserve exact source-bound topology");
            string contactAdded = Path.Combine(root, "contact-added.pma");
            ProjectFieldOutputs.AddCopy(added, contactAdded, "load-1", new("contact-extra", FieldOutputKind.Contact,
                ["CELS", "PCON"], LastIterations: true, ContactElements: true));
            var contactSummary = ProjectFieldOutputs.Read(contactAdded).Outputs.Single(output => output.Name == "contact-extra");
            check(contactSummary is { Kind: "Contact", LastIterations: true, ContactElements: true } &&
                  contactSummary.Variables.SequenceEqual(new[] { "CELS", "PCON" }) &&
                  FileInOut.Output.CalculixFileWriter.GetInputText(StaticProjectArchive.Open(contactAdded).Model,
                      FileInOut.Output.Calculix.ConvertPyramidsToEnum.Wedges).Contains("*Contact file, Last iterations, Contact elements"),
                "contact field-output creation reaches the production contact writer");

            string edited = Path.Combine(root, "edited.pma");
            ProjectFieldOutputs.SaveCopy(added, edited, "load-1", "stress-strain", new("energy", FieldOutputKind.Element,
                ["S", "ENER"], ElementOutput: ElementFieldOutputOutputEnum.ThreeD));
            var editedStep = (StaticStep)StaticProjectArchive.Open(edited).Model.StepCollection.StepsList[0];
            check(editedStep.FieldOutputs.Keys.SequenceEqual(new[] { "displacement", "energy", "contact", "forces" }) &&
                  ((ElementFieldOutput)editedStep.FieldOutputs["energy"]).Output == ElementFieldOutputOutputEnum.ThreeD,
                "field-output edit renames in place and persists element output mode");
            string deck = FileInOut.Output.CalculixFileWriter.GetInputText(StaticProjectArchive.Open(edited).Model,
                FileInOut.Output.Calculix.ConvertPyramidsToEnum.Wedges);
            check(deck.Contains("*El file, Output=3D") && deck.Contains("S, ENER, NOE") &&
                  deck.Contains("*Node file, Last iterations, Contact elements, Global=No"),
                "field-output edits reach production CalculiX keywords and variable data");

            string duplicate = Path.Combine(root, "duplicate.pma");
            ProjectFieldOutputs.DuplicateCopy(edited, duplicate, "load-1", "forces", "forces-copy");
            var duplicated = ProjectFieldOutputs.Read(duplicate).Outputs.Where(output => output.Step == "load-1").ToArray();
            check(duplicated.Last() is { Name: "forces-copy", Kind: "Nodal", LastIterations: true, ContactElements: true, Global: false } &&
                  duplicated.Last().Variables.SequenceEqual(new[] { "RF", "U" }),
                "field-output duplicate appends an independent request with all settings");
            string inactive = Path.Combine(root, "inactive.pma");
            ProjectFieldOutputs.SetActiveCopy(duplicate, inactive, "load-1", "forces-copy", false);
            check(!ProjectFieldOutputs.Read(inactive).Outputs.Single(output => output.Name == "forces-copy").Active,
                "field-output deactivation persists");
            string active = Path.Combine(root, "active.pma");
            ProjectFieldOutputs.SetActiveCopy(inactive, active, "load-1", "forces-copy", true);
            check(ProjectFieldOutputs.Read(active).Outputs.Single(output => output.Name == "forces-copy").Active,
                "field-output reactivation persists");

            string propagated = Path.Combine(root, "propagated.pma");
            ProjectFieldOutputs.PropagateCopy(edited, propagated, "load-1", "contact");
            var propagatedSteps = StaticProjectArchive.Open(propagated).Model.StepCollection.StepsList.OfType<StaticStep>().ToArray();
            check(propagatedSteps[1].FieldOutputs.Keys.SequenceEqual(new[] { "displacement", "stress-strain", "contact" }) &&
                  !ReferenceEquals(propagatedSteps[0].FieldOutputs["contact"], propagatedSteps[1].FieldOutputs["contact"]),
                "propagation replaces the same named request in later steps with independent copies in place");
            string propagatedNew = Path.Combine(root, "propagated-new.pma");
            ProjectFieldOutputs.PropagateCopy(edited, propagatedNew, "load-1", "forces");
            check(((StaticStep)StaticProjectArchive.Open(propagatedNew).Model.StepCollection.StepsList[1]).FieldOutputs.Keys.Last() == "forces",
                "propagation appends a missing request to each later step");

            string removed = Path.Combine(root, "removed.pma");
            ProjectFieldOutputs.RemoveCopy(active, removed, "load-1", "forces-copy");
            check(!ProjectFieldOutputs.Read(removed).Outputs.Any(output => output.Name == "forces-copy") &&
                  ProjectFieldOutputs.Read(removed).Outputs.Any(output => output.Name == "forces"),
                "field-output removal retains other requests and steps");

            foreach (var invalid in new[]
            {
                new FieldOutputSettings(" ", FieldOutputKind.Nodal, ["U"]),
                new FieldOutputSettings("DISPLACEMENT", FieldOutputKind.Nodal, ["U"]),
                new FieldOutputSettings("bad\nname", FieldOutputKind.Nodal, ["U"]),
                new FieldOutputSettings("empty", FieldOutputKind.Nodal, []),
                new FieldOutputSettings("unknown", FieldOutputKind.Nodal, ["BAD"]),
                new FieldOutputSettings("wrong-mode", FieldOutputKind.Contact, ["CDIS"], ElementOutput: ElementFieldOutputOutputEnum.TwoD),
                new FieldOutputSettings("bad-kind", (FieldOutputKind)99, ["U"])
            })
            {
                string rejectedPath = Path.Combine(root, "bad.pma"); bool rejected = false;
                try { ProjectFieldOutputs.AddCopy(source, rejectedPath, "load-1", invalid); }
                catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(rejectedPath), "invalid field-output names, variables, kinds and modes fail before publication");
            }
            bool typeChangeRejected = false;
            try { ProjectFieldOutputs.SaveCopy(source, Path.Combine(root, "bad.pma"), "load-1", "displacement",
                new("displacement", FieldOutputKind.Element, ["S"])); }
            catch (ArgumentException) { typeChangeRejected = true; }
            check(typeChangeRejected, "editing refuses a field-output type change like the original disabled type selector");
            foreach (Action operation in new Action[]
            {
                () => ProjectFieldOutputs.RemoveCopy(source, Path.Combine(root, "bad.pma"), "missing", "displacement"),
                () => ProjectFieldOutputs.RemoveCopy(source, Path.Combine(root, "bad.pma"), "load-1", "missing"),
                () => ProjectFieldOutputs.DuplicateCopy(source, Path.Combine(root, "bad.pma"), "load-1", "displacement", "STRESS-STRAIN"),
                () => ProjectFieldOutputs.PropagateCopy(source, Path.Combine(root, "bad.pma"), "missing", "displacement")
            })
            {
                bool rejected = false; try { operation(); } catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "bad.pma")),
                    "field-output operations reject missing selections and case-insensitive name collisions");
            }
            bool overwrite = false;
            try { ProjectFieldOutputs.RemoveCopy(source, source, "load-1", "displacement"); }
            catch (IOException) { overwrite = true; }
            check(overwrite && SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(before),
                "field-output operations refuse overwrite and preserve original project bytes");

            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("keywords");
            model.CalculixUserKeywords.Add([0], new CalculixUserKeyword("*Preprint, echo=NO"));
            string keywords = Path.Combine(root, "keywords.pma"); StaticProjectArchive.Save(keywords, model);
            bool keywordRejected = false;
            try { ProjectFieldOutputs.RemoveCopy(keywords, Path.Combine(root, "bad.pma"), "load-1", "displacement"); }
            catch (NotSupportedException) { keywordRejected = true; }
            check(keywordRejected && ProjectFieldOutputs.Read(keywords).EditingError != null,
                "field-output edits with positional user keywords remain inspectable and fail closed");
            check(!Directory.EnumerateFiles(root, ".edit-*.pma").Any(),
                "field-output edit and failure paths leave no staging projects");
        }
        finally { Directory.Delete(root, true); }
    }
}
