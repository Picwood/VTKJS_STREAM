using CaeResults;
using CaeGlobals;

internal static class CloudReaderChecks
{
    public static void Run(string directory, Action<bool, string> check)
    {
        string path = Path.Combine(directory, "cloud é.txt");
        foreach (string separator in new[] { " ", ",", ";", ":", "\t" })
        {
            File.WriteAllText(path, "# coordinates and two components\n\n" +
                string.Join(separator, new[] { "0.25", "0", "0", "-1.5E-3", "4" }) + "\n" +
                string.Join(separator, new[] { "1.25", "0", "0", "2.5E-3", "4" }) + "\n");
            var points = CloudPointReader.Read(path);
            check(points.Length == 2 && points[0].Coor[0] == .25 && points[1].Coor[0] == 1.25 &&
                  points[0].Values[0] == -.0015 && points[1].Values[0] == .0025,
                  "cloud file parses invariant numbers and supported separator");
            var cloud = new CloudInterpolator(points);
            cloud.InterpolateAt(new[] { .75, 0, 0 }, CloudInterpolatorEnum.Shepard, 2, out _, out var values);
            check(Math.Abs(values[0] - .0005) < 1e-12 && values[1] == 4,
                  "imported cloud interpolates two components");
        }
        File.WriteAllText(path, "0 0 0 1\n1 0 0 2 3\n");
        bool rejected = false;
        try { CloudPointReader.Read(path); }
        catch (CaeException) { rejected = true; }
        check(rejected, "cloud reader rejects inconsistent component counts");
        check(CloudPointReader.Read(Path.Combine(directory, "missing-cloud.txt")) == null,
              "cloud reader missing-file contract");
    }
}
