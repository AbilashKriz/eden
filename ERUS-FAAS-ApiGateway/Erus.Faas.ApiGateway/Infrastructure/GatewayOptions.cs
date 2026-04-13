namespace Erus.Faas.ApiGateway.Infrastructure;

/// <summary>
/// Gateway-level configuration options.
/// </summary>
public sealed class GatewayOptions
{
    /// <summary>
    /// Optional application name used for self-routing detection.
    /// </summary>
    public string? AppName { get; set; }
}
