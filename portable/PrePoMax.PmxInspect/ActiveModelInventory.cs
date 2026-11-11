using System.Formats.Nrbf;

internal static class ActiveModelInventory
{
    // Only explicitly selected model collections are followed; never controller history.
    public static object Inspect(SerializationRecord root)
    {
        if (root is not SZArrayRecord<SerializationRecord> roots || roots.Length > 16)
            return new { status = "unrecognized-root" };
        var controller = roots.GetArray().FirstOrDefault() as ClassRecord;
        if (controller?.TypeName.FullName != "PrePoMax.Controller" || !controller.HasMember("_model"))
            return new { status = "missing-controller-model" };
        var model = controller.GetClassRecord("_model");
        if (model?.TypeName.FullName != "CaeModel.FeModel")
            return new { status = "unrecognized-model" };
        var collections = new Dictionary<string, object>();
        foreach (string name in new[] { "_materials", "_sections", "_constraints", "_surfaceInteractions", "_contactPairs", "_amplitudes", "_initialConditions" })
            collections[name] = DictionaryItems(model.GetClassRecord(name)).Select(Describe).ToArray();
        var stepCollection = model.GetClassRecord("_stepCollection") ?? throw new InvalidDataException("Missing step collection.");
        var steps = ListItems(stepCollection.GetClassRecord("_steps")).Select(step => new {
            definition = Describe(step),
            nonlinearGeometry = step.HasMember("_nlgeom") ? step.GetBoolean("_nlgeom") : (bool?)null,
            loads = DictionaryItems(step.GetClassRecord("_loads")).Select(Describe).ToArray(),
            boundaries = DictionaryItems(step.GetClassRecord("_boundaryConditions")).Select(Describe).ToArray()
        }).ToArray();
        return new { status = "current-model-collections", collections, steps,
            scope = "Current collection membership, including inactive definitions; excludes command history. Not conversion eligibility." };
    }

    static object Describe(ClassRecord record) => new {
        type = record.TypeName.FullName,
        active = record.HasMember("_active") ? record.GetBoolean("_active") : (bool?)null,
        properties = record.TypeName.FullName is "CaeModel.Material" or "CaeModel.SurfaceInteraction"
            ? ListItems(record.GetClassRecord("_properties")).Select(p => p.TypeName.FullName).ToArray() : null
    };

    static ClassRecord?[] ArrayItems(ArrayRecord array)
    {
        if (array.Rank != 1 || array.Lengths[0] > 100000)
            throw new InvalidDataException("Unsupported collection array or more than 100000 entries.");
        if (array is SZArrayRecord<ClassRecord> classes) return classes.GetArray();
        if (array is SZArrayRecord<SerializationRecord> records)
            return records.GetArray().Select(r => r == null ? null : r as ClassRecord
                ?? throw new InvalidDataException("Non-class collection entry.")).ToArray();
        throw new InvalidDataException("Unsupported collection array.");
    }

    internal static IEnumerable<ClassRecord> ListItems(ClassRecord? list)
    {
        if (list == null) yield break;
        if (!list.TypeName.FullName.StartsWith("System.Collections.Generic.List`", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported list layout.");
        int size = list.GetInt32("_size");
        var array = list.GetArrayRecord("_items") ?? throw new InvalidDataException("Missing list items.");
        if (size < 0 || size > 100000) throw new InvalidDataException("Invalid collection size.");
        var items = ArrayItems(array);
        if (size > items.Length) throw new InvalidDataException("Collection size exceeds storage.");
        for (int i = 0; i < size; i++) yield return items[i] ?? throw new InvalidDataException("Null collection entry.");
    }

    internal static IEnumerable<ClassRecord> DictionaryItems(ClassRecord? dictionary) =>
        DictionaryPairs(dictionary).Select(pair => pair.GetClassRecord("value") ?? throw new InvalidDataException("Null dictionary value."));

    internal static IEnumerable<ClassRecord> DictionaryPairs(ClassRecord? dictionary)
    {
        if (dictionary == null) yield break;
        if (dictionary.TypeName.FullName.StartsWith("CaeGlobals.OrderedDictionary`", StringComparison.Ordinal))
            dictionary = dictionary.GetClassRecord("_dictionary") ?? throw new InvalidDataException("Missing ordered dictionary storage.");
        if (!dictionary.TypeName.FullName.StartsWith("System.Collections.Generic.Dictionary`", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported dictionary layout.");
        if (!dictionary.HasMember("KeyValuePairs"))
        {
            if (!dictionary.HasMember("HashSize") || dictionary.GetInt32("HashSize") != 0)
                throw new InvalidDataException("Missing dictionary entries.");
            yield break;
        }
        var pairs = dictionary.GetArrayRecord("KeyValuePairs");
        if (pairs == null) yield break;
        foreach (var pair in ArrayItems(pairs))
            yield return pair ?? throw new InvalidDataException("Null dictionary pair.");
    }
}
