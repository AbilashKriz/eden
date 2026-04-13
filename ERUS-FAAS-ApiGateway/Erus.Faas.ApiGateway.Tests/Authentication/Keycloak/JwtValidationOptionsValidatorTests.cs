using Erus.Faas.ApiGateway.Authentication.Keycloak;

namespace Erus.Faas.ApiGateway.Tests.Authentication.Keycloak;

public class JwtValidationOptionsValidatorTests
{
    private readonly JwtValidationOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidOptions_Succeeds()
    {
        var options = new JwtValidationOptions
        {
            ValidIssuers = ["https://issuer.example.com"],
            ValidAudiences = ["audience-1"]
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithEmptyIssuers_Fails()
    {
        var options = new JwtValidationOptions
        {
            ValidIssuers = [],
            ValidAudiences = ["audience-1"]
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithWhitespaceAudience_Fails()
    {
        var options = new JwtValidationOptions
        {
            ValidIssuers = ["https://issuer.example.com"],
            ValidAudiences = [" "]
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }
}
