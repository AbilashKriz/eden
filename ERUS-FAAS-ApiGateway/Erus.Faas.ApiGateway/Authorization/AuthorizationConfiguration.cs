using Eden.US.Fleet.Toolkit.Configuration;
using Erus.Faas.ApiGateway.Authentication;
using Erus.Faas.ApiGateway.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Erus.Faas.ApiGateway.Authorization;

/// <summary>
/// Authorization policy names for the API Gateway.
/// Use these constants in route configuration AuthorizationPolicy.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Policy for external service M2M tokens with Gateway.Access role.</summary>
    public const string ExternalService = "ExternalService";
    
    /// <summary>Policy for B2C user tokens with access_as_user scope.</summary>
    public const string User = "User";

    /// <summary>
    /// Policy that accepts either a valid user token (B2C) or an external service M2M token.
    /// Used as the fallback/default policy for routes that serve both audiences.
    /// </summary>
    public const string Authenticated = "Authenticated"; //TODO: DM Remove this policy when dynamic routing deprecated

    /// <summary>Policy for public access. No authentication required.</summary>
    public const string Public = "Public";
}

/// <summary>
/// Configures authorization policies with proper claim validation.
/// Aligns with Azure AD app registration security controls.
/// </summary>
public static class AuthorizationConfiguration
{
    /// <summary>
    /// Adds authorization policies with role/scope validation.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    public static void AddCustomAuthorizationPolicies(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var deployment = configuration.GetDeployment();
        services.AddAuthorization(options =>
        {
            // Reusable assertion predicates
            Func<AuthorizationHandlerContext, bool> isValidExternalService = ctx =>
                ctx.User.HasAppRole(SecurityConstants.AppRoles.GatewayAccess) &&
                ctx.User.IsApplicationToken();
            
            Func<AuthorizationHandlerContext, bool> isValidUser = ctx =>
                ctx.User.HasScope(SecurityConstants.Scopes.AccessAsUser) &&
                ctx.User.HasSubjectClaim();

            switch(deployment)
            {
                case DeploymentType.Cloud:
                    options.AddPolicy(AuthorizationPolicies.ExternalService, policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.RequireAssertion(isValidExternalService);
                    });
                    
                    options.AddPolicy(AuthorizationPolicies.User, policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.RequireAssertion(isValidUser);
                    });
                    //TODO: DM Remove this policy when dynamic routing deprecated
                    options.AddPolicy(AuthorizationPolicies.Authenticated, policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.RequireAssertion(ctx => isValidUser(ctx) || isValidExternalService(ctx));
                    });
                    break;
                default:
                    throw new NotSupportedException($"Deployment type '{deployment}' is not supported for authorization policy registration.");
            };

            options.AddPolicy(AuthorizationPolicies.Public, policy =>
            {
                policy.RequireAssertion(_ => true);
            });
            
            var defaultPolicy = options.GetPolicy(AuthorizationPolicies.Authenticated) 
                                 ?? throw new InvalidOperationException($"Authorization policy {AuthorizationPolicies.Authenticated} not configured");
            options.FallbackPolicy = defaultPolicy;
            options.DefaultPolicy = defaultPolicy;
        });
    }
}
