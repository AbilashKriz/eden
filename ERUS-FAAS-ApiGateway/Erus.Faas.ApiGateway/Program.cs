using Eden.US.Fleet.Toolkit.Configuration;
using Erus.Faas.ApiGateway.Authentication;
using Erus.Faas.ApiGateway.Authorization;
using Erus.Faas.ApiGateway.Infrastructure;
using Erus.Faas.ApiGateway.HealthChecks;
using Erus.Faas.ApiGateway.Middleware;
using Erus.Faas.ApiGateway.Transforms;
using Microsoft.Extensions.Options;
using Serilog;

namespace Erus.Faas.ApiGateway;

public class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        // The initial "bootstrap" logger is able to log errors during start-up. It's completely replaced by the
        // logger configured in `AddSerilog()` below, once configuration and dependency-injection have both been
        // set up successfully.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureServices(builder);

            var app = builder.Build();

            ConfigurePipeline(app, builder.Configuration);

            Log.Information("Starting up the application.");
            await app.RunAsync();
            Log.Information("Stopped cleanly");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // Logging & Correlation
        services.AddToolkitSerilog(configuration);
        services.AddToolkitCorrelate();
        services.AddHttpContextAccessor();

        // Caching
        services.AddDistributedMemoryCache();

        // Configuration Options
        services.AddOptions<GatewayOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GatewayOptions>, GatewayOptionsValidator>();
        services.AddOptions<DynamicRoutingOptions>()
            .Bind(configuration.GetSection(DynamicRoutingOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DynamicRoutingOptions>, DynamicRoutingOptionsValidator>();
        services.AddGatewayForwardedHeadersOptions(configuration);

        // Authentication & Authorization
        services.AddGatewayAuthentication(configuration);
        services.AddCustomAuthorizationPolicies(configuration);

        // Reverse Proxy with Transforms
        services
            .AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms<CorrelationTransform>()
            .AddTransforms<DynamicServiceTransform>()
            .AddTransforms<TokenExchangeTransform>();

        // Health Checks
        services.AddToolkitHealthChecks();
        services.AddHealthChecks()
            .AddCheck<TokenManagerHealthCheck>("token-manager", tags: ["ready"]);

        // CORS
        services.AddToolkitCors(configuration);

        // Rate Limiting
        services.AddGatewayRateLimiting(configuration);
    }

    private static void ConfigurePipeline(WebApplication app, IConfiguration configuration)
    {
        // Development-only settings
        if (configuration.GetDeployment() == DeploymentType.NonCloud)
        {
            EnableDevelopmentMode(app);
        }

        // Request pipeline
        app.UseSerilogRequestLogging();
        app.UseSecurityHeaders();
        app.UseToolkitCorrelate();
        app.UseGatewayForwardedHeaders(app.Logger);

        // Routing & Security
        app.UseRouting();
        app.UseToolkitCors();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        // Endpoints
        app.UseToolkitHealthChecks();
        app.MapReverseProxy();
    }

    private static void EnableDevelopmentMode(WebApplication app)
    {
        Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
        Log.Warning("PII logging is enabled (ShowPII=true). This should NEVER be enabled in production environments.");
        app.UseDeveloperExceptionPage();
    }
}
