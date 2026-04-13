using Erus.Faas.ApiGateway.Infrastructure;

namespace Erus.Faas.ApiGateway.Tests.Infrastructure;

public class GatewayOptionsValidatorTests
{
    private readonly GatewayOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithNullAppName_Succeeds()
    {
        var options = new GatewayOptions { AppName = null };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithWhitespaceAppName_Fails()
    {
        var options = new GatewayOptions { AppName = "   " };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithValidAppName_Succeeds()
    {
        var options = new GatewayOptions { AppName = "fleet-apigateway-service" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }
}
