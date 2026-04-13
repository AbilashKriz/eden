using Erus.Faas.ApiGateway.Transforms;

namespace Erus.Faas.ApiGateway.Tests.Transforms;

public class DynamicServiceNameTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-starts-with-dash")]
    [InlineData("ends-with-dash-")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    [InlineData("has/slash")]
    [InlineData("foo@attacker.com")]
    [InlineData("foo:8080")]
    [InlineData("foo%0abar")]
    public void TryNormalize_Invalid_ReturnsFalse(string? raw)
    {
        Assert.False(DynamicServiceName.TryNormalize(raw, out _));
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("fleet-account-service", "fleet-account-service")]
    [InlineData("Fleet-Account-Service", "fleet-account-service")]
    [InlineData("a0-1", "a0-1")]
    public void TryNormalize_Valid_ReturnsTrue_AndNormalizes(string raw, string expected)
    {
        Assert.True(DynamicServiceName.TryNormalize(raw, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void TryNormalize_TooLong_ReturnsFalse()
    {
        var raw = new string('a', 64);
        Assert.False(DynamicServiceName.TryNormalize(raw, out _));
    }
}


