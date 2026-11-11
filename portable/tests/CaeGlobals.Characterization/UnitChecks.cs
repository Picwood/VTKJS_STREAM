using CaeGlobals;
using System.ComponentModel;

internal static class UnitChecks
{
    public static void Run(Action<bool, string> check)
    {
        var previous = MyNCalc.ExistingParameters;
        try
        {
            MyNCalc.ExistingParameters = new CaeGlobals.OrderedDictionary<string, object>("units");
            var si = new UnitSystem(UnitSystemType.M_KG_S_C);
            Near(StringLengthConverter.ConvertToCurrentUnits("1000 mm"), 1, "SI length");
            Near(StringPressureConverter.ConvertToCurrentUnits("1 MPa"), 1000000, "SI pressure");
            Near(StringTemperatureConverter.ConvertToCurrentUnits("32 °F"), 0, "temperature offset");
            Near(si.AbsoluteZero, -273.15, "SI absolute zero");

            var mm = new UnitSystem(UnitSystemType.MM_TON_S_C);
            Near(StringLengthConverter.ConvertToCurrentUnits("1 m"), 1000, "millimetre length");
            Near(StringLengthConverter.ConvertToCurrentUnits("1 in"), 25.4, "inch to millimetre");
            Near(StringPressureConverter.ConvertToCurrentUnits("1 MPa"), 1, "millimetre pressure");
            Near(StringMassConverter.ConvertToCurrentUnits("1000 kg"), 1, "tonne mass");
            Near(si.Convert(1, new StringLengthConverter(), mm), 1000, "unit-system conversion");
            mm.SetConverterUnits();

            var length = new StringLengthConverter();
            Near((double)length.ConvertFrom("=2*3"), 6, "equation-valued length");
            string formatted = (string)length.ConvertTo(25.4, typeof(string));
            Near((double)length.ConvertFrom(formatted), 25.4, "localized unit round trip");
            int changed = 0;
            var equation = new EquationContainer(typeof(StringLengthConverter), 1);
            equation.EquationChanged = () => changed++;
            equation.SetEquation("= 2 * 3 ; 6 mm", true);
            check(equation.IsEquation() && equation.String == "=2*3" && changed == 1, "equation normalization and callback");
            Near(equation.Value, 6, "equation container value");
            equation.CheckValue = value => Math.Max(0, value);
            equation.SetEquation("-1", true);
            Near(equation.Value, 0, "equation validation clamp");
            check(!equation.IsEquation(), "clamped equation becomes numeric");
            bool rejected = false;
            try { equation.SetEquation("=Sqrt(-1)"); } catch (CaeException) { rejected = true; }
            check(rejected, "nonfinite equation rejection");

            var imperial = new UnitSystem(UnitSystemType.IN_LB_S_F);
            Near(StringLengthConverter.ConvertToCurrentUnits("1 ft"), 12, "imperial length");
            Near(StringTemperatureConverter.ConvertToCurrentUnits("100 °C"), 212, "imperial temperature");
            Near(imperial.AbsoluteZero, -459.67, "imperial absolute zero");
            foreach (UnitSystemType system in Enum.GetValues<UnitSystemType>())
            {
                var units = new UnitSystem(system);
                check(units.UnitSystemType == system, "unit-system initialization " + system);
            }
        }
        finally
        {
            MyNCalc.ExistingParameters = previous;
            new UnitSystem(UnitSystemType.Undefined);
        }
        void Near(double actual, double expected, string name) =>
            check(Math.Abs(actual - expected) <= 1e-9 * Math.Max(1, Math.Abs(expected)), name);
    }
}
