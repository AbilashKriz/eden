using Erus.Faas.ApiGateway.Infrastructure;

namespace Erus.Faas.ApiGateway.Tests.Infrastructure;

public class GatewayForwardedHeadersOptionsValidatorTests
{
    private readonly GatewayForwardedHeadersOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidOptions_Succeeds()
    {
        var options = new GatewayForwardedHeadersOptions
        {
            ForwardLimit = 2,
            KnownNetworks = ["10.0.0.0/8"],
            KnownProxies = ["127.0.0.1"]
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithInvalidForwardLimit_Fails()
    {
        var options = new GatewayForwardedHeadersOptions { ForwardLimit = 0 };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithInvalidKnownNetwork_Fails()
    {
        var options = new GatewayForwardedHeadersOptions
        {
            ForwardLimit = 1,
            KnownNetworks = ["not-a-cidr"]
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithInvalidKnownProxy_Fails()
    {
        var options = new GatewayForwardedHeadersOptions
        {
            ForwardLimit = 1,
            KnownProxies = ["not-an-ip"]
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }
}
