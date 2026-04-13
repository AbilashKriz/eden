using Eden.US.Fleet.Toolkit.Configuration;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Common;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Authentication.Keycloak;

public static class KeycloakConfiguration
{
    /// <summary>
    /// Add Keycloak JWT authentication to the service collection
    /// </summary>
    public static void AddKeycloakJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        
        if (configuration.GetDeployment() != DeploymentType.NonCloud)
            throw new NotSupportedException("Keycloak IDP is not supported in non-cloud environment");

        services.AddOptions<JwtValidationOptions>()
            .Bind(configuration.GetSection(JwtValidationOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtValidationOptions>, JwtValidationOptionsValidator>();

        var keycloakOptions = configuration.GetKeycloakOptions<KeycloakAuthenticationOptions>();
        ArgumentNullException.ThrowIfNull(keycloakOptions, $"{KeycloakAuthenticationOptions.Section} options are required");
        ArgumentException.ThrowIfNullOrWhiteSpace(keycloakOptions.Realm, $"{KeycloakAuthenticationOptions.Section}.{nameof(KeycloakAuthenticationOptions.Realm)} is required");
        ArgumentException.ThrowIfNullOrWhiteSpace(keycloakOptions.AuthServerUrl, $"{KeycloakAuthenticationOptions.Section}.{nameof(KeycloakAuthenticationOptions.AuthServerUrl)} is required");
        ArgumentException.ThrowIfNullOrWhiteSpace(keycloakOptions.Resource, $"{KeycloakAuthenticationOptions.Section}.{nameof(KeycloakAuthenticationOptions.Resource)} is required");
        ArgumentException.ThrowIfNullOrWhiteSpace(keycloakOptions.Credentials.Secret, $"{KeycloakAuthenticationOptions.Section}.{nameof(KeycloakAuthenticationOptions.Credentials)}.{nameof(KeycloakAuthenticationOptions.Credentials.Secret)} is required");

        var jwtValidationOptions = configuration.GetSection(JwtValidationOptions.Section).Get<JwtValidationOptions>();
        ArgumentNullException.ThrowIfNull(jwtValidationOptions, $"Configuration section '{JwtValidationOptions.Section}' not found.");
        var validIssuers = jwtValidationOptions.ValidIssuers;
        var validAudiences = jwtValidationOptions.ValidAudiences;
        
        services.AddKeycloakWebApiAuthentication(configuration,
            options =>
            {
                options.IncludeErrorDetails = true;
                options.RequireHttpsMetadata = false;

                options.Authority = keycloakOptions.KeycloakUrlRealm.TrimEnd('/');
                options.MetadataAddress = keycloakOptions.OpenIdConnectUrl ?? throw new ArgumentNullException(
                    $"{KeycloakAuthenticationOptions.Section}.{nameof(KeycloakAuthenticationOptions.OpenIdConnectUrl)} is required");
                
                var httpClientHandler = new HttpClientHandler();
                //Ignore certificate validation in case of self-signed certificates
                httpClientHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                
                var httpClient = new HttpClient(httpClientHandler)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    options.MetadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    httpClient)
                {
                    AutomaticRefreshInterval = TimeSpan.FromMinutes(30), // Refresh metadata every 30 minutes
                    RefreshInterval = TimeSpan.FromMinutes(1) // Min interval between refreshes
                };
                // To map the "sub" claim from the token to the user principal name
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudiences = validAudiences,
                    ValidateIssuer = true, 
                    ValidIssuers = validIssuers,
                    ValidateLifetime = true,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidateIssuerSigningKey = false, // Disable default signing key validation
                    // Custom signature validation for self-signed certificates in non-cloud environments
                    SignatureValidator = configuration.GetDeployment() == DeploymentType.NonCloud
                        ? (token, _) => new JsonWebToken(token)
                        : null // Use default validation in production
                };
            }
        );
    }
}