using System.Net;

namespace Erus.Faas.ApiGateway.IntegrationTests;

public class HealthEndpointTests
{
    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    public async Task HealthEndpoints_ReturnHealthy(string healthApiPath)
    {
        // Arrange
        await using var apiGateway = IntegrationWebApplicationFactory.CreateApiGatewayService();
        using var client = await apiGateway.CreateHealthyClientAsync();

        // Act
        var response = await client.GetAsync(healthApiPath);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("healthy", content.Trim(), StringComparer.InvariantCultureIgnoreCase);
    }
}
