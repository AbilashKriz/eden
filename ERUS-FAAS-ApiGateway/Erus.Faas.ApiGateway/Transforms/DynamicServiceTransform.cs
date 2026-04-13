using Erus.Faas.ApiGateway.Infrastructure;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Erus.Faas.ApiGateway.Transforms;

public sealed class DynamicServiceTransform : ITransformProvider
{
    private readonly ILogger<DynamicServiceTransform> _logger;
    private readonly HashSet<string> _gatewayHostnames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedServices = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _enforceAllowlist;

    public DynamicServiceTransform(
        ILogger<DynamicServiceTransform> logger,
        IOptions<GatewayOptions> gatewayOptions,
        IOptions<DynamicRoutingOptions> routingOptions)
    {
        _logger = logger;

        // Collect potential gateway hostnames
        _gatewayHostnames.Add("localhost");
        _gatewayHostnames.Add("127.0.0.1");
        _gatewayHostnames.Add("::1");
        
        // Add configured service name variants
        var appName = gatewayOptions.Value.AppName;
        if (!string.IsNullOrWhiteSpace(appName))
        {
            _gatewayHostnames.Add(appName);
            _gatewayHostnames.Add(appName.Replace("-", "")); // without dashes
        }
        
        // Add machine hostname
        var hostname = Environment.MachineName;
        if (!string.IsNullOrEmpty(hostname))
        {
            _gatewayHostnames.Add(hostname);
        }
        
        // Load allowed services from configuration
        var allowedServicesConfig = routingOptions.Value.AllowedServices ?? [];
        if (allowedServicesConfig.Count > 0)
        {
            foreach (var service in allowedServicesConfig.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                _allowedServices.Add(service.Trim());
            }
            _enforceAllowlist = true;
            _logger.LogInformation("Dynamic routing allowlist enabled with {Count} services: {Services}", 
                _allowedServices.Count, string.Join(", ", _allowedServices));
        }
        else
        {
            _enforceAllowlist = false;
            _logger.LogWarning("Dynamic routing allowlist is NOT configured. All valid service names will be routed.");
        }
        
        _logger.LogInformation("Gateway hostnames for self-routing detection: {Hostnames}", 
            string.Join(", ", _gatewayHostnames));
    }

    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        // Only apply to routes with the dynamic-service-route pattern
        if (!context.Route.RouteId.Equals("dynamic-service-route", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Request.RouteValues.TryGetValue("service-name", out var serviceNameObj) 
                && serviceNameObj is string serviceName)
            {
                if (!DynamicServiceName.TryNormalize(serviceName, out var normalizedServiceName))
                {
                    _logger.LogWarning(
                        "Rejected dynamic service route due to invalid service-name '{ServiceName}'",
                        serviceName);
                    transformContext.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await transformContext.HttpContext.Response.WriteAsync("Invalid service-name");
                    return;
                }

                // Check against allowlist
                if (_enforceAllowlist && !_allowedServices.Contains(normalizedServiceName))
                {
                    _logger.LogWarning(
                        "Rejected dynamic service route: service '{ServiceName}' is not in the allowlist",
                        normalizedServiceName);
                    transformContext.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await transformContext.HttpContext.Response.WriteAsync("Service not allowed");
                    return;
                }

                var originalPrefix = transformContext.DestinationPrefix;
                var newDestination = originalPrefix.Replace("{service-name}", normalizedServiceName, StringComparison.OrdinalIgnoreCase);
                
                // Check if destination hostname matches gateway
                if (Uri.TryCreate(newDestination, UriKind.Absolute, out var destUri))
                {
                    if (_gatewayHostnames.Contains(destUri.Host, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Blocked self-routing: destination hostname {Hostname} matches gateway", destUri.Host);
                        transformContext.HttpContext.Response.StatusCode = 400;
                        await transformContext.HttpContext.Response.WriteAsync("Circular routing detected");
                        return;
                    }
                }
                
                transformContext.DestinationPrefix = newDestination;
            }

            await Task.CompletedTask;
        });
    }
}

