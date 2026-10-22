using WinGlance.Services;

namespace WinGlance.Tests.Services;

public class SecretProtectorTests
{
    [Fact]
    public void Protect_RoundTrips_WithoutExposingTheSecret()
    {
        var protectedKey = SecretProtector.Protect("sk-live-123");

        Assert.DoesNotContain("sk-live-123", protectedKey);
        Assert.Equal("sk-live-123", SecretProtector.Unprotect(protectedKey));
    }

    [Fact]
    public void Protect_EmptySecret_GivesEmptyString()
    {
        Assert.Equal(string.Empty, SecretProtector.Protect(string.Empty));
        Assert.Equal(string.Empty, SecretProtector.Unprotect(string.Empty));
    }

    [Theory]
    [InlineData("not base64 !")]
    [InlineData("AQIDBA==")] // valid base64, not a DPAPI blob
    public void Unprotect_InvalidValue_ReturnsNull(string value)
    {
        Assert.Null(SecretProtector.Unprotect(value));
    }
}
