using Eden.US.Fleet.Toolkit.Authentication;
using Eden.US.Fleet.Toolkit.Authentication.Azure;
using Eden.US.Fleet.Toolkit.Configuration;
using Erus.Faas.ApiGateway.Authentication.Azure;
using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Authentication;

/// <summary>
/// Extension methods for registering authentication services.
/// </summary>
public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers authentication (and token management) based on deployment environment.
    /// </summary>
    public static IServiceCollection AddGatewayAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        switch (configuration.GetDeployment())
        {
            case DeploymentType.Cloud:
                services.AddAzureAuthentication(configuration);
                services.AddAzureAdTokenManager(configuration);
                break;
            default:
                throw new NotSupportedException($"Deployment type '{configuration.GetDeployment()}' is not supported for authentication registration.");
        }

        return services;
    }

    /// <summary>
    /// Adds Azure AD and Azure AD B2C authentication with security validation.
    /// </summary>
    public static IServiceCollection AddAzureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Azure AD authentication from Toolkit
        services.AddAzureAdAuthentication(configuration);

        services.AddOptions<AzureAdB2COptions>()
            .Bind(configuration.GetSection(AzureAdB2COptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddSingleton<IValidateOptions<AzureAdB2COptions>, AzureAdB2COptionsValidator>();
        services.AddB2CPolicyValidation();
        services.AddAzureAdB2CAuthentication(configuration);
        
        return services;
    }
}
