using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Infrastructure;

/// <summary>
/// Configuration for API Gateway rate limiting.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// Adds rate limiting with a fixed window per client IP.
    /// Configurable via RateLimiting:PermitLimit and RateLimiting:WindowSeconds.
    /// </summary>
    public static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var rateLimitingOptions = context.RequestServices
                    .GetRequiredService<IOptionsMonitor<RateLimitingOptions>>()
                    .CurrentValue;
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitingOptions.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitingOptions.WindowSeconds),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}

