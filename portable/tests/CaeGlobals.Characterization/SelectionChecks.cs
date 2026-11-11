using CaeGlobals;
using DynamicTypeDescriptor;
using System.Reflection;

internal static class SelectionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var attribute = new StandardValueAttribute(42);
        check(attribute.DisplayName == "42" && attribute.Enabled && attribute.Visible, "metadata defaults");
        attribute.DisplayName = "answer";
        check(attribute.ToString() == "answer" && (int)attribute.Value == 42, "metadata label override");
        var field = typeof(GeometrySelectModeEnum).GetField(nameof(GeometrySelectModeEnum.SelectId));
        check(field.GetCustomAttribute<StandardValueAttribute>().DisplayName == "Selection by ID", "selection field metadata");
        foreach (RegionTypeEnum region in Enum.GetValues<RegionTypeEnum>())
        {
            var label = typeof(RegionTypeEnum).GetField(region.ToString()).GetCustomAttribute<StandardValueAttribute>();
            check(label.DisplayName == region.ToFriendlyString(), "region label parity");
        }
        var geometryModes = new HashSet<vtkSelectBy> { vtkSelectBy.Geometry, vtkSelectBy.GeometryVertex,
            vtkSelectBy.GeometryEdge, vtkSelectBy.GeometrySurface, vtkSelectBy.GeometryEdgeAngle,
            vtkSelectBy.GeometrySurfaceAngle, vtkSelectBy.GeometryPart };
        foreach (vtkSelectBy mode in Enum.GetValues<vtkSelectBy>())
            check(mode.IsGeometryBased() == geometryModes.Contains(mode), "selection classification");

        var selection = new Selection { SelectItem = vtkSelectItem.Node, CurrentView = 2 };
        check(selection.IsGeometryIdBased(true) && !selection.IsGeometryIdBased(false), "empty selection default");
        int[] input = { 4, 7 };
        var ids = new SelectionNodeIds(vtkSelectOperation.Add, false, input, true, GeometrySelectModeEnum.SelectId);
        input[0] = 99;
        check(ids.ItemIds.SequenceEqual(new[] { 4, 7 }), "selection constructor copies IDs");
        selection.Add(ids);
        check(selection.IsGeometryBased() && selection.IsGeometryIdBased(false), "geometry ID selection");
        var mouse = new SelectionNodeMouse(new[] { 1d, 2, 3 }, new[] { 0d, 0, 1 },
            Enumerable.Range(0, 6).Select(i => new[] { (double)i, 0, 0, 0, 1, 0 }).ToArray(),
            true, vtkSelectOperation.Add, new[] { 8 }, null, vtkSelectBy.Node, 30);
        selection.Add(mouse, new[] { 9 });
        check(!selection.IsGeometryBased() && !selection.IsGeometryIdBased(true), "mixed mesh selection");
        mouse.AddOffset(new[] { 10d, 20, 30 });
        check(mouse.PickedPoint.SequenceEqual(new[] { 11d, 22, 33 }), "pick offset");
        check(mouse.PlaneParameters[5].SequenceEqual(new[] { 15d, 20, 30, 0, 1, 0 }), "plane offset preserves normal");
        mouse.RemoveOffset(new[] { 10d, 20, 30 });
        check(mouse.PickedPoint.SequenceEqual(new[] { 1d, 2, 3 }), "inverse pick offset");
        check(mouse.PlaneParameters[5].SequenceEqual(new[] { 5d, 0, 0, 0, 1, 0 }), "inverse plane offset");
        selection.LimitSelectionToFirstPart = true;
        selection.LimitSelectionToFirstGeometryType = true;
        check(!selection.LimitSelectionToFirstPart && selection.LimitSelectionToFirstGeometryType, "geometry limit exclusivity");
        selection.LimitSelectionToFirstMesherType = true;
        check(!selection.LimitSelectionToFirstGeometryType && selection.LimitSelectionToFirstMesherType, "mesher limit exclusivity");
        Selection.EnableSelectionCache(true);
        try
        {
            Selection.AddToCache(ids, new[] { 4, 7 });
            Selection.AddToCache(mouse, new[] { 9 });
            check(selection.TryGetNodeIds(ids, out var cached) && cached.SequenceEqual(new[] { 4, 7 }), "selection cache lookup");
            selection.RemoveFirst();
            check(ids.Hash == -1 && !selection.TryGetNodeIds(ids, out _), "first removal clears cache");
            selection.RemoveLast();
            check(mouse.Hash == -1 && !selection.TryGetNodeIds(mouse, out _) && selection.Nodes.Count == 0, "last removal clears cache");
            selection.Add(ids);
            selection.Clear();
            check(selection.Nodes.Count == 0 && selection.SelectItem == vtkSelectItem.Node && selection.CurrentView == 2, "clear preserves selection context");
        }
        finally { Selection.EnableSelectionCache(false); }

        check(Tools.RoundToSignificantDigits(12345, 3) == 12300, "round large value");
        check(Tools.RoundToSignificantDigits(-0.012345, 3) == -0.0123, "round small negative value");
        check(Tools.RoundToSignificantDigits(0, 3) == 0, "round zero");
        check(new[] { true, false }.ToDouble().SequenceEqual(new[] { 1d, 0 }), "boolean conversion");
        check(new[] { 1.25, -2.5 }.ToFloat().SequenceEqual(new[] { 1.25f, -2.5f }), "float conversion");
        double[][] rows = { new[] { 1d, 2, 3 }, new[] { 4d, 5, 6 } };
        double[] flat = rows.ToFlatArray();
        check(flat.SequenceEqual(new[] { 1d, 2, 3, 4, 5, 6 }), "row-major flatten");
        double[][] restored = flat.ToJaggedArray(2);
        check(restored[0].SequenceEqual(rows[0]) && restored[1].SequenceEqual(rows[1]), "row-major reconstruction");
        restored[0][0] = 99;
        check(flat[0] == 1 && rows[0][0] == 1, "array conversion isolation");
    }
}
