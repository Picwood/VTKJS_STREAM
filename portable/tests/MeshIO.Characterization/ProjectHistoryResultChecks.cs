using System.Security.Cryptography;
using System.Text;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class ProjectHistoryResultChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "history result browsing é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            string dat = """

             displacements (vx,vy,vz) for set RIGHT and time  0.5000000E+00

             2 1.000000E-03 0.000000E+00 -2.500000E-04
             3 2.000000E-03 0.000000E+00 -5.000000E-04

             displacements (vx,vy,vz) for set RIGHT and time  0.1000000E+01

             2 3.000000E-03 0.000000E+00 -7.500000E-04
             3 4.000000E-03 0.000000E+00 -1.000000E-03

            """;
            string source = Path.Combine(root, "result.pma");
            StaticProjectArchive.Save(source, model, dat: Encoding.UTF8.GetBytes(dat));
            byte[] before = SHA256.HashData(File.ReadAllBytes(source));

            var catalog = ProjectHistoryResults.List(source);
            var axial = catalog.Series.Single(series => series.Component == "U1");
            check(catalog.Diagnostics.Length == 0 && axial.Set == "RIGHT" && axial.Entries.SequenceEqual(new[] { "2", "3" }) &&
                  axial.Samples == 4 && axial.MinimumTime == .5 && axial.MaximumTime == 1,
                "history catalog exposes DAT set, field/component hierarchy, entries and time extent");
            var table = ProjectHistoryResults.Read(source, axial.Set, axial.Field, axial.Component);
            check(table.Columns.Length == 3 && table.Rows.Length == 2 &&
                  Convert.ToDouble(table.Rows[0][0]) == .5 && Convert.ToDouble(table.Rows[0][1]) == .001 &&
                  Convert.ToDouble(table.Rows[1][2]) == .004,
                "history series table aligns entry values by sorted time using the production result model");

            string csv = Path.Combine(root, "history.csv");
            ProjectHistoryResults.Export(source, csv, [axial.Set]);
            string exported = File.ReadAllText(csv);
            check(exported.Contains("History output component,RIGHT.") && exported.Contains("0.5,0.001,0.002") &&
                  exported.Contains("1,0.003,0.004"),
                "history CSV export uses the production writer and invariant numeric values");
            string semicolon = Path.Combine(root, "history-semicolon.csv");
            ProjectHistoryResults.Export(source, semicolon, delimiter: ";");
            check(File.ReadAllText(semicolon).Contains("History output component;RIGHT."),
                "history export supports the original comma, semicolon and tab delimiter contract");
            bool overwrite = false;
            try { ProjectHistoryResults.Export(source, csv); } catch (IOException) { overwrite = true; }
            check(overwrite && SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(before),
                "history export refuses overwrite and preserves the source archive");
            foreach (Action invalid in new Action[]
            {
                () => ProjectHistoryResults.Read(source, "missing", axial.Field, axial.Component),
                () => ProjectHistoryResults.Export(source, Path.Combine(root, "bad.csv"), ["missing"]),
                () => ProjectHistoryResults.Export(source, Path.Combine(root, "bad.csv"), delimiter: "|")
            })
            {
                bool rejected = false; try { invalid(); } catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "bad.csv")),
                    "history browsing/export rejects unknown selections and delimiters before publication");
            }
            string empty = Path.Combine(root, "empty.pma");
            StaticProjectArchive.Save(empty, new StaticModelWorkflow(1).Model);
            check(ProjectHistoryResults.List(empty).Series.Length == 0,
                "a project without DAT or binary histories reports an empty history catalog");
            bool emptyExport = false;
            try { ProjectHistoryResults.Export(empty, Path.Combine(root, "empty.csv")); }
            catch (InvalidDataException) { emptyExport = true; }
            check(emptyExport && !File.Exists(Path.Combine(root, "empty.csv")),
                "history export refuses a project without history results");
            check(!Directory.EnumerateFiles(root, ".history-*.tmp").Any(),
                "history export success and failure paths leave no staging files");
        }
        finally { Directory.Delete(root, true); }
    }
}
