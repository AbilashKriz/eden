using System.ComponentModel.DataAnnotations;

namespace Erus.Faas.ApiGateway.Infrastructure;

/// <summary>
/// Configuration options for API Gateway rate limiting.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    /// <summary>
    /// Max number of requests allowed per window per client.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "RateLimiting PermitLimit must be at least 1.")]
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Window size in seconds.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "RateLimiting WindowSeconds must be at least 1.")]
    public int WindowSeconds { get; set; } = 60;
}
