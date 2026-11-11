using System.Security.Cryptography;
using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectHistoryOutputChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "history output parity é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(2).Model;
            var point = new FeReferencePoint("RP", 1, .5, .5);
            model.Mesh.ReferencePoints.Add(point.Name, point);
            var interaction = new SurfaceInteraction("contact-law");
            interaction.AddProperty(new SurfaceBehavior());
            model.SurfaceInteractions.Add(interaction.Name, interaction);
            var pair = new ContactPair("block-contact", interaction.Name, ContactPairMethod.NodeToSurface,
                false, false, 0, "LOADED", RegionTypeEnum.SurfaceName, "LOADED", RegionTypeEnum.SurfaceName);
            model.ContactPairs.Add(pair.Name, pair);
            var first = (StaticStep)model.StepCollection.StepsList[0];
            first.AddHistoryOutput(new ElementHistoryOutput("solid-history", ElementHistoryVariable.S | ElementHistoryVariable.ENER,
                "SOLID", RegionTypeEnum.ElementSetName) { TotalsType = TotalsTypeEnum.Yes, Global = false });
            first.AddHistoryOutput(new ContactHistoryOutput("contact-history", ContactHistoryVariable.CDIS | ContactHistoryVariable.CF,
                pair.Name) { TotalsType = TotalsTypeEnum.Only });
            first.AddHistoryOutput(new SectionHistoryOutput("section-history", SectionHistoryVariableEnum.SOF | SectionHistoryVariableEnum.SOM,
                "LOADED", RegionTypeEnum.SurfaceName));
            string source = Path.Combine(root, "source.pma");
            StaticProjectArchive.Save(source, model, new byte[] { 1 }, new byte[] { 2 });
            byte[] before = SHA256.HashData(File.ReadAllBytes(source));

            var data = ProjectHistoryOutputs.Read(source);
            check(data.Steps.SequenceEqual(new[] { "load-1", "load-2" }) && data.Outputs.Length == 5 &&
                  data.VariableChoices["Nodal"].Contains("RF") && data.VariableChoices["Element"].Contains("SDV") &&
                  data.VariableChoices["Contact"].Contains("CF") && data.VariableChoices["Section"].Contains("SOAREA") &&
                  data.Regions.Any(region => region is { Name: "RIGHT", RegionType: "NodeSetName", Entities: 4 }) &&
                  data.Regions.Any(region => region is { Name: "block-contact", RegionType: "ContactPair" }),
                "history-output inspection exposes ordered requests, typed regions and original variables");
            var reopened = StaticProjectArchive.Open(source).Model.StepCollection.StepsList[0].HistoryOutputs;
            check(reopened["solid-history"] is ElementHistoryOutput && reopened["contact-history"] is ContactHistoryOutput &&
                  reopened["section-history"] is SectionHistoryOutput,
                "archive persistence restores every history-output subtype without converting it to nodal output");
            string deck = CalculixFileWriter.GetInputText(StaticProjectArchive.Open(source).Model, ConvertPyramidsToEnum.Wedges);
            check(deck.Contains("*El print, Elset=SOLID, Totals=Yes") && deck.Contains("S, ENER") &&
                  deck.Contains("*Contact print, Totals=Only, Master=LOADED, Slave=LOADED") && deck.Contains("CDIS, CF") &&
                  deck.Contains("*Section print, Surface=LOADED, Name=section-history") && deck.Contains("SOF, SOM"),
                "all history-output types reach their production CalculiX keyword writers");

            string added = Path.Combine(root, "added.pma");
            ProjectHistoryOutputs.AddCopy(source, added, "load-1", new("reaction", HistoryOutputKind.Nodal, ["RF", "U"],
                RegionTypeEnum.ReferencePointName, "RP", TotalsTypeEnum.Yes, false));
            var addedProject = StaticProjectArchive.Open(added);
            var reaction = (NodalHistoryOutput)((StaticStep)addedProject.Model.StepCollection.StepsList[0]).HistoryOutputs["reaction"];
            check(reaction.Variables == (NodalHistoryVariable.RF | NodalHistoryVariable.U) &&
                  reaction.RegionType == RegionTypeEnum.ReferencePointName && reaction.RegionName == "RP" &&
                  reaction.TotalsType == TotalsTypeEnum.Yes && !reaction.Global &&
                  addedProject.Frd == null && addedProject.Dat == null && addedProject.BinaryResults == null,
                "history-output creation persists region and print settings and discards stale results");

            string edited = Path.Combine(root, "edited.pma");
            ProjectHistoryOutputs.SaveCopy(added, edited, "load-1", "reaction", new("right-forces", HistoryOutputKind.Nodal,
                ["RF"], RegionTypeEnum.SurfaceName, "LOADED", TotalsTypeEnum.Only));
            var editedStep = (StaticStep)StaticProjectArchive.Open(edited).Model.StepCollection.StepsList[0];
            check(editedStep.HistoryOutputs.Keys.SequenceEqual(new[] { "right-history", "solid-history", "contact-history", "section-history", "right-forces" }) &&
                  ((NodalHistoryOutput)editedStep.HistoryOutputs["right-forces"]).RegionType == RegionTypeEnum.SurfaceName,
                "history-output edit renames in place and changes the named region without changing type");
            string duplicate = Path.Combine(root, "duplicate.pma");
            ProjectHistoryOutputs.DuplicateCopy(edited, duplicate, "load-1", "right-forces", "right-forces-copy");
            check(ProjectHistoryOutputs.Read(duplicate).Outputs.Where(output => output.Step == "load-1").Last() is
                  { Name: "right-forces-copy", Kind: "Nodal", RegionType: "SurfaceName", Region: "LOADED", Totals: "Only" },
                "history-output duplicate appends an independent request with all settings");
            string inactive = Path.Combine(root, "inactive.pma");
            ProjectHistoryOutputs.SetActiveCopy(duplicate, inactive, "load-1", "right-forces-copy", false);
            check(!ProjectHistoryOutputs.Read(inactive).Outputs.Single(output => output.Name == "right-forces-copy").Active,
                "history-output deactivation persists");
            string propagated = Path.Combine(root, "propagated.pma");
            ProjectHistoryOutputs.PropagateCopy(edited, propagated, "load-1", "solid-history");
            var propagatedSteps = StaticProjectArchive.Open(propagated).Model.StepCollection.StepsList.OfType<StaticStep>().ToArray();
            check(propagatedSteps[1].HistoryOutputs.Keys.SequenceEqual(new[] { "right-history", "solid-history" }) &&
                  propagatedSteps[1].HistoryOutputs["solid-history"] is ElementHistoryOutput &&
                  !ReferenceEquals(propagatedSteps[0].HistoryOutputs["solid-history"], propagatedSteps[1].HistoryOutputs["solid-history"]),
                "history-output propagation appends an independent typed request to later steps");
            string removed = Path.Combine(root, "removed.pma");
            ProjectHistoryOutputs.RemoveCopy(inactive, removed, "load-1", "right-forces-copy");
            check(!ProjectHistoryOutputs.Read(removed).Outputs.Any(output => output.Name == "right-forces-copy") &&
                  StaticProjectArchive.Open(removed).Model.Mesh.Surfaces.ContainsKey("LOADED"),
                "history-output removal preserves shared named regions");

            foreach (var invalid in new[]
            {
                new HistoryOutputSettings(" ", HistoryOutputKind.Nodal, ["U"], RegionTypeEnum.NodeSetName, "RIGHT"),
                new HistoryOutputSettings("RIGHT-HISTORY", HistoryOutputKind.Nodal, ["U"], RegionTypeEnum.NodeSetName, "RIGHT"),
                new HistoryOutputSettings("unknown-variable", HistoryOutputKind.Element, ["BAD"], RegionTypeEnum.ElementSetName, "SOLID"),
                new HistoryOutputSettings("wrong-region", HistoryOutputKind.Element, ["S"], RegionTypeEnum.NodeSetName, "RIGHT"),
                new HistoryOutputSettings("missing-region", HistoryOutputKind.Nodal, ["U"], RegionTypeEnum.NodeSetName, "missing"),
                new HistoryOutputSettings("section-options", HistoryOutputKind.Section, ["SOF"], RegionTypeEnum.SurfaceName, "LOADED", TotalsTypeEnum.Yes),
                new HistoryOutputSettings("bad-kind", (HistoryOutputKind)99, ["U"], RegionTypeEnum.NodeSetName, "RIGHT")
            })
            {
                string rejectedPath = Path.Combine(root, "bad.pma"); bool rejected = false;
                try { ProjectHistoryOutputs.AddCopy(source, rejectedPath, "load-1", invalid); }
                catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(rejectedPath), "invalid history-output names, variables, regions and options fail before publication");
            }
            bool typeChangeRejected = false;
            try { ProjectHistoryOutputs.SaveCopy(source, Path.Combine(root, "bad.pma"), "load-1", "right-history",
                new("right-history", HistoryOutputKind.Element, ["S"], RegionTypeEnum.ElementSetName, "SOLID")); }
            catch (ArgumentException) { typeChangeRejected = true; }
            check(typeChangeRejected, "editing refuses a history-output type change like the original disabled type selector");
            bool overwrite = false;
            try { ProjectHistoryOutputs.RemoveCopy(source, source, "load-1", "right-history"); }
            catch (IOException) { overwrite = true; }
            check(overwrite && SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(before),
                "history-output operations refuse overwrite and preserve original project bytes");

            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("keywords");
            model.CalculixUserKeywords.Add([0], new CalculixUserKeyword("*Preprint, echo=NO"));
            string keywords = Path.Combine(root, "keywords.pma"); StaticProjectArchive.Save(keywords, model);
            bool keywordRejected = false;
            try { ProjectHistoryOutputs.RemoveCopy(keywords, Path.Combine(root, "bad.pma"), "load-1", "right-history"); }
            catch (NotSupportedException) { keywordRejected = true; }
            check(keywordRejected && ProjectHistoryOutputs.Read(keywords).EditingError != null,
                "history-output edits with positional user keywords remain inspectable and fail closed");
            check(!Directory.EnumerateFiles(root, ".edit-*.pma").Any(),
                "history-output edit and failure paths leave no staging projects");
        }
        finally { Directory.Delete(root, true); }
    }
}
