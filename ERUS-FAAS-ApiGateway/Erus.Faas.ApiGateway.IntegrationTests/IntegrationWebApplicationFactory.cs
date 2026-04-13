using Microsoft.IdentityModel.JsonWebTokens;
using System.Net;
using Eden.US.Fleet.Toolkit.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Erus.Faas.ApiGateway.IntegrationTests;

public static class IntegrationWebApplicationFactory
{
    public static WebApplicationFactory<Program> CreateApiGatewayService(
        Action<IServiceCollection>? configureServices = null,
        Dictionary<string, string?>? configOverrides = null,
        bool useTestAuthentication = true)
    {
        // Use a Cloud environment name so Toolkit GetDeployment() resolves to Cloud.
        // We still load appsettings.Local.json explicitly to keep local reverse-proxy test config.
        var environment = "Fleet-Dev-Uks";

        // The Toolkit derives DeploymentType from the "Environment" config key.
        // The ApiGateway currently only supports Cloud authentication registration.
        // Setting this as an env var ensures it wins over appsettings.* values.
        Environment.SetEnvironmentVariable(
            Eden.US.Fleet.Toolkit.Configuration.Constants.AppConfig.EnvironmentConfigName,
            "Fleet-Dev-Uks");

        // appsettings.json contains placeholder values (e.g. "__B2CTenantId__") that intentionally
        // fail validation. Integration tests override them with safe dummy values.
        var defaultTestConfig = new Dictionary<string, string?>
        {
            // IMPORTANT: The Toolkit derives DeploymentType from the "Environment" config key.
            // The ApiGateway only supports Cloud authentication registration today; Local/Sandbox/Test are NonCloud.
            // To keep integration tests runnable offline, force a Cloud environment and stub JWT validation below.
            [Eden.US.Fleet.Toolkit.Configuration.Constants.AppConfig.EnvironmentConfigName] = "Fleet-Dev-Uks",

            // AzureAd (internal service token) configuration
            ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
            ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000001",
            // Must satisfy Eden toolkit validation (min length 16).
            ["AzureAd:ClientSecret"] = "test-client-secret-012345",
            ["AzureAd:Scopes:0"] = "api://00000000-0000-0000-0000-000000000001/.default",

            // AzureAdB2C (user token) configuration - required for startup validation
            ["AzureAdB2C:Instance"] = "https://test.b2clogin.com",
            ["AzureAdB2C:Domain"] = "test.onmicrosoft.com",
            ["AzureAdB2C:ClientId"] = "00000000-0000-0000-0000-000000000002",
            ["AzureAdB2C:SignUpSignInPolicyId"] = "B2C_1_SignIn",
        };

        var apiGatewayFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    // Keep local routing config even though we run with a Cloud environment name.
                    config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

                    config.AddInMemoryCollection(defaultTestConfig);

                    if (configOverrides != null)
                    {
                        config.AddInMemoryCollection(configOverrides);
                    }
                });

                builder.ConfigureTestServices(services =>
                {
                    if (useTestAuthentication)
                    {
                        // The app registers JwtBearer which brings in JwtBearerPostConfigureOptions.
                        // That post-configure throws if Authority/MetadataAddress are non-HTTPS.
                        // In tests we don't want any metadata fetching at all, so remove that post-configure.
                        var jwtBearerPostConfigureDescriptors = services
                            .Where(d => d.ServiceType == typeof(IPostConfigureOptions<JwtBearerOptions>))
                            .Where(d =>
                                d.ImplementationType?.Name == "JwtBearerPostConfigureOptions" ||
                                d.ImplementationInstance?.GetType().Name == "JwtBearerPostConfigureOptions")
                            .ToList();

                        foreach (var descriptor in jwtBearerPostConfigureDescriptors)
                        {
                            services.Remove(descriptor);
                        }

                        // Configure JWT Bearer authentication to accept our test tokens
                        // PostConfigureAll runs after all other configurations
                        services.PostConfigureAll<JwtBearerOptions>(options =>
                        {
                            // Reset TokenValidationParameters entirely for tests
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuerSigningKey = false,
                                ValidateIssuer = false,
                                ValidateAudience = false,
                                ValidateLifetime = false,
                                SignatureValidator = (token, _) => new JsonWebToken(token),
                                // Ensure we don't hit network for metadata
                                RequireSignedTokens = false
                            };

                            // Don't retrieve signing keys from authority
                            options.RequireHttpsMetadata = false;
                            options.Authority = "https://localhost";
                            options.MetadataAddress = "https://localhost/.well-known/openid-configuration";

                            var staticConfig = new OpenIdConnectConfiguration();
                            options.Configuration = staticConfig;
                            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(staticConfig);
                        });
                        
                    }

                    // Replace the real token manager with a test one (required for health checks)
                    // Do this BEFORE configureServices so tests can override if needed
                    var existingTokenManager = services.FirstOrDefault(d => d.ServiceType == typeof(ITokenManager));
                    if (existingTokenManager != null)
                    {
                        services.Remove(existingTokenManager);
                    }
                    services.AddScoped<ITokenManager>(_ => new TestTokenManager());

                    // Let the test configure services (can override token manager)
                    configureServices?.Invoke(services);
                });
            });

        return apiGatewayFactory;
    }

    private const int MaxRetries = 50;
    private const int DelayMilliseconds = 100;

    public static async Task<HttpClient> CreateHealthyClientAsync(this WebApplicationFactory<Program> factory)
    {
        HttpClient? client = null;
        Exception? lastException = null;
        HttpStatusCode? lastStatusCode = null;
        string? lastBody = null;

        for (var i = 0; i < MaxRetries; i++)
        {
            try
            {
                client = factory.CreateClient();
                var response = await client.GetAsync("/health/ready");
                lastStatusCode = response.StatusCode;
                var content = await response.Content.ReadAsStringAsync();
                lastBody = content;

                if (response.StatusCode == HttpStatusCode.OK &&
                    content.Trim().Equals("healthy", StringComparison.InvariantCultureIgnoreCase))
                {
                    return client; // Application is ready!
                }

                client.Dispose();
                client = null;
            }
            catch (Exception ex)
            {
                lastException = ex;
                client?.Dispose();
                client = null;
            }

            await Task.Delay(DelayMilliseconds);
        }

        var timeoutMessage = $"Application did not become ready within {MaxRetries * DelayMilliseconds}ms.";
        var lastError = lastException?.Message;

        if (lastError == null)
        {
            var statusPart = lastStatusCode != null ? $"Status: {(int)lastStatusCode} {lastStatusCode}." : "Status: (none).";
            var bodyPart = lastBody != null ? $"Body: {lastBody}" : "Body: (none).";
            lastError = $"Health check did not return expected response. {statusPart} {bodyPart}";
        }

        throw new TimeoutException($"{timeoutMessage} Last error: {lastError}", lastException);
    }
}
