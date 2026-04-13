using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Infrastructure;

/// <summary>
/// Configuration for forwarded headers when running behind proxies/ingress.
/// </summary>
public static class ForwardedHeadersConfiguration
{
    /// <summary>
    /// Registers forwarded headers configuration options with validation.
    /// </summary>
    public static IServiceCollection AddGatewayForwardedHeadersOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GatewayForwardedHeadersOptions>()
            .Bind(configuration.GetSection(GatewayForwardedHeadersOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GatewayForwardedHeadersOptions>, GatewayForwardedHeadersOptionsValidator>();

        return services;
    }

    /// <summary>
    /// Configures and applies forwarded headers middleware.
    /// Supports configuration via ForwardedHeaders:ForwardLimit, KnownNetworks, KnownProxies, and TrustAll.
    /// </summary>
    public static IApplicationBuilder UseGatewayForwardedHeaders(
        this IApplicationBuilder app,
        ILogger logger)
    {
        var gatewayOptions = app.ApplicationServices
            .GetRequiredService<IOptions<GatewayForwardedHeadersOptions>>()
            .Value;

        return app.UseGatewayForwardedHeaders(gatewayOptions, logger);
    }

    private static IApplicationBuilder UseGatewayForwardedHeaders(
        this IApplicationBuilder app,
        GatewayForwardedHeadersOptions gatewayOptions,
        ILogger logger)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            RequireHeaderSymmetry = true,
            ForwardLimit = gatewayOptions.ForwardLimit
        };

        var knownNetworks = (gatewayOptions.KnownNetworks ?? []).ToArray();
        var knownProxies = (gatewayOptions.KnownProxies ?? []).ToArray();
        var trustAll = gatewayOptions.TrustAll;

        if (trustAll)
        {
            ConfigureTrustAll(options, logger);
        }
        else if (knownNetworks.Length > 0 || knownProxies.Length > 0)
        {
            ConfigureKnownProxies(options, knownNetworks, knownProxies);
        }

        return app.UseForwardedHeaders(options);
    }

    private static void ConfigureTrustAll(ForwardedHeadersOptions options, ILogger logger)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        logger.LogWarning(
            "ForwardedHeaders:TrustAll is enabled. Ensure upstream network policies prevent spoofed forwarded headers.");
    }

    private static void ConfigureKnownProxies(
        ForwardedHeadersOptions options, 
        string[] knownNetworks, 
        string[] knownProxies)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var cidr in knownNetworks.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr.Trim()));
        }

        foreach (var proxy in knownProxies.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy.Trim()));
        }
    }
}

