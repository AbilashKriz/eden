using Eden.US.Fleet.Toolkit.Authentication;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Erus.Faas.ApiGateway.HealthChecks;

/// <summary>
/// Health check that verifies the TokenManager can acquire tokens from the IdP.
/// This helps detect IdP connectivity issues before they impact user requests.
/// </summary>
public sealed class TokenManagerHealthCheck(
    ITokenManager tokenManager,
    ILogger<TokenManagerHealthCheck> logger) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            var token = await tokenManager.GetAccessTokenAsync(cts.Token);

            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("TokenManager health check: received empty token");
                return HealthCheckResult.Degraded("TokenManager returned empty token");
            }

            return HealthCheckResult.Healthy("TokenManager can acquire tokens");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Propagate external cancellation
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("TokenManager health check timed out after {Timeout}s", Timeout.TotalSeconds);
            return HealthCheckResult.Degraded($"TokenManager timed out after {Timeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TokenManager health check failed");
            return HealthCheckResult.Unhealthy("TokenManager cannot acquire tokens", ex);
        }
    }
}

