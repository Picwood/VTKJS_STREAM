using System.Formats.Nrbf;

internal static class CoordinateSystemInventory
{
    const int MaxCoordinateSystems = 1000;

    internal static object Inspect(ClassRecord model)
    {
        var mesh = RecordOrNull(model, "_mesh");
        var systems = ActiveModelInventory.DictionaryItems(mesh == null ? null : RecordOrNull(mesh, "_coordinateSystems"))
            .Take(MaxCoordinateSystems + 1).ToArray();
        if (systems.Length > MaxCoordinateSystems)
            throw new InvalidDataException("More than 1000 coordinate systems are not supported.");

        var definitions = systems.Select(record => new
        {
            name = Text(record, "_name"),
            active = Bool(record, "_active", true),
            type = EnumNumber(record, "_type"),
            twoD = Bool(record, "_twoD", false),
            center = Point(record, "_x1", "_y1", "_z1"),
            pointX = Point(record, "_x2", "_y2", "_z2"),
            pointXY = Point(record, "_x3", "_y3", "_z3"),
            centerCreatedFrom = EnumNumber(record, "_centerCreatedFrom"),
            pointXCreatedFrom = EnumNumber(record, "_pointXCreatedFrom"),
            pointXYCreatedFrom = EnumNumber(record, "_pointXYCreatedFrom"),
            centerCreationIds = IntArray(record, "_centerCreationIds"),
            pointXCreationIds = IntArray(record, "_pointXCreationIds"),
            pointXYCreationIds = IntArray(record, "_pointXYCreationIds"),
            hasCenterSelection = RecordOrNull(record, "_centerCreationData") != null,
            hasPointXSelection = RecordOrNull(record, "_pointXCreationData") != null,
            hasPointXYSelection = RecordOrNull(record, "_pointXYCreationData") != null
        }).ToArray();

        var references = new List<object>();
        var stepCollection = Record(model, "_stepCollection");
        foreach (var step in ActiveModelInventory.ListItems(Record(stepCollection, "_steps")))
        {
            string stepName = TextOrNull(step, "_name") ?? "";
            AddReferences(references, stepName, "boundary", RecordOrNull(step, "_boundaryConditions"));
            AddReferences(references, stepName, "load", RecordOrNull(step, "_loads"));
        }
        return new { definitions, references };
    }

    static void AddReferences(List<object> references, string step, string category, ClassRecord? dictionary)
    {
        foreach (var item in ActiveModelInventory.DictionaryItems(dictionary))
        {
            string? name = TextOrNull(item, "_coordinateSystemName");
            if (!string.IsNullOrEmpty(name))
                references.Add(new { step, category, definition = Text(item, "_name"), type = item.TypeName.FullName,
                    coordinateSystem = name, active = Bool(item, "_active", true) });
        }
    }

    static object[] Point(ClassRecord record, string x, string y, string z) =>
        [Equation(record, x), Equation(record, y), Equation(record, z)];

    static object Equation(ClassRecord owner, string field)
    {
        var equation = Record(owner, field);
        if (equation.TypeName.FullName != "CaeGlobals.EquationContainer")
            throw new InvalidDataException("Expected an equation-container record.");
        bool constant = equation.GetBoolean(Field(equation, "_constant"));
        return constant
            ? new { constant, value = (object)equation.GetDouble(Field(equation, "_constantValue")) }
            : new { constant, value = (object)(equation.GetString(Field(equation, "_equation")) ??
                throw new InvalidDataException("Missing coordinate-system equation.")) };
    }

    static int EnumNumber(ClassRecord owner, string field) => Record(owner, field).GetInt32("value__");

    static int[]? IntArray(ClassRecord record, string field)
    {
        object? raw = record.GetRawValue(Field(record, field));
        if (raw == null) return null;
        if (raw is SZArrayRecord<int> values && values.Length <= 16) return values.GetArray();
        throw new InvalidDataException("Invalid coordinate-system creation IDs.");
    }

    static string Field(ClassRecord record, string name)
    {
        if (record.HasMember(name)) return name;
        string[] matches = record.MemberNames.Where(member => member.EndsWith("+" + name, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidDataException("Missing or ambiguous record field: " + name);
    }
    static ClassRecord Record(ClassRecord owner, string field) => owner.GetClassRecord(Field(owner, field))
        ?? throw new InvalidDataException("Missing record: " + field);
    static ClassRecord? RecordOrNull(ClassRecord owner, string field)
    {
        if (!owner.MemberNames.Any(name => name == field || name.EndsWith("+" + field, StringComparison.Ordinal))) return null;
        return owner.GetClassRecord(Field(owner, field));
    }
    static string Text(ClassRecord record, string field) => record.GetString(Field(record, field))
        ?? throw new InvalidDataException("Missing text field: " + field);
    static string? TextOrNull(ClassRecord record, string field) =>
        record.MemberNames.Any(name => name == field || name.EndsWith("+" + field, StringComparison.Ordinal))
            ? record.GetString(Field(record, field)) : null;
    static bool Bool(ClassRecord record, string field, bool fallback) =>
        record.MemberNames.Any(name => name == field || name.EndsWith("+" + field, StringComparison.Ordinal))
            ? record.GetBoolean(Field(record, field)) : fallback;
}
