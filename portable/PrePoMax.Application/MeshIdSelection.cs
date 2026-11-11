using System.Globalization;

namespace PrePoMax.Application;

internal static class MeshIdSelection
{
    internal const int MaxExpression = 60000;
    internal static int[] Parse(string expression, string entity)
    {
        if (expression.Length > MaxExpression) throw new ArgumentException("Use at most 60000 characters of IDs/ranges.");
        var ids = new HashSet<int>();
        int expanded = 0;
        int Positive(string token) => int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value : throw new ArgumentException("Use positive " + entity + " IDs or ascending START:END[:STEP] ranges.");
        foreach (string token in expression.Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] range = token.Split(':');
            if (range.Length is < 1 or > 3) throw new ArgumentException("Use ID or START:END[:STEP].");
            int start = Positive(range[0]), end = range.Length == 1 ? start : Positive(range[1]);
            int step = range.Length == 3 ? Positive(range[2]) : 1;
            if (end < start) throw new ArgumentException("ID ranges must be ascending.");
            long count = ((long)end - start) / step + 1;
            if (count > 100000 - expanded) throw new ArgumentException("Select at most 100000 expanded IDs.");
            expanded += (int)count;
            for (long id = start; id <= end; id += step) ids.Add((int)id);
        }
        return ids.Order().ToArray();
    }

    internal static string Format(int[] labels)
    {
        int[] ids = labels.Distinct().Order().ToArray();
        var spans = new List<string>();
        for (int i = 0; i < ids.Length; i++)
        {
            int start = ids[i], end = start;
            while (i + 1 < ids.Length && (long)ids[i + 1] == (long)end + 1) end = ids[++i];
            spans.Add(start == end ? start.ToString(CultureInfo.InvariantCulture) : $"{start}:{end}");
        }
        return string.Join(", ", spans);
    }
}
