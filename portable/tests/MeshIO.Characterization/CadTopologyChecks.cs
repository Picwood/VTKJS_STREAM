using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PrePoMax.Application;
using PrePoMax.Persistence;

internal static class CadTopologyChecks
{
    private const string Geometry = "SetFactory(\"OpenCASCADE\"); Sphere(1)={0,0,0,1};";
    internal const string Msh = """
$MeshFormat
4.1 0 8
$EndMeshFormat
$PhysicalNames
2
2 11 "Face é"
3 21 "Body"
$EndPhysicalNames
$Entities
4 6 4 1
1 0 0 0 0
2 1 0 0 0
3 0 1 0 0
4 0 0 1 0
1 0 0 0 1 0 0 0 2 1 -2
2 0 0 0 1 1 0 0 2 2 -3
3 0 0 0 0 1 0 0 2 3 -1
4 0 0 0 0 0 1 0 2 1 -4
5 0 0 0 1 0 1 0 2 2 -4
6 0 0 0 0 1 1 0 2 3 -4
1 0 0 0 1 1 0 1 11 3 1 2 3
2 0 0 0 1 0 1 0 3 1 5 -4
3 0 0 0 1 1 1 0 3 2 6 -5
4 0 0 0 0 1 1 0 3 3 4 -6
1 0 0 0 1 1 1 1 21 4 1 -2 3 -4
$EndEntities
""";
    public static void Run(Action<bool, string> check)
    {
        byte[] source = Encoding.UTF8.GetBytes(Geometry);
        var topology = CadTopologyCodec.ReadGmsh(Encoding.UTF8.GetBytes(Msh), source);
        check(topology.Entities.Length == 15 && topology.Entities.Count(entity => entity.Dimension == 1) == 6 &&
              topology.Entities.Single(entity => entity.Dimension == 3).BoundaryTags.SequenceEqual(new[] { 1, -2, 3, -4 }),
            "CAD topology parser retains dimensions and oriented boundary graph");
        check(topology.PhysicalGroups.Single(group => group.Dimension == 2).Name == "Face é" &&
              topology.Entities.Single(entity => entity.Dimension == 2 && entity.Tag == 1).PhysicalTags.SequenceEqual(new[] { 11 }),
            "CAD topology retains Unicode physical groups and entity membership");
        check(topology.SourceSha256 == Convert.ToHexString(SHA256.HashData(source)),
            "CAD topology is bound to exact retained source bytes");
        var restored = CadTopologyCodec.Deserialize(CadTopologyCodec.Serialize(topology, source), source);
        check(CadTopologyCodec.Serialize(restored, source).SequenceEqual(CadTopologyCodec.Serialize(topology, source)),
            "CAD topology JSON round-trips without losing graph or geometry bounds");
        foreach (string invalid in new[]
        {
            Msh.Replace("4.1 0 8", "4.1 1 8"),
            Msh.Replace("4 6 4 1", "20001 6 4 1"),
            Msh.Replace("4 6 4 1", "2147483648 6 4 1"),
            Msh.Replace("1 0 0 0 0\n", "1 NaN 0 0 0\n"),
            Msh.Replace("2 1 0 0 0\n", "1 1 0 0 0\n"),
            Msh.Replace("0 2 1 -2", "0 2 1 -99"),
            Msh.Replace("0 2 1 -2", "0 2 1 -2147483648"),
            Msh.Replace("0 2 1 -2", "0 2 1 0"),
            Msh.Replace("1 0 0 0 1 0 0 0 2", "1 2 0 0 1 0 0 0 2"),
            Msh.Replace("2 11 \"Face é\"", "2 11 \"\""),
            Msh.Replace("$EndEntities", "$EndEntities\n$Nodes\n0 0 0 0\n$EndNodes"),
            Msh.Replace("$EndEntities", ""),
            Msh.Replace("1 -2 3 -4", "1 -2 3")
        })
        {
            bool rejected = false;
            try { CadTopologyCodec.ReadGmsh(Encoding.UTF8.GetBytes(invalid), source); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, "CAD topology rejects malformed, oversized, nonfinite or dangling geometry records");
        }
        foreach (var invalid in new[]
        {
            topology with { Version = 2 },
            topology with { SourceSha256 = new string('0', 64) },
            topology with { Entities = topology.Entities.Concat(new[] { topology.Entities[0] }).ToArray() },
            topology with { PhysicalGroups = topology.PhysicalGroups.Concat(new[] { topology.PhysicalGroups[0] }).ToArray() }
        })
        {
            bool rejected = false;
            try { CadTopologyCodec.Serialize(invalid, source); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, "CAD topology JSON rejects bad versions, source bindings and duplicate identities");
        }
        string root = Path.Combine(Path.GetTempPath(), "CAD topology archive é " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var model = new StaticModelWorkflow(1).Model;
            var geometry = new ProjectGeometrySource("solid.geo", source, topology);
            string project = Path.Combine(root, "topology.pma");
            StaticProjectArchive.Save(project, model, geometrySource: geometry);
            check(AnalysisArchive.Describe(project).Version == 7 &&
                  AnalysisArchive.Describe(project).Payloads.Contains("geometry.topology.json") &&
                  ProjectService.Inspect(project).CadTopology is { Points: 4, Curves: 6, Surfaces: 4, Volumes: 1, PhysicalGroups: 2 },
                "PMA v7 persists checksummed source-bound topology and material identities and exposes inspection counts");
            string versionSix = Path.Combine(root, "version-six.pma");
            AnalysisArchive.Save(versionSix, AnalysisArchive.Open(project) with { MaterialIdentities = null });
            check(AnalysisArchive.Describe(versionSix).Version == 6 && StaticProjectArchive.Open(versionSix).GeometrySource!.Topology != null,
                "legacy topology PMA v6 remains readable with deterministic material identities");
            check(ProjectService.CadTopology(project).SourceSha256 == topology.SourceSha256,
                "application CAD topology service returns the retained inventory");
            string edited = Path.Combine(root, "edited.pma");
            ProjectNodeSets.AddCopy(project, edited, "CAD_TEST", new[] { 1 });
            check(CadTopologyCodec.Serialize(ProjectService.CadTopology(edited), source)
                    .SequenceEqual(CadTopologyCodec.Serialize(topology, source)),
                "project editing preserves the full CAD boundary graph");
            using (var stream = new MemoryStream())
            {
                AnalysisArchive.CopyPayloads(project, new Dictionary<string, Stream> { ["geometry.topology.json"] = stream });
                check(stream.ToArray().SequenceEqual(CadTopologyCodec.Serialize(topology, source)),
                    "streaming archive validation supports topology without opening the editable mesh");
            }
            string legacy = Path.Combine(root, "legacy.pma");
            AnalysisArchive.Save(legacy, AnalysisArchive.Open(project) with { GeometryTopology = null, MaterialIdentities = null });
            check(AnalysisArchive.Describe(legacy).Version == 5 && ProjectService.Inspect(legacy).CadTopology == null,
                "older retained-source PMA v5 remains readable without invented topology");
            bool missingRejected = false;
            try { ProjectService.CadTopology(legacy); }
            catch (NotSupportedException) { missingRejected = true; }
            check(missingRejected, "topology inspection explains how to capture missing legacy topology");
            foreach (bool wrongVersion in new[] { false, true })
            {
                StaticProjectArchive.Save(project, model, geometrySource: geometry);
                using (var zip = ZipFile.Open(project, ZipArchiveMode.Update))
                {
                    JsonNode manifest;
                    using (var reader = new StreamReader(zip.GetEntry("manifest.json")!.Open()))
                        manifest = JsonNode.Parse(reader.ReadToEnd())!;
                    if (wrongVersion) manifest["Version"] = 5;
                    else
                    {
                        byte[] badTopology = CadTopologyCodec.Serialize(topology, source);
                        var json = JsonNode.Parse(badTopology)!;
                        json["SourceSha256"] = new string('0', 64);
                        badTopology = Encoding.UTF8.GetBytes(json.ToJsonString());
                        zip.GetEntry("geometry.topology.json")!.Delete();
                        using (var output = zip.CreateEntry("geometry.topology.json").Open()) output.Write(badTopology);
                        var payload = manifest["Payloads"]!.AsArray().Single(item => item!["Name"]!.GetValue<string>() == "geometry.topology.json")!;
                        payload["Length"] = badTopology.Length;
                        payload["Sha256"] = Convert.ToHexString(SHA256.HashData(badTopology));
                    }
                    zip.GetEntry("manifest.json")!.Delete();
                    using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
                    writer.Write(manifest.ToJsonString());
                }
                bool rejected = false;
                try { StaticProjectArchive.Open(project); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, "archive rejects downgraded topology or a rehashed payload bound to different source bytes");
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
