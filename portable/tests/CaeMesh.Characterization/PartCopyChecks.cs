using CaeMesh;
using CaeGlobals;

internal static class PartCopyChecks
{
    public static void Run(Action<bool, string> check)
    {
        var history = new Selection { CurrentView = 2, SelectItem = vtkSelectItem.Element,
            MaxNumberOfGeometryIds = 8, MaxNumberOfItemIds = 20, LimitSelectionToFirstPart = true,
            LimitSelectionToShellEdges = true, EnableShellEdgeFaceSelection = true };
        var ids = new SelectionNodeIds(vtkSelectOperation.Subtract, false, new[] { 11, 12 }, true, GeometrySelectModeEnum.SelectId);
        ids.SetPartId(9);
        var mouse = new SelectionNodeMouse(new[] { 1d, 2, 3 }, new[] { 0d, 0, 1 },
            new[] { new[] { 0d, 0, 0, 0, 0, 1 }, null }, true, vtkSelectOperation.Add,
            new[] { 9 }, new[] { new[] { 4d, 5, 6 }, null }, vtkSelectBy.GeometrySurface, 45) { Precision = .01 };
        history.Add(ids); history.Add(mouse, null); history.Add(new SelectionNodeInvert(), null);
        var set = new FeElementSet("Parts", new[] { 11, 12 }, true) { CreationData = history, CreationIds = new[] { 9 }, Visible = false };
        Selection.EnableSelectionCache(true);
        try
        {
            Selection.AddToCache(ids, new[] { 11, 12 });
            var copy = new FeElementSet(set);
            var copyIds = (SelectionNodeIds)copy.CreationData.Nodes[0];
            var copyMouse = (SelectionNodeMouse)copy.CreationData.Nodes[1];
            check(copy.Labels.SequenceEqual(set.Labels) && copy.CreatedFromParts && !copy.Visible && copy.Name == "Parts", "element set copied state");
            check(copy.CreationData.CurrentView == 2 && copy.CreationData.SelectItem == vtkSelectItem.Element &&
                copy.CreationData.MaxNumberOfGeometryIds == 8 && copy.CreationData.MaxNumberOfItemIds == 20 &&
                copy.CreationData.LimitSelectionToFirstPart && copy.CreationData.LimitSelectionToShellEdges &&
                copy.CreationData.EnableShellEdgeFaceSelection, "copied selection settings");
            check(copyIds.Hash == -1 && !copy.CreationData.TryGetNodeIds(copyIds, out _) && history.TryGetNodeIds(ids, out _), "copy does not inherit or remove cache");
            check(copyIds.GeometrySelectMode == GeometrySelectModeEnum.SelectId && copyIds.IsGeometryBased &&
                copyIds.SelectOperation == vtkSelectOperation.Subtract && !copyIds.SelectAll, "copied ID selection semantics");
            check(copyMouse.CompletelyInside && copyMouse.Angle == 45 && copyMouse.Precision == .01 &&
                copyMouse.SelectBy == vtkSelectBy.GeometrySurface && copyMouse.PlaneParameters[1] == null &&
                copyMouse.PartOffsets[1] == null, "copied mouse selection semantics");
            check(copy.CreationData.Nodes[2] is SelectionNodeInvert && copy.CreationData.Nodes[2].SelectOperation == vtkSelectOperation.Invert, "copied invert node");
            copy.Labels[0] = 99; copy.CreationIds[0] = 99; copyIds.ItemIds[0] = 99; copyIds.PartIds[0] = 99;
            copyMouse.PickedPoint[0] = 99; copyMouse.SelectionDirection[0] = 99;
            copyMouse.PlaneParameters[0][0] = 99; copyMouse.PartOffsets[0][0] = 99; copyMouse.PartIds[0] = 99;
            check(set.Labels[0] == 11 && set.CreationIds[0] == 9 && ids.ItemIds[0] == 11 && ids.PartIds[0] == 9, "element set array isolation");
            check(mouse.PickedPoint[0] == 1 && mouse.SelectionDirection[0] == 0 && mouse.PlaneParameters[0][0] == 0 &&
                mouse.PartOffsets[0][0] == 4 && mouse.PartIds[0] == 9, "mouse nested array isolation");
            copy.CreationData.Nodes.Clear();
            check(history.Nodes.Count == 3, "selection list isolation");
        }
        finally { Selection.EnableSelectionCache(false); }
        var empty = new FeElementSet(new FeElementSet("Empty", Array.Empty<int>()));
        check(empty.CreationData == null && empty.CreationIds == null, "null creation state copy");
        var mass = new PartMassProperties(true) { Volume = 12, Area = 8 };
        mass.CenterOfMass[0] = 1; mass.InertiaMatrixCG[0][0] = 2; mass.InertiaMatrixOrigin[0][0] = 3;
        var massCopy = mass.DeepCopy();
        check(massCopy.Volume == 12 && massCopy.Area == 8 && massCopy.CenterOfMass[0] == 1 &&
            massCopy.InertiaMatrixCG[0][0] == 2 && massCopy.InertiaMatrixOrigin[0][0] == 3, "mass property values");
        massCopy.CenterOfMass[0] = 99; massCopy.InertiaMatrixCG[0][0] = 99; massCopy.InertiaMatrixOrigin[0][0] = 99;
        check(mass.CenterOfMass[0] == 1 && mass.InertiaMatrixCG[0][0] == 2 && mass.InertiaMatrixOrigin[0][0] == 3, "mass property nested array isolation");
        var uninitialized = new PartMassProperties(false).DeepCopy();
        check(uninitialized.CenterOfMass == null && uninitialized.InertiaMatrixCG == null && uninitialized.InertiaMatrixOrigin == null, "uninitialized mass copy");
    }
}
