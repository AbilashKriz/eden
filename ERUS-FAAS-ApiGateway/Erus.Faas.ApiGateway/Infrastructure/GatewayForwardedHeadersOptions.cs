using System.ComponentModel.DataAnnotations;

namespace Erus.Faas.ApiGateway.Infrastructure;

/// <summary>
/// Configuration options for forwarded headers middleware.
/// </summary>
public sealed class GatewayForwardedHeadersOptions
{
    public const string Section = "ForwardedHeaders";

    /// <summary>
    /// Maximum number of forwarded headers to process.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "ForwardedHeaders ForwardLimit must be at least 1.")]
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    /// Trusted proxy networks in CIDR format.
    /// </summary>
    public List<string> KnownNetworks { get; set; } = [];

    /// <summary>
    /// Trusted proxy IP addresses.
    /// </summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>
    /// When true, trusts all forwarded headers regardless of sender.
    /// </summary>
    public bool TrustAll { get; set; }
}
