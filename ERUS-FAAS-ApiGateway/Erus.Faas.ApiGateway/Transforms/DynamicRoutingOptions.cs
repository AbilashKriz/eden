namespace Erus.Faas.ApiGateway.Transforms;

/// <summary>
/// Configuration options for dynamic routing.
/// </summary>
public sealed class DynamicRoutingOptions
{
    public const string Section = "DynamicRouting";

    /// <summary>
    /// Explicit allowlist of services that can be routed dynamically.
    /// </summary>
    public List<string> AllowedServices { get; set; } = [];
}
