using CaeResults;
using PrePoMax.Persistence;

namespace PrePoMax.Application;

public sealed record HistorySeriesSummary(string Set, string Field, string Component, string Unit,
    string[] Entries, int Samples, double? MinimumTime, double? MaximumTime);
public sealed record HistoryResultCatalog(HistorySeriesSummary[] Series, string[] Diagnostics);
public sealed record HistoryResultTable(string Set, string Field, string Component, string[] Columns,
    object?[][] Rows);

/// <summary>Production DAT/binary history browsing and CSV export for portable result archives.</summary>
public static class ProjectHistoryResults
{
    private const int MaxSeries = 100_000;
    private const int MaxEntriesPerSeries = 100_000;
    private const long MaxSamples = 5_000_000;
    private const long MaxTableCells = 5_000_000;

    public static HistoryResultCatalog List(string path)
    {
        var loaded = Load(path);
        var summaries = new List<HistorySeriesSummary>();
        long totalSamples = 0;
        var history = loaded.Results.GetHistory();
        if (history == null) return new([], loaded.Diagnostics);
        foreach (var set in history.Sets)
        foreach (var field in set.Value.Fields)
        foreach (var component in field.Value.Components)
        {
            ValidateComponent(component.Value, ref totalSamples);
            if (++loaded.SeriesCount > MaxSeries) throw new InvalidDataException("History result contains too many series.");
            double[] times = component.Value.Entries.Values.SelectMany(entry => entry.Time).ToArray();
            summaries.Add(new(set.Key, field.Key, component.Key, component.Value.Unit ?? "",
                component.Value.Entries.Keys.ToArray(), component.Value.Entries.Values.Sum(entry => entry.Values.Count),
                times.Length == 0 ? null : times.Min(), times.Length == 0 ? null : times.Max()));
        }
        return new(summaries.ToArray(), loaded.Diagnostics);
    }

    public static HistoryResultTable Read(string path, string setName, string fieldName, string componentName)
    {
        var loaded = Load(path);
        var history = loaded.Results.GetHistory();
        if (history == null || !history.Sets.TryGetValue(setName, out var set) ||
            !set.Fields.TryGetValue(fieldName, out var field) ||
            !field.Components.TryGetValue(componentName, out var component))
            throw new ArgumentException("Select an existing history result series.");
        long samples = 0;
        ValidateComponent(component, ref samples);
        long rows = component.Entries.Values.SelectMany(entry => entry.Time).Distinct().LongCount();
        if (rows * (component.Entries.Count + 1L) > MaxTableCells)
            throw new NotSupportedException("The selected history table exceeds 5,000,000 cells.");
        loaded.Results.GetHistoryOutputData(new HistoryResultData(setName, fieldName, componentName),
            out string[] columns, out object[][] values, false);
        var table = values.Select(row => row.Select(value => (object?)value).ToArray()).ToArray();
        return new(setName, fieldName, componentName,
            columns.Select(column => column.Replace(Environment.NewLine, " — ")).ToArray(), table);
    }

    public static void Export(string source, string destination, string[]? setNames = null, string delimiter = ",")
    {
        if (delimiter is not ("," or ";" or "\t")) throw new ArgumentException("Delimiter must be comma, semicolon or tab.");
        var loaded = Load(source);
        var history = loaded.Results.GetHistory();
        if (history == null || history.Sets.Count == 0) throw new InvalidDataException("Project contains no history results.");
        string[] selected = setNames is not { Length: > 0 } ? history.Sets.Keys.ToArray() :
            setNames.Distinct(StringComparer.Ordinal).ToArray();
        if (selected.Length == 0 || selected.Length > history.Sets.Count || selected.Any(name => !history.Sets.ContainsKey(name)))
            throw new ArgumentException("Select existing history result sets.");
        long samples = 0, cells = 0;
        foreach (string name in selected)
        foreach (var field in history.Sets[name].Fields.Values)
        foreach (var component in field.Components.Values)
        {
            ValidateComponent(component, ref samples);
            long rows = component.Entries.Values.SelectMany(entry => entry.Time).Distinct().LongCount();
            cells += rows * (component.Entries.Count + 1L);
            if (cells > MaxTableCells) throw new NotSupportedException("The selected history export exceeds 5,000,000 cells.");
        }
        string target = Path.GetFullPath(destination);
        string? directory = Path.GetDirectoryName(target);
        if (directory == null || !Directory.Exists(directory)) throw new DirectoryNotFoundException("Export directory does not exist.");
        string temporary = Path.Combine(directory, ".history-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            ResultHistoryOutputWriter.Write(temporary, selected, delimiter, loaded.Results);
            File.Move(temporary, target, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class Loaded(FeResults results, string[] diagnostics)
    {
        public FeResults Results { get; } = results;
        public string[] Diagnostics { get; } = diagnostics;
        public int SeriesCount { get; set; }
    }

    private static Loaded Load(string path)
    {
        var project = StaticProjectArchive.Open(path);
        FeResults? results = ProjectResults.Load(path, default, out _);
        results ??= new FeResults(project.Model.Name, project.Model.UnitSystem);
        string[] diagnostics = [];
        if (project.Dat is { Length: > 0 } dat)
        {
            string directory = Path.Combine(Path.GetTempPath(), "prepomax-history-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string datPath = Path.Combine(directory, "results.dat");
                File.WriteAllBytes(datPath, dat);
                var history = DatFileReader.Read(datPath, out diagnostics);
                if (history == null) throw new InvalidDataException("DAT history results could not be read.");
                results.SetHistory(history);
            }
            finally { Directory.Delete(directory, true); }
        }
        return new(results, diagnostics);
    }

    private static void ValidateComponent(HistoryResultComponent component, ref long totalSamples)
    {
        if (component.Entries.Count == 0 || component.Entries.Count > MaxEntriesPerSeries)
            throw new InvalidDataException("History result series has an invalid entry count.");
        foreach (var entry in component.Entries.Values)
        {
            if (entry.Time.Count != entry.Values.Count || entry.Time.Count > MaxSamples ||
                entry.Time.Any(value => !double.IsFinite(value)) || entry.Values.Any(value => !double.IsFinite(value)))
                throw new InvalidDataException("History result series contains invalid samples.");
            totalSamples += entry.Values.Count;
            if (totalSamples > MaxSamples) throw new InvalidDataException("History result exceeds 5,000,000 samples.");
        }
    }
}
