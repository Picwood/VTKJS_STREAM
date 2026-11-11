using CaeGlobals;
using CaeMesh;
using CaeModel;
using FileInOut.Output;
using FileInOut.Output.Calculix;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectRegionMaintenanceChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "region maintenance é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(2).Model;
            model.Mesh.ReferencePoints.Add("RP", new FeReferencePoint("RP", 0, 0, 0));
            model.Constraints.Add("body", new RigidBody("body", "RP", "LEFT", RegionTypeEnum.NodeSetName, false) { Active = false });
            foreach (var step in model.StepCollection.StepsList)
            {
                step.AddHistoryOutput(new NodalHistoryOutput("left-history", NodalHistoryVariable.U, "LEFT", RegionTypeEnum.NodeSetName));
                step.AddLoad(new CLoad("left-force", "LEFT", RegionTypeEnum.NodeSetName, 1, 0, 0, false, false, 0) { Active = false });
            }
            var geometry = new ProjectGeometrySource("source.step", [1, 2, 3]);
            string source = Path.Combine(root, "nodes.pma");
            StaticProjectArchive.Save(source, model, [1], [2], geometrySource: geometry);
            byte[] before = File.ReadAllBytes(source);
            string renamed = Path.Combine(root, "nodes-renamed.pma");
            ProjectNodeSets.SaveCopy(source, renamed, "left", new("renamed-left", "1,4,4"));
            var reopened = StaticProjectArchive.Open(renamed);
            check(reopened.Model.Mesh.NodeSets["renamed-left"].Labels.SequenceEqual(new[] { 1, 4 }) &&
                reopened.Model.StepCollection.StepsList.All(step => step.BoundaryConditions["left"].RegionName == "renamed-left" &&
                    step.Loads["left-force"].RegionName == "renamed-left" &&
                    step.HistoryOutputs["left-history"].RegionName == "renamed-left"),
                "node-set membership edit/rename updates BCs, inactive loads and histories in every step");
            check(((RigidBody)reopened.Model.Constraints["body"]).RegionName == "renamed-left", "node-set rename updates rigid-body region references");
            check(reopened.Frd == null && reopened.Dat == null && reopened.GeometrySource!.Data.SequenceEqual(geometry.Data) && File.ReadAllBytes(source).SequenceEqual(before),
                "region edits preserve source/geometry and invalidate stale results");
            string boxed = Path.Combine(root, "boxed.pma");
            ProjectNodeSets.SaveCopy(renamed, boxed, "renamed-left", new("renamed-left", Box: new(0, 0, 0, 1, 0, 1)));
            check(ProjectNodeSets.List(boxed).Single(set => set.Name == "renamed-left").Nodes.SequenceEqual(new[] { 1, 4, 5, 8 }),
                "node-set coordinate-box editing uses inclusive undeformed mesh coordinates");
            check(ProjectNodeSets.Preview(boxed, new("preview", "2:7:2")).SequenceEqual(new[] { 2, 4, 6 }),
                "node-set preview supports validated ranges and strides");
            string duplicate = Path.Combine(root, "node-duplicate.pma");
            ProjectNodeSets.DuplicateCopy(boxed, duplicate, "renamed-left", "spare");
            string changed = Path.Combine(root, "node-changed.pma");
            ProjectNodeSets.SaveCopy(duplicate, changed, "spare", new("spare", "1"));
            check(ProjectNodeSets.List(changed).Single(set => set.Name == "renamed-left").Nodes.Length == 4 &&
                ProjectNodeSets.List(changed).Single(set => set.Name == "spare").Nodes.SequenceEqual(new[] { 1 }), "node-set duplicates own independent membership");
            string removed = Path.Combine(root, "node-removed.pma");
            ProjectNodeSets.RemoveCopy(changed, removed, "spare");
            check(ProjectNodeSets.List(removed).All(set => set.Name != "spare"), "unreferenced node sets can be removed");
            string surfaceNodeRename = Path.Combine(root, "surface-source-rename.pma");
            ProjectNodeSets.SaveCopy(source, surfaceNodeRename, "RIGHT", new("renamed-right"));
            check(StaticProjectArchive.Open(surfaceNodeRename).Model.Mesh.Surfaces["LOADED"].CreatedFromNodeSetName == "renamed-right",
                "renaming a surface source node set updates the surface source link");
            check(ProjectNodeSets.Read(source).Sets.Single(set => set.Name == "RIGHT").MembershipLocked,
                "surface-derived node sets are identified as protected membership");
            string refused = Path.Combine(root, "refused.pma");
            void Reject(Action operation, string message)
            {
                bool failed = false;
                try { operation(); } catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException) { failed = true; }
                check(failed && !File.Exists(refused), message);
            }
            Reject(() => ProjectNodeSets.RemoveCopy(boxed, refused, "renamed-left"), "node-set removal refuses referenced BC/load/constraint/field/history regions");
            Reject(() => ProjectNodeSets.SaveCopy(source, refused, "RIGHT", new("RIGHT", "1")), "node-set edits cannot desynchronize an element surface");
            foreach (string ids in new[] { "", "0", "9", "8:1", "1:8:0", "1:2147483647", "2147483648" })
                Reject(() => ProjectNodeSets.SaveCopy(source, refused, "LEFT", new("LEFT", ids)), "invalid node-set membership/ranges publish nothing");
            foreach (var box in new[] { new NodeSelectionBox(2, 1, 0, 1, 0, 1), new(0, 1, double.NaN, 1, 0, 1), new(.1, .2, 0, 1, 0, 1) })
                Reject(() => ProjectNodeSets.SaveCopy(source, refused, "LEFT", new("LEFT", Box: box)), "invalid or empty node boxes publish nothing");
            Reject(() => ProjectNodeSets.SaveCopy(source, refused, "LEFT", new("RIGHT")), "node-set rename collisions are refused");

            model = new StaticModelWorkflow(2).Model;
            model.Mesh.Surfaces["LOADED"].NodeSetName = "RIGHT";
            var fixedSurface = new FeSurface("FIXED", "LEFT") { NodeSetName = "LEFT" };
            fixedSurface.AddElementFace(FeFaceName.S6, "SOLID");
            model.Mesh.Surfaces.Add(fixedSurface.Name, fixedSurface);
            model.Mesh.ReferencePoints.Add("RP", new FeReferencePoint("RP", 0, 0, 0));
            model.Constraints.Add("tie", new Tie("tie", .01, false, "LOADED", RegionTypeEnum.SurfaceName, "FIXED", RegionTypeEnum.SurfaceName, false) { Active = false });
            model.Constraints.Add("rigid", new RigidBody("rigid", "RP", "LOADED", RegionTypeEnum.SurfaceName, false) { Active = false });
            model.Constraints.Add("spring", new SurfaceSpring("spring", "LOADED", RegionTypeEnum.SurfaceName, false, false) { Active = false });
            var law = new SurfaceInteraction("law"); law.AddProperty(new Friction(.2, 5000)); model.SurfaceInteractions.Add(law.Name, law);
            model.ContactPairs.Add("contact", new ContactPair("contact", "law", ContactPairMethod.NodeToSurface, false, false, 0,
                "LOADED", RegionTypeEnum.SurfaceName, "FIXED", RegionTypeEnum.SurfaceName));
            foreach (var step in model.StepCollection.StepsList)
            {
                step.AddLoad(new DLoad("pressure", "LOADED", RegionTypeEnum.SurfaceName, 2, false, false, 0));
                step.AddBoundaryCondition(new DisplacementRotation("surface-bc", "LOADED", RegionTypeEnum.SurfaceName, false, false, 0)
                    { U2 = new EquationContainer(typeof(StringLengthDOFConverter), 0) });
                step.AddHistoryOutput(new NodalHistoryOutput("surface-history", NodalHistoryVariable.U, "LOADED", RegionTypeEnum.SurfaceName));
                step.AddHistoryOutput(new SectionHistoryOutput("section-history", SectionHistoryVariableEnum.SOF, "LOADED", RegionTypeEnum.SurfaceName));
            }
            string surfaceSource = Path.Combine(root, "surfaces.pma");
            StaticProjectArchive.Save(surfaceSource, model, [1], [2], geometrySource: geometry);
            byte[] surfaceBefore = File.ReadAllBytes(surfaceSource);
            string surfaceChanged = Path.Combine(root, "surface-changed.pma");
            ProjectSurfaces.SaveCopy(surfaceSource, surfaceChanged, "loaded", new("renamed-loaded", [new(1, "S2")]));
            reopened = StaticProjectArchive.Open(surfaceChanged);
            var editedSurface = reopened.Model.Mesh.Surfaces["renamed-loaded"];
            int[] expectedNodes = model.Mesh.Elements[1].GetNodeIdsFromFaceName(FeFaceName.S2).Order().ToArray();
            check(Math.Abs(editedSurface.Area - 1) < 1e-12 && reopened.Model.Mesh.NodeSets[editedSurface.NodeSetName].Labels.SequenceEqual(expectedNodes) &&
                reopened.Model.Mesh.NodeSets["RIGHT"].Labels.SequenceEqual(model.Mesh.NodeSets["RIGHT"].Labels),
                "surface edits rebuild exact face nodes and area without modifying shared source node sets");
            check(reopened.Model.StepCollection.StepsList.All(step => step.Loads["force"].RegionName == "renamed-loaded" &&
                step.Loads["pressure"].RegionName == "renamed-loaded" && step.BoundaryConditions["surface-bc"].RegionName == "renamed-loaded" &&
                step.HistoryOutputs["surface-history"].RegionName == "renamed-loaded" && step.HistoryOutputs["section-history"].RegionName == "renamed-loaded"),
                "surface rename updates traction, pressure, BCs and nodal/section histories across steps");
            check(reopened.Model.Constraints["tie"].MasterRegionName == "renamed-loaded" &&
                ((RigidBody)reopened.Model.Constraints["rigid"]).RegionName == "renamed-loaded" &&
                ((SurfaceSpring)reopened.Model.Constraints["spring"]).RegionName == "renamed-loaded" &&
                reopened.Model.ContactPairs["contact"].MasterRegionName == "renamed-loaded",
                "surface rename preserves tie, rigid-body, spring and contact-pair region links");
            string deck = CalculixFileWriter.GetInputText(reopened.Model, ConvertPyramidsToEnum.Wedges);
            check(deck.Contains("FIXED, renamed-loaded") && deck.Contains(", P2, 2") && deck.Contains("Surface=renamed-loaded"),
                "updated surface definitions reach the production contact, pressure and section-history writers");
            check(reopened.Frd == null && reopened.Dat == null && reopened.GeometrySource!.Data.SequenceEqual(geometry.Data) &&
                File.ReadAllBytes(surfaceSource).SequenceEqual(surfaceBefore), "surface edits preserve source/geometry and invalidate results");
            check(ProjectNodeSets.Read(surfaceChanged).Sets.Single(set => set.Name == editedSurface.NodeSetName).MembershipLocked,
                "surface backing node sets are protected from independent membership edits");
            string surfaceDup = Path.Combine(root, "surface-duplicate.pma");
            ProjectSurfaces.DuplicateCopy(surfaceChanged, surfaceDup, "renamed-loaded", "copy");
            var dupModel = StaticProjectArchive.Open(surfaceDup).Model;
            check(dupModel.Mesh.Surfaces["copy"].NodeSetName != editedSurface.NodeSetName &&
                dupModel.Mesh.Surfaces["copy"].ElementFaces.Values.Single() != editedSurface.ElementFaces.Values.Single(),
                "surface duplication creates independent node and element backing sets");
            string surfaceDupEdit = Path.Combine(root, "surface-copy-edited.pma");
            ProjectSurfaces.SaveCopy(surfaceDup, surfaceDupEdit, "copy", new("copy", [new(1, "S1")]));
            dupModel = StaticProjectArchive.Open(surfaceDupEdit).Model;
            check(dupModel.Mesh.Surfaces["renamed-loaded"].ElementFaces.ContainsKey(FeFaceName.S2) && dupModel.Mesh.Surfaces["copy"].ElementFaces.ContainsKey(FeFaceName.S1),
                "editing a duplicate surface does not change the original");
            string surfaceRemoved = Path.Combine(root, "surface-removed.pma");
            ProjectSurfaces.RemoveCopy(surfaceDupEdit, surfaceRemoved, "copy");
            dupModel = StaticProjectArchive.Open(surfaceRemoved).Model;
            check(!dupModel.Mesh.Surfaces.ContainsKey("copy") && dupModel.Mesh.ElementSets.ContainsKey("SOLID") && dupModel.Mesh.NodeSets.ContainsKey("RIGHT"),
                "unreferenced surface removal preserves potentially shared or user-owned backing sets");
            Reject(() => ProjectSurfaces.RemoveCopy(surfaceChanged, refused, "renamed-loaded"), "referenced surfaces cannot be removed");
            Reject(() => ProjectNodeSets.SaveCopy(surfaceChanged, refused, editedSurface.NodeSetName, new(editedSurface.NodeSetName, "1")),
                "surface-owned nodes cannot be independently changed");
            Reject(() => ProjectSurfaces.SaveCopy(surfaceSource, refused, "LOADED", new("FIXED")), "surface rename collisions are refused");
            foreach (var invalid in new ProjectElementFace[][] { [], [new(99, "S1")], [new(1, "S9")], Enumerable.Repeat(new ProjectElementFace(1, "S1"), 101).ToArray() })
                Reject(() => ProjectSurfaces.SaveCopy(surfaceSource, refused, "LOADED", new("changed", invalid)), "invalid surface membership publishes nothing");
            string renameOnly = Path.Combine(root, "rename-only.pma");
            ProjectSurfaces.SaveCopy(surfaceChanged, renameOnly, "renamed-loaded", new("again"));
            check(StaticProjectArchive.Open(renameOnly).Model.Mesh.Surfaces["again"].NodeSetName == editedSurface.NodeSetName,
                "rename-only surface edits retain the complete existing backing representation");
            var nodal = new StaticModelWorkflow(1).Model;
            nodal.Mesh.Surfaces.Add("nodal", new FeSurface("nodal", "LEFT") { Type = FeSurfaceType.Node, NodeSetName = "LEFT" });
            string nodeSurface = Path.Combine(root, "nodal-surface.pma"); StaticProjectArchive.Save(nodeSurface, nodal);
            string nodeSurfaceCopy = Path.Combine(root, "nodal-surface-copy.pma");
            ProjectSurfaces.DuplicateCopy(nodeSurface, nodeSurfaceCopy, "nodal", "nodal-copy");
            var copy = StaticProjectArchive.Open(nodeSurfaceCopy).Model.Mesh.Surfaces["nodal-copy"];
            string changedNodal = Path.Combine(root, "nodal-surface-changed.pma");
            ProjectNodeSets.SaveCopy(nodeSurfaceCopy, changedNodal, copy.NodeSetName, new("nodal-backing", "1"));
            var nodeModel = StaticProjectArchive.Open(changedNodal).Model;
            check(copy.NodeSetName != "LEFT" && nodeModel.Mesh.Surfaces["nodal-copy"].NodeSetName == "nodal-backing" &&
                nodeModel.Mesh.Surfaces["nodal-copy"].CreatedFromNodeSetName == "nodal-backing" && nodeModel.Mesh.NodeSets["LEFT"].Labels.Length == 4 &&
                ProjectSurfaces.Read(changedNodal, "nodal-copy").Selected!.NodeSetName == "nodal-backing" &&
                ProjectSurfaces.Read(changedNodal).Surfaces.Single(surface => surface.Name == "nodal-copy").Nodes == 1,
                "node-surface duplicates own independent backing nodes and follow node-set membership/rename edits");
            model.CalculixUserKeywords = new CaeGlobals.OrderedDictionary<int[], CalculixUserKeyword>("keywords");
            model.CalculixUserKeywords.Add([0], new CalculixUserKeyword("*Preprint, echo=NO"));
            string keywords = Path.Combine(root, "keywords.pma"); StaticProjectArchive.Save(keywords, model);
            Reject(() => ProjectSurfaces.SaveCopy(keywords, refused, "LOADED", new("renamed")), "untracked user keywords block surface mutation");
            Reject(() => ProjectNodeSets.SaveCopy(keywords, refused, "LEFT", new("renamed")), "untracked user keywords block node-set mutation");
            check(!Directory.GetFiles(root, ".edit-*").Any(), "failed region edits clean staged archives");
        }
        finally { Directory.Delete(root, true); }
    }
}
