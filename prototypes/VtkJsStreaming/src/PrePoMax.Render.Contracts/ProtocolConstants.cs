namespace PrePoMax.Render.Contracts;

public static class ProtocolConstants
{
    public const int Major = 1;
    public const int Minor = 0;
    public const string Version = "1.0";
    public const string HeaderName = "X-PrePoMax-Protocol";
    public const string ResourceContentType = "application/vnd.prepomax.render-array";
}

public static class RenderDataTypes
{
    public const string Int32 = "int32";
    public const string UInt32 = "uint32";
    public const string Float32 = "float32";
    public const string Float64 = "float64";

    public static int SizeOf(string dataType) => dataType switch
    {
        Int32 or UInt32 or Float32 => 4,
        Float64 => 8,
        _ => throw new ContractValidationException("data-type-invalid", $"Unsupported data type '{dataType}'."),
    };
}
