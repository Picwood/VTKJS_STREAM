using PrePoMax.Render.Contracts;

namespace PrePoMax.Render.Contracts.Tests;

public sealed class ContractValidatorTests
{
    [Fact]
    public void CalculateByteLength_UsesCheckedArithmetic()
    {
        Assert.Equal(36, ContractValidator.CalculateByteLength(RenderDataTypes.Float32, 3, 3));
        var error = Assert.Throws<ContractValidationException>(() =>
            ContractValidator.CalculateByteLength(RenderDataTypes.Float64, int.MaxValue, long.MaxValue));
        Assert.Equal("resource-length-invalid", error.Code);
    }

    [Theory]
    [InlineData("AAAAAAAA-0000-0000-0000-000000000000")]
    [InlineData("not-a-uuid")]
    [InlineData("aaaaaaaa000000000000000000000000")]
    public void ValidateOpaqueId_RejectsNonCanonicalIds(string identifier)
    {
        var error = Assert.Throws<ContractValidationException>(() =>
            ContractValidator.ValidateOpaqueId(identifier, "testId"));
        Assert.Equal("identifier-invalid", error.Code);
    }
}
