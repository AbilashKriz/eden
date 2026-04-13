using Erus.Faas.ApiGateway.Transforms;

namespace Erus.Faas.ApiGateway.Tests.Transforms;

public class DynamicRoutingOptionsValidatorTests
{
    private readonly DynamicRoutingOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyAllowlist_Succeeds()
    {
        var options = new DynamicRoutingOptions { AllowedServices = [] };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithWhitespaceEntry_Fails()
    {
        var options = new DynamicRoutingOptions { AllowedServices = ["", "fleet-account-service"] };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }
}
