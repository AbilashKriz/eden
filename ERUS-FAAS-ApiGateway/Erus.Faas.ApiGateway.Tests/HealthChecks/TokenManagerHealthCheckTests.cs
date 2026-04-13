using Eden.US.Fleet.Toolkit.Authentication;
using Erus.Faas.ApiGateway.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;

namespace Erus.Faas.ApiGateway.Tests.HealthChecks;

public class TokenManagerHealthCheckTests
{
    private readonly Mock<ITokenManager> _tokenManager = new();
    private readonly Mock<ILogger<TokenManagerHealthCheck>> _logger = new();

    [Fact]
    public async Task CheckHealthAsync_TokenAcquired_ReturnsHealthy()
    {
        // Arrange
        _tokenManager.Setup(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid-token");
        
        var healthCheck = new TokenManagerHealthCheck(_tokenManager.Object, _logger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("can acquire tokens", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_EmptyToken_ReturnsDegraded()
    {
        // Arrange
        _tokenManager.Setup(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        
        var healthCheck = new TokenManagerHealthCheck(_tokenManager.Object, _logger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("empty token", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_NullToken_ReturnsDegraded()
    {
        // Arrange
        _tokenManager.Setup(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null!);
        
        var healthCheck = new TokenManagerHealthCheck(_tokenManager.Object, _logger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_TokenManagerThrows_ReturnsUnhealthy()
    {
        // Arrange
        _tokenManager.Setup(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("IdP connection failed"));
        
        var healthCheck = new TokenManagerHealthCheck(_tokenManager.Object, _logger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("cannot acquire tokens", result.Description);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_ExternalCancellation_PropagatesException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        _tokenManager.Setup(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        
        var healthCheck = new TokenManagerHealthCheck(_tokenManager.Object, _logger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => healthCheck.CheckHealthAsync(context, cts.Token));
    }

    [Fact]
    public async Task CheckHealthAsync_InternalTimeout_ReturnsDegraded()
    {
        // Arrange
        _tokenManager.Setup(m => m.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct); // Longer than 10s timeout
                return "token";
            });
        
        var healthCheck = new TokenManagerHealthCheck(_tokenManager.Object, _logger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("timed out", result.Description);
    }
}

