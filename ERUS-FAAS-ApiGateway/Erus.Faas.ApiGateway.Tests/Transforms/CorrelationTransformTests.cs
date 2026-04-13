using Correlate;
using Eden.US.Fleet.Toolkit.Configuration;
using Erus.Faas.ApiGateway.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Yarp.ReverseProxy.Transforms;

namespace Erus.Faas.ApiGateway.Tests.Transforms;

public class CorrelationTransformTests
{
    private readonly Mock<ILogger<CorrelationTransform>> _logger = new();
    private readonly Mock<ICorrelationContextAccessor> _correlationContextAccessor = new();

    [Fact]
    public async Task TransformRequestAsync_AddsCorrelationId_WhenContextHasCorrelationId()
    {
        // Arrange
        const string expectedCorrelationId = "test-correlation-id-123";
        var correlationContext = new CorrelationContext { CorrelationId = expectedCorrelationId };
        _correlationContextAccessor.Setup(x => x.CorrelationContext).Returns(correlationContext);

        var transform = new CorrelationTransform(_logger.Object, _correlationContextAccessor.Object);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/test";
        var proxy = new HttpRequestMessage();
        var tctx = new RequestTransformContext 
        { 
            HttpContext = ctx, 
            ProxyRequest = proxy, 
            Path = "/api/test", 
            Query = new QueryTransformContext(ctx.Request) 
        };

        // Act
        await transform.TransformRequestAsync(tctx);

        // Assert
        Assert.True(proxy.Headers.Contains(Constants.Headers.CorrelationIdHeaderName));
        Assert.Equal(expectedCorrelationId, proxy.Headers.GetValues(Constants.Headers.CorrelationIdHeaderName).First());
    }

    [Fact]
    public async Task TransformRequestAsync_DoesNotAddHeader_WhenCorrelationContextIsNull()
    {
        // Arrange
        _correlationContextAccessor.Setup(x => x.CorrelationContext).Returns((CorrelationContext?)null);

        var transform = new CorrelationTransform(_logger.Object, _correlationContextAccessor.Object);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/test";
        var proxy = new HttpRequestMessage();
        var tctx = new RequestTransformContext 
        { 
            HttpContext = ctx, 
            ProxyRequest = proxy, 
            Path = "/api/test", 
            Query = new QueryTransformContext(ctx.Request) 
        };

        // Act
        await transform.TransformRequestAsync(tctx);

        // Assert
        Assert.False(proxy.Headers.Contains(Constants.Headers.CorrelationIdHeaderName));
    }

    [Fact]
    public async Task TransformRequestAsync_DoesNotAddHeader_WhenCorrelationIdIsEmpty()
    {
        // Arrange
        var correlationContext = new CorrelationContext { CorrelationId = string.Empty };
        _correlationContextAccessor.Setup(x => x.CorrelationContext).Returns(correlationContext);

        var transform = new CorrelationTransform(_logger.Object, _correlationContextAccessor.Object);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/test";
        var proxy = new HttpRequestMessage();
        var tctx = new RequestTransformContext 
        { 
            HttpContext = ctx, 
            ProxyRequest = proxy, 
            Path = "/api/test", 
            Query = new QueryTransformContext(ctx.Request) 
        };

        // Act
        await transform.TransformRequestAsync(tctx);

        // Assert
        Assert.False(proxy.Headers.Contains(Constants.Headers.CorrelationIdHeaderName));
    }

    [Fact]
    public async Task TransformRequestAsync_DoesNotAddHeader_WhenCorrelationIdIsNull()
    {
        // Arrange
        var correlationContext = new CorrelationContext { CorrelationId = null! };
        _correlationContextAccessor.Setup(x => x.CorrelationContext).Returns(correlationContext);

        var transform = new CorrelationTransform(_logger.Object, _correlationContextAccessor.Object);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/test";
        var proxy = new HttpRequestMessage();
        var tctx = new RequestTransformContext 
        { 
            HttpContext = ctx, 
            ProxyRequest = proxy, 
            Path = "/api/test", 
            Query = new QueryTransformContext(ctx.Request) 
        };

        // Act
        await transform.TransformRequestAsync(tctx);

        // Assert
        Assert.False(proxy.Headers.Contains(Constants.Headers.CorrelationIdHeaderName));
    }

    [Fact]
    public async Task TransformRequestAsync_PreservesExistingHeaders()
    {
        // Arrange
        const string expectedCorrelationId = "test-correlation-id-456";
        var correlationContext = new CorrelationContext { CorrelationId = expectedCorrelationId };
        _correlationContextAccessor.Setup(x => x.CorrelationContext).Returns(correlationContext);

        var transform = new CorrelationTransform(_logger.Object, _correlationContextAccessor.Object);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/test";
        var proxy = new HttpRequestMessage();
        proxy.Headers.TryAddWithoutValidation("X-Custom-Header", "custom-value");
        
        var tctx = new RequestTransformContext 
        { 
            HttpContext = ctx, 
            ProxyRequest = proxy, 
            Path = "/api/test", 
            Query = new QueryTransformContext(ctx.Request) 
        };

        // Act
        await transform.TransformRequestAsync(tctx);

        // Assert
        Assert.True(proxy.Headers.Contains("X-Custom-Header"));
        Assert.True(proxy.Headers.Contains(Constants.Headers.CorrelationIdHeaderName));
    }

    [Fact]
    public async Task TransformRequestAsync_LogsDebug_WhenCorrelationIdPropagated()
    {
        // Arrange
        const string expectedCorrelationId = "logged-correlation-id";
        var correlationContext = new CorrelationContext { CorrelationId = expectedCorrelationId };
        _correlationContextAccessor.Setup(x => x.CorrelationContext).Returns(correlationContext);

        var transform = new CorrelationTransform(_logger.Object, _correlationContextAccessor.Object);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/test";
        var proxy = new HttpRequestMessage();
        var tctx = new RequestTransformContext 
        { 
            HttpContext = ctx, 
            ProxyRequest = proxy, 
            Path = "/api/test", 
            Query = new QueryTransformContext(ctx.Request) 
        };

        // Act
        await transform.TransformRequestAsync(tctx);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedCorrelationId)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
