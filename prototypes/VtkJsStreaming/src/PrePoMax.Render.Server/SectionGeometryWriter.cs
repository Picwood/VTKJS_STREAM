using System.Text;

namespace PrePoMax.Render.Server;

internal static class SectionGeometryWriter
{
    public const string ContentType = "application/vnd.prepomax.section-geometry-v1";
    private static readonly byte[] Magic = "PMXSEC1\0"u8.ToArray();

    public static async Task WriteAsync(HttpResponse response, SectionGeometryResult result, CancellationToken cancellationToken)
    {
        response.ContentType = ContentType;
        response.ContentLength = GetLength(result);
        await response.Body.WriteAsync(Magic, cancellationToken);
        await WriteInt32Async(response.Body, result.Actors.Count, cancellationToken);
        await WriteInt32Async(response.Body, result.SkippedCells, cancellationToken);
        foreach (var actor in result.Actors)
        {
            await WriteStringAsync(response.Body, actor.ActorId, cancellationToken);
            await WriteStringAsync(response.Body, actor.ParentActorId, cancellationToken);
            await WriteStringAsync(response.Body, actor.Name, cancellationToken);
            foreach (var component in actor.Rgba) await WriteSingleAsync(response.Body, (float)component, cancellationToken);
            await WriteSingleAsync(response.Body, actor.LineWidthPx, cancellationToken);
            await WriteInt32Async(response.Body, actor.Positions.Length / 3, cancellationToken);
            await WriteInt32Async(response.Body, actor.TriangleConnectivity.Length / 3, cancellationToken);
            await WriteInt32Async(response.Body, actor.LineConnectivity.Length / 2, cancellationToken);
            foreach (var value in actor.Bounds) await WriteDoubleAsync(response.Body, value, cancellationToken);
            await WriteArrayAsync(response.Body, actor.Positions, cancellationToken);
            await WriteArrayAsync(response.Body, actor.TriangleConnectivity, cancellationToken);
            await WriteArrayAsync(response.Body, actor.LineConnectivity, cancellationToken);
            await WriteArrayAsync(response.Body, actor.PointScalars, cancellationToken);
            await WriteArrayAsync(response.Body, actor.SourceElementIds, cancellationToken);
        }
    }

    private static long GetLength(SectionGeometryResult result)
    {
        var length = checked((long)Magic.Length + 2 * sizeof(int));
        foreach (var actor in result.Actors)
        {
            length = checked(length + StringLength(actor.ActorId) + StringLength(actor.ParentActorId) + StringLength(actor.Name));
            length = checked(length + 5 * sizeof(float) + 3 * sizeof(int) + 6 * sizeof(double));
            length = checked(length + (long)(actor.Positions.Length + actor.PointScalars.Length) * sizeof(float));
            length = checked(length + (long)(actor.TriangleConnectivity.Length + actor.LineConnectivity.Length) * sizeof(uint));
            length = checked(length + (long)actor.SourceElementIds.Length * sizeof(int));
        }
        return length;
    }

    private static int StringLength(string value) => checked(sizeof(int) + Encoding.UTF8.GetByteCount(value));

    private static async Task WriteStringAsync(Stream stream, string value, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        await WriteInt32Async(stream, bytes.Length, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task WriteInt32Async(Stream stream, int value, CancellationToken cancellationToken)
    {
        var bytes = BitConverter.GetBytes(value);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task WriteSingleAsync(Stream stream, float value, CancellationToken cancellationToken)
    {
        var bytes = BitConverter.GetBytes(value);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task WriteDoubleAsync(Stream stream, double value, CancellationToken cancellationToken)
    {
        var bytes = BitConverter.GetBytes(value);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task WriteArrayAsync(Stream stream, Array values, CancellationToken cancellationToken)
    {
        var bytes = new byte[Buffer.ByteLength(values)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        await stream.WriteAsync(bytes, cancellationToken);
    }
}
