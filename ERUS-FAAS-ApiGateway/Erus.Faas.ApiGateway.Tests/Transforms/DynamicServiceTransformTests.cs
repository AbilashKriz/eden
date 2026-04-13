using Erus.Faas.ApiGateway.Infrastructure;
using Erus.Faas.ApiGateway.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Erus.Faas.ApiGateway.Tests.Transforms;

public class DynamicServiceTransformTests
{
    private readonly Mock<ILogger<DynamicServiceTransform>> _logger = new();

    private static (IOptions<GatewayOptions> GatewayOptions, IOptions<DynamicRoutingOptions> RoutingOptions) CreateOptions(
        string[]? allowedServices = null,
        string? appName = null)
    {
        var gatewayOptions = new GatewayOptions { AppName = appName };
        var routingOptions = new DynamicRoutingOptions
        {
            AllowedServices = allowedServices?.ToList() ?? []
        };

        return (Options.Create(gatewayOptions), Options.Create(routingOptions));
    }

    [Fact]
    public void Constructor_WithAllowlist_LogsEnabledMessage()
    {
        // Arrange
        var (gatewayOptions, routingOptions) = CreateOptions(["fleet-account-service", "fleet-company-service"]);

        // Act
        var transform = new DynamicServiceTransform(_logger.Object, gatewayOptions, routingOptions);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("allowlist enabled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithoutAllowlist_LogsWarning()
    {
        // Arrange
        var (gatewayOptions, routingOptions) = CreateOptions();

        // Act
        var transform = new DynamicServiceTransform(_logger.Object, gatewayOptions, routingOptions);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NOT configured")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Transform_ServiceInAllowlist_AllowsRouting()
    {
        // Arrange
        var (gatewayOptions, routingOptions) = CreateOptions(["fleet-account-service"]);
        var transform = new DynamicServiceTransform(_logger.Object, gatewayOptions, routingOptions);
        
        var ctx = new DefaultHttpContext();
        ctx.Request.RouteValues["service-name"] = "fleet-account-service";
        
        var proxyRequest = new HttpRequestMessage();
        var transformContext = new RequestTransformContext
        {
            HttpContext = ctx,
            ProxyRequest = proxyRequest,
            Path = "/api/test",
            Query = new QueryTransformContext(ctx.Request),
            DestinationPrefix = "http://env-{service-name}:8080"
        };

        // Act - Get the transform action and execute it
        var builderContext = CreateTransformBuilderContext("dynamic-service-route");
        transform.Apply(builderContext);
        
        // The transform should have been added
        Assert.NotEmpty(builderContext.RequestTransforms);
        
        // Execute the transform
        await builderContext.RequestTransforms[0].ApplyAsync(transformContext);

        // Assert
        Assert.Equal(200, ctx.Response.StatusCode); // Default, not set to error
        Assert.Equal("http://env-fleet-account-service:8080", transformContext.DestinationPrefix);
    }

    [Fact]
    public async Task Transform_ServiceNotInAllowlist_Returns403()
    {
        // Arrange
        var (gatewayOptions, routingOptions) = CreateOptions(["fleet-account-service"]);
        var transform = new DynamicServiceTransform(_logger.Object, gatewayOptions, routingOptions);
        
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.RouteValues["service-name"] = "unauthorized-service";
        
        var proxyRequest = new HttpRequestMessage();
        var transformContext = new RequestTransformContext
        {
            HttpContext = ctx,
            ProxyRequest = proxyRequest,
            Path = "/api/test",
            Query = new QueryTransformContext(ctx.Request),
            DestinationPrefix = "http://env-{service-name}:8080"
        };

        // Act
        var builderContext = CreateTransformBuilderContext("dynamic-service-route");
        transform.Apply(builderContext);
        await builderContext.RequestTransforms[0].ApplyAsync(transformContext);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Transform_NoAllowlist_AllowsAnyValidService()
    {
        // Arrange
        var (gatewayOptions, routingOptions) = CreateOptions(); // No allowlist
        var transform = new DynamicServiceTransform(_logger.Object, gatewayOptions, routingOptions);
        
        var ctx = new DefaultHttpContext();
        ctx.Request.RouteValues["service-name"] = "any-valid-service";
        
        var proxyRequest = new HttpRequestMessage();
        var transformContext = new RequestTransformContext
        {
            HttpContext = ctx,
            ProxyRequest = proxyRequest,
            Path = "/api/test",
            Query = new QueryTransformContext(ctx.Request),
            DestinationPrefix = "http://env-{service-name}:8080"
        };

        // Act
        var builderContext = CreateTransformBuilderContext("dynamic-service-route");
        transform.Apply(builderContext);
        await builderContext.RequestTransforms[0].ApplyAsync(transformContext);

        // Assert
        Assert.Equal(200, ctx.Response.StatusCode); // Default, not error
        Assert.Equal("http://env-any-valid-service:8080", transformContext.DestinationPrefix);
    }

    [Fact]
    public async Task Transform_SelfRouting_Returns400()
    {
        // Arrange
        var (gatewayOptions, routingOptions) = CreateOptions(appName: "fleet-apigateway-service");
        var transform = new DynamicServiceTransform(_logger.Object, gatewayOptions, routingOptions);
        
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.RouteValues["service-name"] = "localhost";
        
        var proxyRequest = new HttpRequestMessage();
        var transformContext = new RequestTransformContext
        {
            HttpContext = ctx,
            ProxyRequest = proxyRequest,
            Path = "/api/test",
            Query = new QueryTransformContext(ctx.Request),
            DestinationPrefix = "http://{service-name}:8080"
        };

        // Act
        var builderContext = CreateTransformBuilderContext("dynamic-service-route");
        transform.Apply(builderContext);
        await builderContext.RequestTransforms[0].ApplyAsync(transformContext);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Transform_InvalidServiceName_Returns400()
    {
        // Arrange
        var (gatewayOptions, routingOptions) = CreateOptions(["fleet-account-service"]);
        var transform = new DynamicServiceTransform(_logger.Object, gatewayOptions, routingOptions);
        
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.RouteValues["service-name"] = "invalid@service";
        
        var proxyRequest = new HttpRequestMessage();
        var transformContext = new RequestTransformContext
        {
            HttpContext = ctx,
            ProxyRequest = proxyRequest,
            Path = "/api/test",
            Query = new QueryTransformContext(ctx.Request),
            DestinationPrefix = "http://env-{service-name}:8080"
        };

        // Act
        var builderContext = CreateTransformBuilderContext("dynamic-service-route");
        transform.Apply(builderContext);
        await builderContext.RequestTransforms[0].ApplyAsync(transformContext);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
    }

    [Fact]
    public void Apply_NonDynamicRoute_DoesNotAddTransform()
    {
        // Arrange
        var (gatewayOptions, routingOptions) = CreateOptions();
        var transform = new DynamicServiceTransform(_logger.Object, gatewayOptions, routingOptions);
        
        var builderContext = CreateTransformBuilderContext("some-other-route");

        // Act
        transform.Apply(builderContext);

        // Assert
        Assert.Empty(builderContext.RequestTransforms);
    }

    private static TransformBuilderContext CreateTransformBuilderContext(string routeId)
    {
        return new TransformBuilderContext
        {
            Route = new RouteConfig { RouteId = routeId },
            Cluster = new ClusterConfig { ClusterId = "test-cluster" },
            Services = new ServiceCollection().BuildServiceProvider()
        };
    }
}

