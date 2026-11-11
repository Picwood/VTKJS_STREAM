using System.Globalization;

int checks = 0;
foreach (string culture in new[] { "en-US", "it-IT" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    EquationChecks.Run((passed, name) => {
        if (!passed) throw new Exception("Legacy mismatch: " + name);
        checks++;
    });
}
Console.WriteLine($"PASS: {checks} legacy NCalc compatibility checks.");
