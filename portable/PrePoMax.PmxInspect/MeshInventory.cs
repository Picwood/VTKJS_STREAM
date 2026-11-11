using System.Formats.Nrbf;

internal sealed class MeshInventory(BinaryReader reader, Version version)
{
    internal List<ConversionFinding> ConversionBlockers { get; } = [];
    long values;
    int Count(bool nullable = false)
    {
        int count = reader.ReadInt32();
        if (count < (nullable ? -1 : 0) || count > 10000000 || (values += Math.Max(0, count)) > 100000000)
            throw new InvalidDataException("Mesh count exceeds inspection limits.");
        return count;
    }
    void Array(int width, bool jagged = false)
    {
        int count = Count(true);
        for (int i = 0; i < count; i++)
            if (jagged) Array(4);
            else if (width == 8) reader.ReadDouble();
            else reader.ReadInt32();
    }
    void Visualization()
    {
        int present = reader.ReadInt32();
        if (present == -1) return;
        if (present != 1) throw new InvalidDataException("Invalid visualization flag.");
        Array(4, true); Array(4); Array(4, true); Array(8); Array(4);
        Array(4, true); Array(4, true); Array(4, true); Array(4, true);
        Array(8); Array(4); Array(4);
    }
    object? Mesh(ClassRecord? metadata, bool analysisMesh)
    {
        int present = reader.ReadInt32();
        if (present == 0) return null;
        if (present != 1) throw new InvalidDataException("Invalid mesh presence flag.");
        var nodes = new HashSet<int>();
        int count = Count();
        for (int i = 0; i < count; i++)
        {
            int id = reader.ReadInt32();
            if (id <= 0 || !nodes.Add(id)) throw new InvalidDataException("Invalid or duplicate mesh node ID.");
            for (int axis = 0; axis < 3; axis++)
                if (!double.IsFinite(reader.ReadDouble())) throw new InvalidDataException("Nonfinite mesh coordinate.");
        }
        var elements = new Dictionary<int, (int PartId, int Cell)>();
        var cells = new SortedDictionary<int, int>();
        var partIds = new HashSet<int>();
        var declaredParts = metadata == null ? null : ActiveModelInventory.DictionaryItems(metadata.GetClassRecord("_parts"))
            .ToDictionary(p => p.GetInt32("_partId"));
        int formulationsChecked = 0, formulationsUnchecked = 0;
        count = Count();
        for (int i = 0; i < count; i++)
        {
            int partId = reader.ReadInt32();
            partIds.Add(partId);
            int id = reader.ReadInt32(), cell = reader.ReadInt32(), width = Count();
            int expected = cell switch { 3 => 2, 21 => 3, 5 => 3, 9 => 4, 22 => 6, 23 => 8,
                10 => 4, 14 => 5, 13 => 6, 12 => 8, 24 => 10, 27 => 13, 26 => 15, 25 => 20, _ => -1 };
            if (id <= 0 || !elements.TryAdd(id, (partId, cell)) || width != expected)
                throw new InvalidDataException("Invalid element ID, VTK cell type or connectivity width.");
            if (declaredParts != null)
            {
                if (!declaredParts.TryGetValue(partId, out var part))
                    throw new InvalidDataException("Element references missing part metadata.");
                string? field = cell switch { 24 => "_parabolicTetraType", 25 => "_parabolicHexaType", 26 => "_parabolicWedgeType", _ => null };
                if (field == null) formulationsUnchecked++;
                else
                {
                    int formulation = part.GetClassRecord(field)?.GetInt32("value__") ?? -1;
                    if (formulation != 10 && !(formulation == 11 && cell is 24 or 25))
                        throw new InvalidDataException("Element topology has missing or unknown part formulation.");
                    formulationsChecked++;
                }
            }
            for (int j = 0; j < width; j++)
                if (!nodes.Contains(reader.ReadInt32())) throw new InvalidDataException("Element references missing node.");
            cells[cell] = cells.GetValueOrDefault(cell) + 1;
        }
        int parts = Count(true);
        for (int i = 0; i < parts; i++)
        {
            if (version >= new Version(2, 4, 4))
            {
                int bytes = reader.Read7BitEncodedInt();
                if (bytes < 0 || bytes > 4096) throw new InvalidDataException("Invalid part name length.");
                if (reader.ReadBytes(bytes).Length != bytes) throw new EndOfStreamException();
            }
            Visualization();
        }
        int setsChecked = 0;
        if (metadata != null)
            foreach (string field in new[] { "_nodeSets", "_elementSets" })
                foreach (var set in ActiveModelInventory.DictionaryItems(metadata.GetClassRecord(field)))
                {
                    string labelField = set.HasMember("_labels") ? "_labels" : "FeGroup+_labels";
                    if (set.GetArrayRecord(labelField) is not SZArrayRecord<int> labels || labels.Length > 10000000)
                        throw new InvalidDataException("Unsupported set labels or set exceeds limit.");
                    IEnumerable<int> validIds = field == "_nodeSets" ? nodes :
                        set.HasMember("_createdFromParts") && set.GetBoolean("_createdFromParts") ? partIds : elements.Keys;
                    foreach (int id in labels.GetArray())
                        if (!validIds.Contains(id)) throw new InvalidDataException("Set references missing node, element or part.");
                    setsChecked++;
                }
        int partMembershipsChecked = 0;
        if (declaredParts != null)
        {
            foreach (var entry in declaredParts)
            {
                int[] labels = Labels(entry.Value);
                if (labels.Length != labels.Distinct().Count() ||
                    !labels.ToHashSet().SetEquals(elements.Where(e => e.Value.PartId == entry.Key).Select(e => e.Key)))
                    throw new InvalidDataException("Part labels do not exactly match binary element membership.");
                partMembershipsChecked++;
            }
            if (!partIds.SetEquals(declaredParts.Keys))
                throw new InvalidDataException("Part metadata and binary element part IDs differ.");
        }
        int surfaceFacesChecked = 0;
        if (metadata != null)
        {
            var elementSets = ActiveModelInventory.DictionaryPairs(metadata.GetClassRecord("_elementSets"))
                .ToDictionary(pair => pair.GetString("key") ?? throw new InvalidDataException("Null element-set name."),
                    pair => pair.GetClassRecord("value") ?? throw new InvalidDataException("Null element set."), StringComparer.Ordinal);
            IEnumerable<int> ElementSetMembers(ClassRecord set)
            {
                int[] labels = Labels(set);
                if (set.HasMember("_createdFromParts") && set.GetBoolean("_createdFromParts"))
                {
                    var selectedParts = labels.ToHashSet();
                    return elements.Where(e => selectedParts.Contains(e.Value.PartId)).Select(e => e.Key);
                }
                return labels;
            }
            foreach (var surface in ActiveModelInventory.DictionaryItems(metadata.GetClassRecord("_surfaces")))
                foreach (var pair in ActiveModelInventory.DictionaryPairs(surface.GetClassRecord("_elementFaces")))
                {
                    int face = pair.GetClassRecord("key")?.GetInt32("value__")
                        ?? throw new InvalidDataException("Missing surface face number.");
                    string setName = pair.GetString("value") ?? throw new InvalidDataException("Missing surface element-set name.");
                    if (!elementSets.TryGetValue(setName, out var set))
                        throw new InvalidDataException("Surface references missing element set.");
                    foreach (int elementId in ElementSetMembers(set))
                    {
                        if (!elements.TryGetValue(elementId, out var element))
                            throw new InvalidDataException("Surface element set references missing element.");
                        int maxFace = element.Cell switch
                        {
                            3 or 21 => 1,
                            5 or 9 or 22 or 23 => 2,
                            10 or 24 => 4,
                            14 or 27 => 5,
                            13 or 26 => 5,
                            12 or 25 => 6,
                            _ => 0
                        };
                        if (face < 1 || face > maxFace)
                            throw new InvalidDataException("Surface face number is invalid for referenced element topology.");
                        surfaceFacesChecked++;
                    }
                }
        }
        if (analysisMesh)
        {
            var unsupported = cells.Where(entry => entry.Key is not (10 or 12 or 24 or 25 or 26)).ToArray();
            if (unsupported.Length != 0)
                ConversionBlockers.Add(new("mesh.topology", unsupported.Sum(entry => entry.Value),
                    "Current PMA metadata supports linear tetrahedra/hexahedra and quadratic tetrahedra/wedges/hexahedra; found " +
                    string.Join(", ", unsupported.Select(entry => $"VTK {entry.Key}: {entry.Value}")) + "."));
        }
        return new { setsChecked, partMembershipsChecked, surfaceFacesChecked, formulationsChecked, formulationsUnchecked, partMetadataAvailable = declaredParts != null, nodes = nodes.Count, elements = elements.Count, vtkCellCounts = cells,
            elementPartIds = partIds.Order().ToArray(), visualizationParts = parts };
    }

    static int[] Labels(ClassRecord record)
    {
        string field = record.HasMember("_labels") ? "_labels" : record.HasMember("FeGroup+_labels") ? "FeGroup+_labels" :
            record.HasMember("BasePart+_labels") ? "BasePart+_labels" : throw new InvalidDataException("Missing labels.");
        if (record.GetArrayRecord(field) is not SZArrayRecord<int> labels || labels.Length > 10000000)
            throw new InvalidDataException("Unsupported labels or more than 10000000 entries.");
        return labels.GetArray();
    }
    public object Read(ClassRecord? model)
    {
        var geometry = Mesh(model?.GetClassRecord("_geometry"), false);
        var mesh = Mesh(model?.GetClassRecord("_mesh"), true);
        if (reader.BaseStream.ReadByte() != -1) throw new InvalidDataException("Unexpected trailing model binary data.");
        return new { geometry, mesh, validation = "Finite coordinates, unique positive IDs and element-to-node references; visualization arrays structurally consumed. Available sets, exact part membership, surface face numbers and quadratic solid formulation availability checked where metadata exists. Element quality and analysis-region contents not validated." };
    }
}
