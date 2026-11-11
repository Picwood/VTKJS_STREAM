using System.Globalization;
using System.Text;
using QuantumConcepts.Formats.StereoLithography;

// Independent input characterizes existing file semantics before core migration.
const string ascii = "solid triangle\nfacet normal 0 0 1\nouter loop\nvertex 0.25 0 0\nvertex 1.5 0 0\nvertex 0 2.75 0\nendloop\nendfacet\nendsolid triangle\n";
string directory = Path.Combine(Path.GetTempPath(), "prepomax STL à " + Guid.NewGuid());
CultureInfo originalCulture = CultureInfo.CurrentCulture;
int checks = 0;
try
{
    foreach (string culture in new[] { "en-US", "it-IT" })
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        foreach (string newline in new[] { "\n", "\r\n" })
        {
            STLDocument document = STLDocument.Read(ascii.Replace("\n", newline));
            Check(document.Name == "triangle", "solid name");
            Check(document.Facets.Count == 1, "facet count");
            Check(document.Facets[0].Normal.Equals(new Normal(0, 0, 1)), "normal");
            Check(document.Facets[0].Vertices.SequenceEqual(new[] {
                new Vertex(0.25f, 0, 0), new Vertex(1.5f, 0, 0), new Vertex(0, 2.75f, 0)
            }), "vertex coordinates and order");

            string path = Path.Combine(directory, culture, newline.Length.ToString(), "Triangle.stl");
            document.SaveAsText(path);
            Check(document.Equals(STLDocument.Open(path)), "text round trip with Unicode path");
            Check(File.ReadAllText(path).Contains("0.25"), "invariant decimal output");

            document.Facets[0].AttributeByteCount = 42;
            document.SaveAsBinary(path);
            byte[] bytes = File.ReadAllBytes(path);
            Check(bytes.Length == 134, "binary STL record length");
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                reader.BaseStream.Position = 80;
                Check(reader.ReadUInt32() == 1, "binary triangle count");
                Check(reader.ReadSingle() == 0 && reader.ReadSingle() == 0 && reader.ReadSingle() == 1,
                    "binary normal layout");
                Check(reader.ReadSingle() == 0.25f, "binary coordinate layout");
            }
            STLDocument restored = STLDocument.Open(path);
            Check(document.Equals(restored), "binary geometry round trip");
            Check(restored.Facets[0].AttributeByteCount == 42, "binary attribute preservation");
            using var stream = new MemoryStream();
            document.WriteText(stream);
            Check(stream.CanWrite, "writer leaves caller stream open");
            stream.Position = 0;
            Check(document.Equals(STLDocument.Read(stream)), "stream round trip");
            Check(stream.CanRead, "reader leaves caller stream open");
        }
    }
    Console.WriteLine($"PASS: {checks} STL characterization checks.");
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}

void Check(bool condition, string description)
{
    if (!condition) throw new Exception($"FAIL ({CultureInfo.CurrentCulture.Name}): {description}");
    checks++;
}
