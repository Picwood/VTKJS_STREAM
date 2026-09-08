using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using vtkControl;

namespace PrePoMax.Rendering
{
    /// <summary>
    /// Builds the small, serializable lookup table consumed by the 3MF writer without creating
    /// or querying an ActiViz lookup table. Control points intentionally mirror vtkControl's
    /// established palettes; interpolation is performed in managed RGB, HSV, CIE Lab, or
    /// Moreland Msh diverging space.
    /// </summary>
    internal static class VtkJsExportLookupTable
    {
        private enum ColorSpace { Rgb, Hsv, Lab, Diverging }

        public static vtkMaxLookupTable Create(vtkMaxColorSpectrum source, double scalarMinimum,
                                               double scalarMaximum)
        {
            vtkMaxColorSpectrum spectrum = source == null ? new vtkMaxColorSpectrum() : source;
            if (!IsFinite(scalarMinimum) || !IsFinite(scalarMaximum))
            {
                scalarMinimum = 0;
                scalarMaximum = 1;
            }
            if (scalarMaximum < scalarMinimum)
            {
                double swap = scalarMinimum;
                scalarMinimum = scalarMaximum;
                scalarMaximum = swap;
            }

            double minimum = scalarMinimum;
            double maximum = scalarMaximum;
            bool addMinimumColor = false;
            bool addMaximumColor = false;
            if (spectrum.MinMaxType == vtkColorSpectrumMinMaxType.Manual)
            {
                minimum = Math.Min(scalarMinimum, spectrum.MinUserValue);
                maximum = Math.Max(scalarMaximum, spectrum.MaxUserValue);
                if (spectrum.MinUserValue > minimum && maximum != minimum)
                {
                    addMinimumColor = true;
                    minimum = spectrum.MinUserValue;
                }
                if (spectrum.MaxUserValue < maximum && maximum != minimum)
                {
                    addMaximumColor = true;
                    maximum = spectrum.MaxUserValue;
                }
            }

            int baseColorCount = Math.Max(2, spectrum.NumberOfColors);
            var colors = new List<Color>(baseColorCount + 2);
            if (addMinimumColor) colors.Add(spectrum.MinColor);
            Palette palette = GetPalette(spectrum.Type);
            for (int index = 0; index < baseColorCount; index++)
            {
                double position = index / (double)(baseColorCount - 1);
                if (spectrum.ReverseColors) position = 1 - position;
                colors.Add(ToColor(Sample(palette, position, spectrum.ColorBrightness)));
            }
            if (addMaximumColor) colors.Add(spectrum.MaxColor);

            if (addMinimumColor || addMaximumColor)
            {
                double delta = (maximum - minimum) / baseColorCount;
                if (addMinimumColor) minimum -= delta;
                if (addMaximumColor) maximum += delta;
            }
            return new vtkMaxLookupTable(minimum, maximum, colors.ToArray());
        }

