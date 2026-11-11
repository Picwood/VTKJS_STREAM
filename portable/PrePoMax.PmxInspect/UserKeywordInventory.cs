using System.Formats.Nrbf;

internal static class UserKeywordInventory
{
    const int MaxKeywords = 1000;
    const int MaxTextLength = 1024 * 1024;
    internal sealed record Entry(int[] Path, ClassRecord Keyword);

    internal static object[] Inspect(ClassRecord model)
    {
        return Read(model).Select((entry, order) => Describe(entry, order)).ToArray();
    }

    internal static Entry[] Read(ClassRecord model)
    {
        var dictionary = model.HasMember("_calculixUserKeywords")
            ? model.GetClassRecord("_calculixUserKeywords") : null;
        if (dictionary == null) return [];
        var entries = ActiveModelInventory.DictionaryPairs(dictionary).Select(pair =>
        {
            if (pair.GetArrayRecord("key") is not SZArrayRecord<int> key)
                throw new InvalidDataException("Invalid CalculiX user-keyword insertion path.");
            return Validate(key.GetArray(), pair.GetClassRecord("value") ??
                throw new InvalidDataException("Missing CalculiX user-keyword value."));
        }).ToArray();
        if (entries.Length > MaxKeywords)
            throw new InvalidDataException($"More than {MaxKeywords} CalculiX user keywords.");
        if (!dictionary.TypeName.FullName.StartsWith("CaeGlobals.OrderedDictionary`", StringComparison.Ordinal) ||
            !dictionary.HasMember("_list")) return entries;

        var list = dictionary.GetClassRecord("_list") ?? throw new InvalidDataException("Missing user-keyword order list.");
        int size = list.GetInt32("_size");
        if (size != entries.Length) throw new InvalidDataException("User-keyword order does not match dictionary entries.");
        var storage = list.GetArrayRecord("_items") ?? throw new InvalidDataException("Missing user-keyword order storage.");
        int[][] ordered;
        if (storage.GetType().Name == "JaggedArrayRecord")
        {
            if (storage.Rank != 1 || storage.Lengths[0] < size || storage.Lengths[0] > MaxKeywords)
                throw new InvalidDataException($"Invalid user-keyword order array: rank {storage.Rank}, lengths [{string.Join(',', storage.Lengths.ToArray())}], entries {entries.Length}.");
            var values = storage.GetArray(typeof(int[][]), allowNulls: true);
            ordered = Enumerable.Range(0, size).Select(index => values.GetValue(index) is SZArrayRecord<int> key
                ? key.GetArray() : throw new InvalidDataException("Invalid ordered user-keyword path.")).ToArray();
        }
        else
        {
            SerializationRecord?[] records = storage switch
            {
                SZArrayRecord<SerializationRecord> values => values.GetArray(),
                SZArrayRecord<ArrayRecord> values => values.GetArray(),
                _ => throw new InvalidDataException("Unsupported user-keyword order storage: " + storage.GetType().FullName)
            };
            ordered = records.Take(size).Select(record => record is SZArrayRecord<int> key
                ? key.GetArray() : throw new InvalidDataException("Invalid ordered user-keyword path.")).ToArray();
        }
        if (ordered.Select(Key).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new InvalidDataException("Duplicate CalculiX user-keyword insertion path.");
        var byPath = entries.ToDictionary(entry => Key(entry.Path), StringComparer.Ordinal);
        if (ordered.Any(path => !byPath.ContainsKey(Key(path))))
            throw new InvalidDataException("User-keyword order references an unknown path.");
        return ordered.Select(path => byPath[Key(path)]).ToArray();
    }

    static Entry Validate(int[] path, ClassRecord keyword)
    {
        if (path.Length == 0 || path.Length > 64 || path.Any(index => index < 0))
            throw new InvalidDataException("Invalid CalculiX user-keyword insertion path.");
        if (keyword.TypeName.FullName != "FileInOut.Output.Calculix.CalculixUserKeyword")
            throw new InvalidDataException("Unexpected CalculiX user-keyword type.");
        return new Entry(path, keyword);
    }

    static string Key(int[] path) => string.Join(',', path);

    static object Describe(Entry entry, int order)
    {
        var keyword = entry.Keyword;
        string data = keyword.GetString("_data") ?? "";
        if (data.Length > MaxTextLength)
            throw new InvalidDataException("CalculiX user-keyword text exceeds 1 MiB.");
        return new {
            order,
            path = entry.Path,
            active = !keyword.HasMember("_active") || keyword.GetBoolean("_active"),
            suppressed = keyword.HasMember("_suppressed") && keyword.GetBoolean("_suppressed"),
            data
        };
    }
}
