using System.Globalization;
using System.Text.RegularExpressions;
using CaeMesh;

namespace PrePoMax.Application;

/// <summary>Strict mesh-only subset of Abaqus/CalculiX input syntax.</summary>
internal static class InpMeshImport
{
    internal static FeMesh Read(string path)
    {
        var nodes = new Dictionary<int, FeNode>();
        var elements = new Dictionary<int, FeElement>();
        var nodeSets = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var elementSets = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var nodeReferences = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var elementReferences = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var surfaceDefinitions = new Dictionary<string, Dictionary<FeFaceName, List<string>>>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<int>();
        string keyword = "", elementType = "";
        string? setName = null;
        bool generate = false;
        int surfaceRecords = 0;
        int Id(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && id > 0
            ? id : throw new InvalidDataException("INP IDs must be positive integers.");
        void AddSet(Dictionary<string, HashSet<int>> sets, string? name, IEnumerable<int> ids)
        {
            if (name == null) return;
            if (!sets.TryGetValue(name, out var values)) sets.Add(name, values = new HashSet<int>());
            values.UnionWith(ids);
            if (values.Count > 100000) throw new NotSupportedException("INP set exceeds 100000 entries.");
        }
        foreach (string raw in ReadIncludedLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("**")) continue;
            if (line.StartsWith('*'))
            {
                if (pending.Count != 0) throw new InvalidDataException("Incomplete INP element connectivity.");
                var header = line.Split(',').Select(s => s.Trim()).ToArray();
                keyword = header[0].ToUpperInvariant();
                var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string option in header.Skip(1).Where(s => s.Length != 0))
                {
                    var pair = option.Split('=', 2);
                    if (!options.TryAdd(pair[0].Trim(), pair.Length == 2 ? pair[1].Trim() : ""))
                        throw new InvalidDataException("Repeated INP option.");
                }
                string[] allowed = keyword switch
                {
                    "*HEADING" => Array.Empty<string>(), "*NODE" => new[] { "NSET" },
                    "*ELEMENT" => new[] { "TYPE", "ELSET" }, "*NSET" => new[] { "NSET", "GENERATE" },
                    "*ELSET" => new[] { "ELSET", "GENERATE" },
                    "*SURFACE" => new[] { "NAME", "TYPE" },
                    _ => throw new NotSupportedException("Unsupported mesh-only INP keyword: " + keyword)
                };
                if (options.Keys.Any(k => !allowed.Contains(k, StringComparer.OrdinalIgnoreCase)))
                    throw new NotSupportedException("Unsupported option on " + keyword);
                setName = options.GetValueOrDefault(keyword switch { "*NODE" or "*NSET" => "NSET", "*SURFACE" => "NAME", _ => "ELSET" });
                if ((setName != null && string.IsNullOrWhiteSpace(setName)) || (keyword is "*NSET" or "*ELSET" && setName == null))
                    throw new InvalidDataException("Missing INP set name.");
                generate = options.ContainsKey("GENERATE");
                if (generate && options["GENERATE"].Length != 0) throw new InvalidDataException("GENERATE takes no value.");
                if (keyword == "*ELEMENT")
                {
                    elementType = options.GetValueOrDefault("TYPE", "").ToUpperInvariant();
                    if (elementType is not ("C3D4" or "C3D8" or "C3D8R"))
                        throw new NotSupportedException("Supported INP elements: C3D4, C3D8 and C3D8R.");
                }
                else if (keyword == "*SURFACE")
                {
                    if (setName == null || !options.TryGetValue("TYPE", out string? type) ||
                        !type.Equals("ELEMENT", StringComparison.OrdinalIgnoreCase))
                        throw new NotSupportedException("INP surfaces require NAME and TYPE=ELEMENT.");
                    if (!surfaceDefinitions.ContainsKey(setName))
                    {
                        if (surfaceDefinitions.Count >= 1000) throw new NotSupportedException("INP contains more than 1000 surfaces.");
                        surfaceDefinitions.Add(setName, new Dictionary<FeFaceName, List<string>>());
                    }
                }
                continue;
            }
            if (keyword == "*HEADING") continue;
            var fields = line.TrimEnd(',').Split(',').Select(s => s.Trim()).ToArray();
            if (fields.Any(string.IsNullOrEmpty)) throw new InvalidDataException("Empty INP data field.");
            if (keyword == "*NODE")
            {
                if (fields.Length is < 2 or > 4) throw new InvalidDataException("Invalid INP node record.");
                int id = Id(fields[0]);
                var xyz = new double[3];
                for (int i = 1; i < fields.Length; i++)
                    if (!double.TryParse(fields[i].Replace('D', 'E').Replace('d', 'e'), NumberStyles.Float, CultureInfo.InvariantCulture, out xyz[i - 1]) || !double.IsFinite(xyz[i - 1]))
                        throw new InvalidDataException("Invalid INP coordinate.");
                if (!nodes.TryAdd(id, new FeNode(id, xyz))) throw new InvalidDataException("Duplicate INP node ID.");
                AddSet(nodeSets, setName, new[] { id });
            }
            else if (keyword == "*ELEMENT")
            {
                pending.AddRange(fields.Select(Id));
                int width = elementType == "C3D4" ? 5 : 9;
                if (pending.Count > width) throw new InvalidDataException("Invalid INP connectivity width.");
                if (pending.Count == width)
                {
                    int id = pending[0];
                    var connectivity = pending.Skip(1).ToArray();
                    if (connectivity.Distinct().Count() != connectivity.Length) throw new InvalidDataException("Repeated INP element node.");
                    FeElement element = elementType == "C3D4" ? new LinearTetraElement(id, connectivity) : new LinearHexaElement(id, connectivity);
                    element.PartId = elementType == "C3D4" ? 1 : elementType == "C3D8" ? 2 : 3;
                    if (!elements.TryAdd(id, element)) throw new InvalidDataException("Duplicate INP element ID.");
                    AddSet(elementSets, setName, new[] { id });
                    pending.Clear();
                }
            }
            else if (keyword is "*NSET" or "*ELSET")
            {
                var sets = keyword == "*NSET" ? nodeSets : elementSets;
                var references = keyword == "*NSET" ? nodeReferences : elementReferences;
                if (generate)
                {
                    var ids = fields.Select(Id).ToArray();
                    if (ids.Length is not (2 or 3)) throw new InvalidDataException("Invalid generated INP set.");
                    int increment = ids.Length == 3 ? ids[2] : 1;
                    long distance = (long)ids[1] - ids[0];
                    if (distance < 0 || distance % increment != 0 || distance / increment >= 100000)
                        throw new InvalidDataException("Invalid generated INP range.");
                    AddSet(sets, setName, Enumerable.Range(0, (int)(distance / increment) + 1).Select(i => (int)(ids[0] + (long)i * increment)));
                }
                else
                {
                    AddSet(sets, setName, Array.Empty<int>());
                    foreach (string value in fields)
                    {
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                            AddSet(sets, setName, new[] { Id(value) });
                        else
                        {
                            if (!references.TryGetValue(setName!, out var names))
                                references.Add(setName!, names = new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                            names.Add(value);
                        }
                    }
                }
            }
            else if (keyword == "*SURFACE")
            {
                if (fields.Length != 2 || !Enum.TryParse(fields[1], true, out FeFaceName face) ||
                    face == FeFaceName.Empty || !Enum.IsDefined(face))
                    throw new InvalidDataException("Element surface rows require an element set or ID and S1 through S6.");
                string source = fields[0];
                if (string.IsNullOrWhiteSpace(source) || ++surfaceRecords > 100000)
                    throw new NotSupportedException("INP surface definition exceeds supported bounds.");
                var faces = surfaceDefinitions[setName!];
                if (!faces.TryGetValue(face, out var sources)) faces.Add(face, sources = new List<string>());
                sources.Add(source);
            }
            else throw new NotSupportedException("INP data requires a supported keyword.");
            if (nodes.Count > 100000 || elements.Count > 100000) throw new NotSupportedException("INP mesh exceeds 100000 nodes/elements.");
        }
        if (pending.Count != 0 || nodes.Count == 0 || elements.Count == 0 || elements.Values.Any(e => e.NodeIds.Any(id => !nodes.ContainsKey(id))))
            throw new InvalidDataException("Incomplete INP mesh or unknown node reference.");
        ResolveReferences(nodeSets, nodeReferences);
        ResolveReferences(elementSets, elementReferences);
        if (nodeSets.Values.Any(set => set.Any(id => !nodes.ContainsKey(id))) || elementSets.Values.Any(set => set.Any(id => !elements.ContainsKey(id))))
            throw new InvalidDataException("Unknown INP set member.");
        if (elements.Values.Cast<FeElement3D>().Any(e => !double.IsFinite(e.GetVolume(nodes)) || e.GetVolume(nodes) <= 0))
            throw new InvalidDataException("Degenerate INP volume element.");
        var mesh = new FeMesh(nodes, elements, MeshRepresentation.Mesh);
        foreach (MeshPart part in mesh.Parts.Values)
            if (part.PartId == 3) part.LinearHexaType = FeElementTypeLinearHexa.C3D8R;
        foreach (var set in nodeSets) mesh.NodeSets.Add(set.Key, new FeNodeSet(set.Key, set.Value.Order().ToArray()));
        foreach (var set in elementSets) mesh.ElementSets.Add(set.Key, new FeElementSet(set.Key, set.Value.Order().ToArray()));
        int generatedSet = 0;
        foreach (var definition in surfaceDefinitions)
        {
            if (definition.Value.Count == 0) throw new InvalidDataException("INP surface has no element faces.");
            var surface = new FeSurface(definition.Key);
            foreach (var face in definition.Value)
            {
                var labels = new HashSet<int>();
                string? reusableName = null;
                foreach (string source in face.Value)
                {
                    if (int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out int elementId))
                    {
                        if (elementId <= 0 || !elements.ContainsKey(elementId))
                            throw new InvalidDataException("Unknown element ID in INP surface.");
                        labels.Add(elementId);
                    }
                    else
                    {
                        if (!elementSets.TryGetValue(source, out var sourceSet) || sourceSet.Count == 0)
                            throw new InvalidDataException("Unknown or empty element set in INP surface.");
                        labels.UnionWith(sourceSet);
                        if (face.Value.Count == 1)
                            reusableName = elementSets.Keys.First(name => name.Equals(source, StringComparison.OrdinalIgnoreCase));
                    }
                }
                foreach (int elementId in labels)
                    try { elements[elementId].GetNodeIdsFromFaceName(face.Key); }
                    catch (NotSupportedException ex) { throw new InvalidDataException("INP surface face is invalid for its element type.", ex); }
                string elementSetName = reusableName ?? "__surface_" + ++generatedSet + "_" + face.Key;
                if (reusableName == null)
                {
                    while (mesh.ElementSets.Keys.Any(name => name.Equals(elementSetName, StringComparison.OrdinalIgnoreCase)))
                        elementSetName = "__surface_" + ++generatedSet + "_" + face.Key;
                    mesh.ElementSets.Add(elementSetName, new FeElementSet(elementSetName, labels.Order().ToArray()));
                }
                surface.AddElementFace(face.Key, elementSetName);
            }
            mesh.Surfaces.Add(surface.Name, surface);
        }
        return mesh;
    }
    private static void ResolveReferences(Dictionary<string, HashSet<int>> sets, Dictionary<string, HashSet<string>> references)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<int> Resolve(string name, int depth)
        {
            if (!sets.TryGetValue(name, out var values)) throw new InvalidDataException("Unknown named INP set reference.");
            if (resolved.Contains(name)) return values;
            if (depth > 64 || !active.Add(name)) throw new InvalidDataException("Cyclic or excessively nested INP set reference.");
            if (references.TryGetValue(name, out var dependencies))
                foreach (string dependency in dependencies)
                {
                    values.UnionWith(Resolve(dependency, depth + 1));
                    if (values.Count > 100000) throw new NotSupportedException("INP set exceeds 100000 entries.");
                }
            active.Remove(name);
            resolved.Add(name);
            return values;
        }
        foreach (string name in sets.Keys) Resolve(name, 0);
    }

    private static IEnumerable<string> ReadIncludedLines(string path)
    {
        var active = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        long bytes = 0;
        int files = 0;
        IEnumerable<string> Read(string source, int depth)
        {
            source = Path.GetFullPath(source);
            if (depth > 16 || ++files > 64 || !active.Add(source))
                throw new InvalidDataException("Cyclic or excessively nested INP includes.");
            if (!File.Exists(source)) throw new InvalidDataException("An INP include file is missing.");
            bytes += new FileInfo(source).Length;
            if (bytes > 32 * 1024 * 1024) throw new NotSupportedException("Combined INP input exceeds 32 MiB.");
            foreach (string line in File.ReadLines(source))
            {
                string keyword = line.TrimStart().Split(',')[0].Trim();
                if (!keyword.Equals("*INCLUDE", StringComparison.OrdinalIgnoreCase))
                {
                    yield return line;
                    continue;
                }
                var match = Regex.Match(line, "^\\s*\\*INCLUDE\\s*,\\s*INPUT\\s*=\\s*(?:\"([^\"]+)\"|'([^']+)'|([^,\"']+))\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!match.Success) throw new InvalidDataException("INP INCLUDE requires exactly INPUT=filename.");
                string filename = Enumerable.Range(1, 3).Select(i => match.Groups[i].Value).First(v => v.Length != 0).Trim();
                if (filename.Length == 0) throw new InvalidDataException("Missing INP include filename.");
                filename = filename.Replace('\\', Path.DirectorySeparatorChar);
                foreach (string included in Read(Path.Combine(Path.GetDirectoryName(source)!, filename), depth + 1)) yield return included;
            }
            active.Remove(source);
        }
        return Read(path, 0);
    }

}
