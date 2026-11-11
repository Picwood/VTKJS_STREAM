using System.Globalization;
using CaeMesh;
using FileInOut.Input;
using FileInOut.Output;

internal static class NativeSurfaceChecks
{
    public static void Run(string input, string output)
    {
        foreach (string culture in new[] { "en-US", "it-IT" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var mesh = StlFileReader.Read(input) ?? throw new Exception("Native STL was not found.");
            if (mesh.Parts.Count != 1 || mesh.Elements.Count == 0 || mesh.Nodes.Count == 0)
                throw new Exception("Expected one connected cube boundary.");
            double area = 0;
            foreach (var element in mesh.Elements.Values)
            {
                if (element is not LinearTriangleElement) throw new Exception("Expected triangles.");
                element.GetCG(mesh.Nodes, out double a);
                if (!double.IsFinite(a) || a <= 0) throw new Exception("Invalid triangle area.");
                area += a;
            }
            if (Math.Abs(area - 6) > 1e-5) throw new Exception($"Incorrect cube area: {area}");
            if (Math.Abs(mesh.BoundingBox.GetDiagonal() - Math.Sqrt(3)) > 1e-6)
                throw new Exception("Incorrect cube bounds.");
            foreach (var node in mesh.Nodes.Values)
            {
                var p = node.Coor;
                if (p.Any(x => x < -1e-6 || x > 1 + 1e-6) || !p.Any(x => Math.Abs(x) < 1e-6 || Math.Abs(x - 1) < 1e-6))
                    throw new Exception("Node does not lie on the unit cube boundary.");
            }
            GmshMshFileWriter.Write(output + "." + culture + ".msh", mesh);
            Console.WriteLine($"PASS: native surface import/export ({culture}): {mesh.Nodes.Count} nodes, {mesh.Elements.Count} triangles.");
        }
    }
}
