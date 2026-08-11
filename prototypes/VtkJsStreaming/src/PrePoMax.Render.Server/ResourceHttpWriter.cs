using Microsoft.Net.Http.Headers;
using PrePoMax.Render.Contracts;
using PrePoMax.Render.SyntheticData;

namespace PrePoMax.Render.Server;

internal static class ResourceHttpWriter
{
    public static async Task WriteAsync(HttpContext context, IRenderResource resource, CancellationToken cancellationToken)
    {
        var etag = $"\"{resource.Descriptor.Sha256}\"";
        context.Response.Headers.ETag = etag;
        context.Response.Headers.CacheControl = "private, immutable";
        context.Response.Headers.AcceptRanges = "bytes";
        context.Response.ContentType = ProtocolConstants.ResourceContentType;

        if (context.Request.Headers.IfNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        var start = 0L;
        var end = resource.Descriptor.ByteLength - 1;
        if (context.Request.Headers.TryGetValue(HeaderNames.Range, out var rangeHeader))
        {
            if (!TryParseSingleRange(rangeHeader.ToString(), resource.Descriptor.ByteLength, out start, out end))
            {
                context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                context.Response.Headers.ContentRange = $"bytes */{resource.Descriptor.ByteLength}";
                return;
            }

            context.Response.StatusCode = StatusCodes.Status206PartialContent;
            context.Response.Headers.ContentRange = $"bytes {start}-{end}/{resource.Descriptor.ByteLength}";
        }

        var length = checked(end - start + 1);
        context.Response.ContentLength = length;
        await resource.WriteAsync(context.Response.Body, start, length, cancellationToken);
    }

    internal static bool TryParseSingleRange(string header, long totalLength, out long start, out long end)
    {
        start = 0;
        end = 0;
        if (totalLength <= 0 || !header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) || header.Contains(','))
        {
            return false;
        }

        var parts = header[6..].Split('-', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[0].Length == 0)
        {
            if (!long.TryParse(parts[1], out var suffixLength) || suffixLength <= 0)
            {
                return false;
            }

            suffixLength = Math.Min(suffixLength, totalLength);
            start = totalLength - suffixLength;
            end = totalLength - 1;
            return true;
        }

        if (!long.TryParse(parts[0], out start) || start < 0 || start >= totalLength)
        {
            return false;
        }

        if (parts[1].Length == 0)
        {
            end = totalLength - 1;
            return true;
        }

        if (!long.TryParse(parts[1], out end) || end < start)
        {
            return false;
        }

        end = Math.Min(end, totalLength - 1);
        return true;
    }
}
