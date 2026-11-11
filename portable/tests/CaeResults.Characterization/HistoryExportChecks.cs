using CaeResults;
using System.Globalization;
using Microsoft.VisualBasic.FileIO;

internal static class HistoryExportChecks
{
    public static void Run(string directory, Action<bool, string> check)
    {
        string dat = Path.Combine(directory, "export.dat");
        File.WriteAllText(dat, "\n displacements (vx,vy,vz) for set RIGHT and time  0.5000000E+00\n\n 2 1.000000E-03 0.000000E+00 -2.500000E-04\n\n");
        var history = DatFileReader.Read(dat, out _);
        var results = new FeResults("export", null);
        results.SetHistory(history);
        var set = history.Sets.Values.Single();
        string oldName = set.Name;
        set.Name = "RIGHT,\"quoted\";é\nsecond line";
        history.Sets.Replace(oldName, set.Name, set);
        foreach (string delimiter in new[] { ",", ";", "\t" })
        {
            string file = Path.Combine(directory, "history-export.txt");
            ResultHistoryOutputWriter.Write(file, history.Sets.Keys.ToArray(), delimiter, results);
            using var parser = new TextFieldParser(file);
            parser.SetDelimiters(delimiter);
            parser.HasFieldsEnclosedInQuotes = true;
            var rows = new List<string[]>();
            while (!parser.EndOfData) rows.Add(parser.ReadFields());
            var titles = rows.Where(row => row[0] == "History output component").ToArray();
            check(titles.Length == 3 && titles.All(row => row.Length == 2 && row[1].StartsWith(set.Name + ".")),
                  "history export escapes delimiter and quotes in component title");
            var samples = rows.Where(row => row[0] == "0.5").ToArray();
            check(samples.Length == 3 && samples.All(row => row.Length == 2), "history export uses invariant numeric columns");
            var values = samples.Select(row => double.Parse(row[1], NumberStyles.Float, CultureInfo.InvariantCulture)).Order().ToArray();
            check(values.SequenceEqual(new[] { -.00025, 0, .001 }), "history export preserves displacement values");
        }
    }

    public static void NativeCube(string dat)
    {
        var history = DatFileReader.Read(dat, out _);
        var results = new FeResults("native", null);
        results.SetHistory(history);
        string file = Path.ChangeExtension(dat, ".history.csv");
        ResultHistoryOutputWriter.Write(file, history.Sets.Keys.ToArray(), ",", results);
        using var parser = new TextFieldParser(file);
        parser.SetDelimiters(",");
        bool axial = false;
        int checkedRows = 0;
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields();
            if (row[0] == "History output component") axial = row[1].EndsWith(".U1", StringComparison.Ordinal);
            else if (axial && row[0] == "1")
            {
                if (row.Length != 5 || row.Skip(1).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).Any(x => !double.IsFinite(x) || Math.Abs(x - .001) > 1e-8))
                    throw new Exception("Native history export mismatch.");
                checkedRows++;
            }
        }
        if (checkedRows != 1) throw new Exception("Native history export has no unique axial row.");
    }
}
