using System.Net;
using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Infrastructure;

/// <summary>
/// Validates <see cref="GatewayForwardedHeadersOptions"/> on startup.
/// </summary>
public sealed class GatewayForwardedHeadersOptionsValidator : IValidateOptions<GatewayForwardedHeadersOptions>
{
    public ValidateOptionsResult Validate(string? name, GatewayForwardedHeadersOptions? options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail($"{nameof(GatewayForwardedHeadersOptions)} cannot be null.");
        }

        var failures = new List<string>();

        if (options.ForwardLimit < 1)
        {
            failures.Add($"{nameof(options.ForwardLimit)} must be at least 1 in {GatewayForwardedHeadersOptions.Section} configuration.");
        }

        if (!options.TrustAll)
        {
            var knownNetworks = options.KnownNetworks ?? [];
            foreach (var network in knownNetworks)
            {
                if (string.IsNullOrWhiteSpace(network))
                {
                    failures.Add($"{nameof(options.KnownNetworks)} cannot contain empty or whitespace entries.");
                    continue;
                }

                try
                {
                    _ = IPNetwork.Parse(network.Trim());
                }
                catch (Exception)
                {
                    failures.Add($"{nameof(options.KnownNetworks)} contains invalid CIDR '{network}'.");
                }
            }

            var knownProxies = options.KnownProxies ?? [];
            foreach (var proxy in knownProxies)
            {
                if (string.IsNullOrWhiteSpace(proxy))
                {
                    failures.Add($"{nameof(options.KnownProxies)} cannot contain empty or whitespace entries.");
                    continue;
                }

                if (!IPAddress.TryParse(proxy.Trim(), out _))
                {
                    failures.Add($"{nameof(options.KnownProxies)} contains invalid IP address '{proxy}'.");
                }
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