        private static Palette GetPalette(vtkColorSpectrumType type)
        {
            switch (type)
            {
                case vtkColorSpectrumType.CoolWarm:
                    return new Palette(ColorSpace.Diverging, new[] { P(0, .230, .299, .754), P(1, .706, .016, .150) });
                case vtkColorSpectrumType.Rainbow:
                    return new Palette(ColorSpace.Hsv, new[] { P(0, 0, 0, 1), P(.25, 0, 1, 1),
                        P(.5, 0, 1, 0), P(.75, 1, 1, 0), P(1, 1, 0, 0) });
                case vtkColorSpectrumType.RainbowDesaturated:
                    return new Palette(ColorSpace.Rgb, new[] { P(0, .278, .278, .788), P(.143, .02, .02, .396),
                        P(.286, 0, .871, .918), P(.429, 0, .604, .2), P(.571, 1, .875, 0),
                        P(.714, 1, .329, 0), P(.857, .451, .02, .02), P(1, .878, .302, .302) });
                case vtkColorSpectrumType.Jet:
                    return new Palette(ColorSpace.Rgb, new[] { P(0, 0, 0, .5), P(.125, 0, 0, 1),
                        P(.375, 0, 1, 1), P(.5, 0, 1, 0), P(.625, 1, 1, 0), P(.875, 1, 0, 0), P(1, .5, 0, 0) });
                case vtkColorSpectrumType.Turbo:
                    return new Palette(ColorSpace.Rgb, new[] { P(0, .18995, .07176, .23217), P(.067, .25107, .25237, .63374),
                        P(.133, .27698, .46153, .93309), P(.2, .24427, .60937, .99697), P(.267, .17223, .7168, .93981),
                        P(.333, .10342, .896, .715), P(.4, .2618, .96765, .52981), P(.467, .47375, .99755, .34963),
                        P(.533, .69525, .9761, .21294), P(.6, .88331, .86553, .21719), P(.667, .98, .73, .22161),
                        P(.733, .99297, .55214, .15417), P(.8, .94084, .35566, .07031), P(.867, .84662, .21407, .02487),
                        P(.933, .68602, .09536, .00481), P(1, .4796, .01583, .01055) });
                case vtkColorSpectrumType.Warm:
                    return new Palette(ColorSpace.Rgb, new[] { P(0, .549, .176, .016), P(.143, .8, .298, .008),
                        P(.286, .925, .439, .078), P(.429, .996, .6, .161), P(.571, .996, .769, .31),
                        P(.714, .996, .89, .569), P(.857, 1, .969, .737), P(1, 1, 1, .898) });
                case vtkColorSpectrumType.Cool:
                    return new Palette(ColorSpace.Rgb, new[] { P(0, .031, .271, .58), P(.143, .129, .443, .71),
                        P(.286, .259, .573, .776), P(.429, .42, .682, .839), P(.571, .62, .792, .882),
                        P(.714, .776, .859, .937), P(.857, .871, .922, .969), P(1, .969, .984, 1) });
                case vtkColorSpectrumType.Cividis:
                    return new Palette(ColorSpace.Rgb, new[] { P(0, 0, .125, .297), P(.125, .023, .211, .43),
                        P(.25, .254, .301, .418), P(.375, .375, .391, .43), P(.5, .484, .48, .469),
                        P(.625, .609, .578, .465), P(.75, .738, .684, .43), P(.875, .875, .793, .363), P(1, .996, .91, .27) });
                case vtkColorSpectrumType.Viridis:
                    return new Palette(ColorSpace.Rgb, new[] { P(0, .267, .005, .329), P(.143, .275, .197, .497),
                        P(.286, .213, .359, .552), P(.429, .153, .498, .558), P(.571, .122, .632, .531),
                        P(.714, .29, .759, .428), P(.857, .622, .854, .226), P(1, .993, .906, .144) });
                case vtkColorSpectrumType.Plasma:
                    return new Palette(ColorSpace.Rgb, new[] { P(0, .05, .03, .528), P(.143, .328, .007, .64),
                        P(.286, .545, .038, .647), P(.429, .725, .197, .538), P(.571, .859, .359, .408),
                        P(.714, .956, .534, .285), P(.857, .995, .738, .167), P(1, .94, .975, .131) });
                case vtkColorSpectrumType.BlackBody:
                    return new Palette(ColorSpace.Lab, new[] { P(0, 0, 0, 0), P(.143, .257, .089, .069),
                        P(.286, .502, .123, .107), P(.429, .735, .198, .124), P(.571, .877, .395, .038),
                        P(.714, .911, .632, .1), P(.857, .907, .855, .189), P(1, 1, 1, 1) });
                case vtkColorSpectrumType.Inferno:
                    return new Palette(ColorSpace.Lab, new[] { P(0, .001, 0, .014), P(.143, .159, .044, .329),
                        P(.286, .397, .083, .433), P(.429, .623, .165, .388), P(.571, .831, .283, .259),
                        P(.714, .962, .49, .084), P(.857, .982, .756, .153), P(1, .988, .998, .645) });
                case vtkColorSpectrumType.Kindlmann:
                    return new Palette(ColorSpace.Lab, new[] { P(0, 0, 0, 0), P(.143, .14, .022, .46),
                        P(.286, .028, .243, .587), P(.429, .021, .449, .382), P(.571, .031, .625, .083),
                        P(.714, .44, .767, .037), P(.857, .98, .815, .572), P(1, 1, 1, 1) });
                case vtkColorSpectrumType.Grayscale:
                    return new Palette(ColorSpace.Lab, new[] { P(0, .2, .2, .2), P(1, 1, 1, 1) });
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        private static double[] Sample(Palette palette, double position, double brightness)
        {
            Point upper = palette.Points.First(point => point.Position >= position);
            Point lower = palette.Points.Last(point => point.Position <= position);
            double span = upper.Position - lower.Position;
            double fraction = span == 0 ? 0 : (position - lower.Position) / span;
            double[] a = Brighten(lower.Rgb, brightness);
            double[] b = Brighten(upper.Rgb, brightness);
            if (palette.Space == ColorSpace.Hsv) return HsvToRgb(InterpolateHue(RgbToHsv(a), RgbToHsv(b), fraction));
            if (palette.Space == ColorSpace.Diverging) return InterpolateDiverging(a, b, fraction);
            if (palette.Space == ColorSpace.Lab) return LabToRgb(Interpolate(RgbToLab(a), RgbToLab(b), fraction));
            return Interpolate(a, b, fraction);
        }

        // Kenneth Moreland's Msh interpolation, also used by VTK's diverging transfer-function mode.
        private static double[] InterpolateDiverging(double[] rgbA, double[] rgbB, double fraction)
        {
            double[] mshA = LabToMsh(RgbToLab(rgbA));
            double[] mshB = LabToMsh(RgbToLab(rgbB));
            if (mshA[1] > .05 && mshB[1] > .05 && Math.Abs(mshA[2] - mshB[2]) > Math.PI / 3)
            {
                double middle = Math.Max(Math.Max(mshA[0], mshB[0]), 88);
                if (fraction < .5)
                {
                    mshB = new[] { middle, 0.0, 0.0 };
                    fraction *= 2;
                }
                else
                {
                    mshA = new[] { middle, 0.0, 0.0 };
                    fraction = 2 * fraction - 1;
                }
            }
            if (mshA[1] < .05 && mshB[1] > .05) mshA[2] = AdjustHue(mshB, mshA[0]);
            else if (mshB[1] < .05 && mshA[1] > .05) mshB[2] = AdjustHue(mshA, mshB[0]);
            return LabToRgb(MshToLab(Interpolate(mshA, mshB, fraction)));
        }

        private static double[] LabToMsh(double[] lab)
        {
            double magnitude = Math.Sqrt(lab[0] * lab[0] + lab[1] * lab[1] + lab[2] * lab[2]);
            double saturation = magnitude > .001 ? Math.Acos(lab[0] / magnitude) : 0;
            double hue = saturation > .001 ? Math.Atan2(lab[2], lab[1]) : 0;
            return new[] { magnitude, saturation, hue };
        }

        private static double[] MshToLab(double[] msh)
        {
            return new[] { msh[0] * Math.Cos(msh[1]), msh[0] * Math.Sin(msh[1]) * Math.Cos(msh[2]),
                           msh[0] * Math.Sin(msh[1]) * Math.Sin(msh[2]) };
        }

        private static double AdjustHue(double[] saturated, double unsaturatedMagnitude)
        {
            if (saturated[0] >= unsaturatedMagnitude) return saturated[2];
            double spin = saturated[1] * Math.Sqrt(unsaturatedMagnitude * unsaturatedMagnitude -
                                                   saturated[0] * saturated[0]) /
                          (saturated[0] * Math.Sin(saturated[1]));
            return saturated[2] > -Math.PI / 3 ? saturated[2] + spin : saturated[2] - spin;
        }

        private static double[] Brighten(double[] rgb, double amount)
        {
            return new[] { rgb[0] + amount * (1 - rgb[0]), rgb[1] + amount * (1 - rgb[1]),
                           rgb[2] + amount * (1 - rgb[2]) };
        }

        private static double[] Interpolate(double[] a, double[] b, double fraction)
        {
            return new[] { a[0] + (b[0] - a[0]) * fraction, a[1] + (b[1] - a[1]) * fraction,
                           a[2] + (b[2] - a[2]) * fraction };
        }

        private static double[] InterpolateHue(double[] a, double[] b, double fraction)
        {
            double delta = b[0] - a[0];
            if (delta > .5) delta -= 1;
            else if (delta < -.5) delta += 1;
            double hue = a[0] + delta * fraction;
            if (hue < 0) hue += 1;
            else if (hue > 1) hue -= 1;
            return new[] { hue, a[1] + (b[1] - a[1]) * fraction, a[2] + (b[2] - a[2]) * fraction };
        }

        private static double[] RgbToHsv(double[] rgb)
        {
            double max = rgb.Max();
            double min = rgb.Min();
            double delta = max - min;
            double hue = 0;
            if (delta > 0)
            {
                if (max == rgb[0]) hue = ((rgb[1] - rgb[2]) / delta) % 6;
                else if (max == rgb[1]) hue = (rgb[2] - rgb[0]) / delta + 2;
                else hue = (rgb[0] - rgb[1]) / delta + 4;
                hue /= 6;
                if (hue < 0) hue += 1;
            }
            return new[] { hue, max == 0 ? 0 : delta / max, max };
        }

        private static double[] HsvToRgb(double[] hsv)
        {
            double scaled = hsv[0] * 6;
            int sector = (int)Math.Floor(scaled) % 6;
            double fraction = scaled - Math.Floor(scaled);
            double p = hsv[2] * (1 - hsv[1]);
            double q = hsv[2] * (1 - fraction * hsv[1]);
            double t = hsv[2] * (1 - (1 - fraction) * hsv[1]);
            switch (sector)
            {
                case 0: return new[] { hsv[2], t, p };
                case 1: return new[] { q, hsv[2], p };
                case 2: return new[] { p, hsv[2], t };
                case 3: return new[] { p, q, hsv[2] };
                case 4: return new[] { t, p, hsv[2] };
                default: return new[] { hsv[2], p, q };
            }
        }

        private static double[] RgbToLab(double[] rgb)
        {
            double r = PivotRgb(rgb[0]);
            double g = PivotRgb(rgb[1]);
            double b = PivotRgb(rgb[2]);
            double x = (r * .4124564 + g * .3575761 + b * .1804375) / .95047;
            double y = r * .2126729 + g * .7151522 + b * .072175;
            double z = (r * .0193339 + g * .119192 + b * .9503041) / 1.08883;
            x = PivotXyz(x); y = PivotXyz(y); z = PivotXyz(z);
            return new[] { 116 * y - 16, 500 * (x - y), 200 * (y - z) };
        }

        private static double[] LabToRgb(double[] lab)
        {
            double y = (lab[0] + 16) / 116;
            double x = lab[1] / 500 + y;
            double z = y - lab[2] / 200;
            x = InversePivotXyz(x) * .95047;
            y = InversePivotXyz(y);
            z = InversePivotXyz(z) * 1.08883;
            double r = x * 3.2404542 + y * -1.5371385 + z * -.4985314;
            double g = x * -.969266 + y * 1.8760108 + z * .041556;
            double b = x * .0556434 + y * -.2040259 + z * 1.0572252;
            return new[] { Clamp(InversePivotRgb(r)), Clamp(InversePivotRgb(g)), Clamp(InversePivotRgb(b)) };
        }

        private static double PivotRgb(double value) { return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        private static double InversePivotRgb(double value) { return value <= .0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1 / 2.4) - .055; }
        private static double PivotXyz(double value) { return value > .008856 ? Math.Pow(value, 1.0 / 3) : 7.787 * value + 16.0 / 116; }
        private static double InversePivotXyz(double value) { double cube = value * value * value; return cube > .008856 ? cube : (value - 16.0 / 116) / 7.787; }
        private static double Clamp(double value) { return Math.Max(0, Math.Min(1, value)); }
        private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static Color ToColor(double[] rgb) { return Color.FromArgb(255, (int)(Clamp(rgb[0]) * 255), (int)(Clamp(rgb[1]) * 255), (int)(Clamp(rgb[2]) * 255)); }
        private static Point P(double position, double red, double green, double blue) { return new Point(position, new[] { red, green, blue }); }

        private sealed class Palette
        {
            public Palette(ColorSpace space, Point[] points) { Space = space; Points = points; }
            public ColorSpace Space { get; private set; }
            public Point[] Points { get; private set; }
        }

        private sealed class Point
        {
            public Point(double position, double[] rgb) { Position = position; Rgb = rgb; }
            public double Position { get; private set; }
            public double[] Rgb { get; private set; }
        }
    }
}
