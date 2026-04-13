using Erus.Faas.ApiGateway.Infrastructure;

namespace Erus.Faas.ApiGateway.Tests.Infrastructure;

public class RateLimitingOptionsValidatorTests
{
    private readonly RateLimitingOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidOptions_Succeeds()
    {
        var options = new RateLimitingOptions
        {
            PermitLimit = 10,
            WindowSeconds = 5
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithZeroPermitLimit_Fails()
    {
        var options = new RateLimitingOptions
        {
            PermitLimit = 0,
            WindowSeconds = 10
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithZeroWindowSeconds_Fails()
    {
        var options = new RateLimitingOptions
        {
            PermitLimit = 10,
            WindowSeconds = 0
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }
}
