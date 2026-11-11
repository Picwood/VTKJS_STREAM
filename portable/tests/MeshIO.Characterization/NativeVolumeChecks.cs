using System.Globalization;
using CaeMesh;
using FileInOut.Input;
using FileInOut.Output;

internal static class NativeVolumeChecks
{
    public static void Run(string input, string output)
    {
        foreach (string culture in new[] { "en-US", "it-IT" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var mesh = MmgFileReader.Read3D(input, ElementsToImport.Solid, MeshRepresentation.Mesh);
            if (mesh == null || mesh.Elements.Count == 0 || mesh.Parts.Count != 1)
                throw new Exception("Expected a connected volume mesh.");
            double volume = 0;
            foreach (var element in mesh.Elements.Values)
            {
                if (element is not LinearTetraElement tetra) throw new Exception("Expected linear tetrahedra.");
                double v = tetra.GetVolume(mesh.Nodes);
                if (!double.IsFinite(v) || v <= 0) throw new Exception("Invalid imported tetra volume.");
                volume += v;
            }
            if (Math.Abs(volume - 1) > 1e-8 || Math.Abs(mesh.BoundingBox.GetDiagonal() - Math.Sqrt(3)) > 1e-8)
                throw new Exception($"Incorrect cube volume/bounds: {volume}");
            GmshMshFileWriter.Write(output + "." + culture + ".msh", mesh);
            Console.WriteLine($"PASS: native volume import/export ({culture}): {mesh.Nodes.Count} nodes, {mesh.Elements.Count} tetrahedra, volume {volume:G12}.");
        }
    }
}
