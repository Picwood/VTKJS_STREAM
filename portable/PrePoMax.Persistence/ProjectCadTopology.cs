using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PrePoMax.Persistence;

public sealed record CadEntity(int Dimension, int Tag, double[] Bounds, int[] PhysicalTags, int[] BoundaryTags);
public sealed record CadPhysicalGroup(int Dimension, int Tag, string? Name);
public sealed record ProjectCadTopology(int Version, string SourceSha256, CadEntity[] Entities,
                                         CadPhysicalGroup[] PhysicalGroups);

/// <summary>Bounded, source-bound CAD entity inventory; not an editable geometric kernel.</summary>
public static class CadTopologyCodec
{
    public const int MaximumBytes = 4 * 1024 * 1024;
    private const int MaximumEntities = 20000;
    private const int MaximumBoundaryReferences = 200000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static byte[] Serialize(ProjectCadTopology topology, byte[] source)
    {
        Validate(topology, source);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(topology, JsonOptions);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("CAD topology exceeds 4 MiB.");
        return bytes;
    }

    public static ProjectCadTopology Deserialize(byte[] bytes, byte[] source)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Invalid CAD topology size.");
        try
        {
            var topology = JsonSerializer.Deserialize<ProjectCadTopology>(bytes, JsonOptions)
                ?? throw new InvalidDataException("Missing CAD topology.");
            Validate(topology, source);
            return topology;
        }
        catch (JsonException ex) { throw new InvalidDataException("Invalid CAD topology JSON.", ex); }
    }

    public static void Validate(ProjectCadTopology topology, byte[] source)
    {
        if (topology == null || topology.Version != 1 ||
            topology.SourceSha256 != Convert.ToHexString(SHA256.HashData(source)) ||
            topology.Entities is not { Length: > 0 and <= MaximumEntities } ||
            topology.PhysicalGroups == null || topology.PhysicalGroups.Length > 2000)
            throw new InvalidDataException("Invalid or source-mismatched CAD topology.");
        var entities = new HashSet<(int, int)>();
        var groups = new HashSet<(int, int)>();
        foreach (var group in topology.PhysicalGroups)
            if (group == null || group.Dimension is < 0 or > 3 || group.Tag <= 0 ||
                group.Name != null && (string.IsNullOrWhiteSpace(group.Name) || group.Name.Length > 1024 || group.Name.Any(char.IsControl)) ||
                !groups.Add((group.Dimension, group.Tag)))
                throw new InvalidDataException("Invalid CAD physical group.");
        long references = 0;
        foreach (var entity in topology.Entities)
        {
            if (entity == null || entity.Dimension is < 0 or > 3 || entity.Tag <= 0 ||
                !entities.Add((entity.Dimension, entity.Tag)) || entity.Bounds is not { Length: 6 } ||
                entity.Bounds.Any(value => !double.IsFinite(value)) ||
                Enumerable.Range(0, 3).Any(axis => entity.Bounds[axis] > entity.Bounds[axis + 3]) ||
                entity.Dimension == 0 && Enumerable.Range(0, 3).Any(axis => entity.Bounds[axis] != entity.Bounds[axis + 3]) ||
                entity.PhysicalTags == null || entity.BoundaryTags == null ||
                entity.PhysicalTags.Length > 2000 || entity.PhysicalTags.Distinct().Count() != entity.PhysicalTags.Length ||
                entity.PhysicalTags.Any(tag => !groups.Contains((entity.Dimension, tag))) ||
                entity.Dimension == 0 && entity.BoundaryTags.Length != 0)
                throw new InvalidDataException("Invalid CAD entity.");
            references += entity.BoundaryTags.Length;
            if (references > MaximumBoundaryReferences) throw new InvalidDataException("CAD boundary graph exceeds limit.");
        }
        foreach (var entity in topology.Entities)
            if (entity.BoundaryTags.Any(tag => tag is 0 or int.MinValue ||
                !entities.Contains((entity.Dimension - 1, Math.Abs(tag)))))
                throw new InvalidDataException("Dangling CAD boundary reference.");
        if (!topology.Entities.Any(entity => entity.Dimension == 3))
            throw new InvalidDataException("CAD topology contains no volume.");
    }

    /// <summary>Reads the geometry-only MSH 4.1 snapshot written before mesh generation.</summary>
    public static ProjectCadTopology ReadGmsh(byte[] bytes, byte[] source)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw new InvalidDataException("Invalid Gmsh topology size.");
        string[] lines;
        try { lines = new UTF8Encoding(false, true).GetString(bytes).Split('\n'); }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("Invalid topology text.", ex); }
        int position = 0;
        string Next()
        {
            if (position >= lines.Length) throw new InvalidDataException("Truncated Gmsh topology.");
            return lines[position++].Trim();
        }
        string[] Fields() => Next().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int Integer(string text, bool signed = false)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ||
                value == int.MinValue || (!signed && value < 0)) throw new InvalidDataException("Invalid topology integer.");
            return value;
        }
        double Number(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                throw new InvalidDataException("Invalid topology coordinate.");
            return value;
        }
        if (Next() != "$MeshFormat" || Next() != "4.1 0 8" || Next() != "$EndMeshFormat")
            throw new InvalidDataException("Require ASCII MSH 4.1 topology.");
        var entities = new List<CadEntity>();
        var groups = new Dictionary<(int, int), CadPhysicalGroup>();
        string section = Next();
        if (section == "$PhysicalNames")
        {
            int count = Integer(Next());
            if (count > 2000) throw new InvalidDataException("Too many physical groups.");
            for (int row = 0; row < count; row++)
            {
                var match = Regex.Match(Next(), "^([0-3])\\s+([1-9][0-9]*)\\s+\"(.*)\"$", RegexOptions.CultureInvariant);
                if (!match.Success) throw new InvalidDataException("Invalid topology physical name.");
                int dimension = Integer(match.Groups[1].Value), tag = Integer(match.Groups[2].Value);
                string name = match.Groups[3].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                if (!groups.TryAdd((dimension, tag), new(dimension, tag, name)))
                    throw new InvalidDataException("Duplicate physical name.");
            }
            if (Next() != "$EndPhysicalNames") throw new InvalidDataException("Missing physical-name terminator.");
            section = Next();
        }
        if (section != "$Entities") throw new InvalidDataException("Missing Gmsh entities.");
        string[] counts = Fields();
        if (counts.Length != 4) throw new InvalidDataException("Invalid entity counts.");
        int[] sizes = counts.Select(value => Integer(value)).ToArray();
        if (sizes.Sum(value => (long)value) > MaximumEntities) throw new InvalidDataException("Too many CAD entities.");
        for (int dimension = 0; dimension < 4; dimension++)
            for (int row = 0; row < sizes[dimension]; row++)
            {
                string[] fields = Fields();
                int width = dimension == 0 ? 4 : 7;
                if (fields.Length <= width) throw new InvalidDataException("Truncated entity record.");
                int tag = Integer(fields[0]);
                double[] bounds = dimension == 0
                    ? fields.Skip(1).Take(3).Select(Number).Concat(fields.Skip(1).Take(3).Select(Number)).ToArray()
                    : fields.Skip(1).Take(6).Select(Number).ToArray();
                int nPhysical = Integer(fields[width++]);
                if (nPhysical > 2000 || fields.Length < width + nPhysical)
                    throw new InvalidDataException("Invalid physical membership count.");
                int[] physical = fields.Skip(width).Take(nPhysical).Select(value => Integer(value)).ToArray();
                width += nPhysical;
                foreach (int physicalTag in physical)
                    groups.TryAdd((dimension, physicalTag), new(dimension, physicalTag, null));
                int[] boundary = [];
                if (dimension > 0)
                {
                    if (width >= fields.Length) throw new InvalidDataException("Missing boundary count.");
                    int nBoundary = Integer(fields[width++]);
                    if (nBoundary > MaximumBoundaryReferences || fields.Length - width != nBoundary)
                        throw new InvalidDataException("Invalid boundary count.");
                    boundary = fields.Skip(width).Select(value => Integer(value, true)).ToArray();
                    width += nBoundary;
                }
                if (fields.Length != width) throw new InvalidDataException("Trailing entity fields.");
                entities.Add(new(dimension, tag, bounds, physical, boundary));
            }
        if (Next() != "$EndEntities" || lines.Skip(position).Any(line => !string.IsNullOrWhiteSpace(line)))
            throw new InvalidDataException("Unexpected topology sections or trailing data.");
        var result = new ProjectCadTopology(1, Convert.ToHexString(SHA256.HashData(source)),
            entities.OrderBy(entity => entity.Dimension).ThenBy(entity => entity.Tag).ToArray(),
            groups.Values.OrderBy(group => group.Dimension).ThenBy(group => group.Tag).ToArray());
        Validate(result, source);
        return result;
    }
}
