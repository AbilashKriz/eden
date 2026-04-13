using Eden.US.Fleet.Toolkit.Authentication;
using Moq;
using System.Net;
using System.Net.Http.Headers;

namespace Erus.Faas.ApiGateway.IntegrationTests;

public class ProxyForwardingTests
{
    [Fact]
    public async Task Gateway_UnconfiguredRoute_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        await using var apiGateway = IntegrationWebApplicationFactory.CreateApiGatewayService();
        using var apiGatewayClient = await apiGateway.CreateHealthyClientAsync();

        // Act - Route exists but no auth header
        var response = await apiGatewayClient.GetAsync("/unconfigured-service/test");

        // Assert - Returns 401 (Unauthorized) when auth is required but not provided
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_ConfiguredRoute_WithAuthHeader_PassesMiddlewareCheck()
    {
        // Arrange
        var tokenManager = new Mock<ITokenManager>();
        tokenManager.Setup(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("internal-token");

        await using var apiGateway = IntegrationWebApplicationFactory.CreateApiGatewayService(
            services => IntegrationTestHelpers.ReplaceTokenManager(services, tokenManager.Object));
        using var apiGatewayClient = await apiGateway.CreateHealthyClientAsync();
        
        // Reset invocations after health check setup
        tokenManager.Invocations.Clear();
        
        var testToken = IntegrationTestHelpers.CreateTestJwtToken();
        apiGatewayClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", testToken);

        // Act
        var response = await apiGatewayClient.GetAsync("/test/api/endpoint");

        // Assert - The middleware passes the request (Authorization header exists)
        // No downstream exists → YARP returns 502 BadGateway for invalid token scenario
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        tokenManager.Verify(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Gateway_PublicRoute_AllowsAnyRequest()
    {
        // Arrange - Override routes to ensure clean test config
        // Also add Order to ensure this route takes precedence over dynamic-service-route
        var configOverrides = new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:public-test-route:ClusterId"] = "public-cluster",
            ["ReverseProxy:Routes:public-test-route:Match:Path"] = "/public/{**catch-all}",
            ["ReverseProxy:Routes:public-test-route:Order"] = "1",
            ["ReverseProxy:Routes:public-test-route:AuthorizationPolicy"] = "Public",
            ["ReverseProxy:Clusters:public-cluster:Destinations:dest:Address"] = "http://localhost:29998",
            // Disable the dynamic catch-all route to avoid conflicts
            ["ReverseProxy:Routes:dynamic-service-route:Order"] = "999"
        };

        await using var apiGateway = IntegrationWebApplicationFactory.CreateApiGatewayService(
            configOverrides: configOverrides);
        using var apiGatewayClient = await apiGateway.CreateHealthyClientAsync();

        // Act - Public route doesn't require auth
        var response = await apiGatewayClient.GetAsync("/public/api/endpoint");

        // Assert - Passes middleware auth check (public route)
        // Returns 502 because no downstream service exists
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_ConfiguredRoute_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        await using var apiGateway = IntegrationWebApplicationFactory.CreateApiGatewayService();
        using var apiGatewayClient = await apiGateway.CreateHealthyClientAsync();

        // Act
        var response = await apiGatewayClient.GetAsync("/test/api/endpoint");

        // Assert - Expect 401 (Unauthorized) since auth is required
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

}


