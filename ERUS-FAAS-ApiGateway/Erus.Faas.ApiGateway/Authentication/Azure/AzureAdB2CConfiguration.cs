using Eden.US.Fleet.Toolkit.Authentication.Azure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;

namespace Erus.Faas.ApiGateway.Authentication.Azure;

/// <summary>
/// Authentication scheme names for multi-scheme JWT authentication.
/// </summary>
public static class AuthenticationSchemes
{
    /// <summary>Selector scheme that routes bearer tokens to the correct underlying JWT bearer handler.</summary>
    public const string Selector = "SelectorScheme";

    /// <summary>Scheme for B2C user tokens (delegated permissions).</summary>
    public const string User = "UserScheme";

    /// <summary>Scheme for external service M2M tokens (application permissions).</summary>
    public const string ExternalService = "ExternalServiceScheme";
}

/// <summary>
/// Configures Azure AD and Azure AD B2C JWT Bearer authentication with enterprise-grade security.
/// </summary>
public static class AzureAdB2CConfiguration
{
    /// <summary>
    /// Adds Azure AD B2C authentication for users and Azure AD authentication for external services.
    /// Implements comprehensive token validation per Microsoft security guidelines.
    /// </summary>
    public static void AddAzureAdB2CAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var b2COptions = configuration.GetSection(AzureAdB2COptions.Section).Get<AzureAdB2COptions>();
        ArgumentNullException.ThrowIfNull(b2COptions, $"Configuration section '{AzureAdB2COptions.Section}' not found.");

        var adOptions = configuration.GetSection(AzureAdOptions.Section).Get<AzureAdOptions>();
        ArgumentNullException.ThrowIfNull(adOptions, $"Configuration section '{AzureAdOptions.Section}' not found.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = AuthenticationSchemes.Selector;
            options.DefaultChallengeScheme = AuthenticationSchemes.Selector;
        })
        .AddPolicyScheme(AuthenticationSchemes.Selector, "Selects the correct JWT bearer scheme based on token content", options =>
        {
            options.ForwardDefaultSelector = context => BearerSchemeSelector.SelectScheme(context.Request.Headers.Authorization);
        })
        .AddJwtBearer(AuthenticationSchemes.User, options =>
        {
            ConfigureB2CUserScheme(options, b2COptions);
        })
        .AddJwtBearer(AuthenticationSchemes.ExternalService, options =>
        {
            ConfigureExternalServiceScheme(options, adOptions);
        });
    }

    /// <summary>
    /// Configures JWT Bearer authentication for B2C user tokens.
    /// B2C tokens use delegated permissions (scp claim) and come from user flows.
    /// </summary>
    private static void ConfigureB2CUserScheme(JwtBearerOptions options, AzureAdB2COptions b2COptions)
    {
        // B2C authority includes the policy name
        var authority = $"{b2COptions.Instance.TrimEnd('/')}/{b2COptions.Domain}/{b2COptions.SignUpSignInPolicyId}/v2.0";
        options.Authority = authority;

        // Build valid audiences list using configuration
        var validAudiences = AzureAdConfiguration.BuildValidAudiences(b2COptions.ClientId, b2COptions.ValidAudiences);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // === Signature Validation (Critical) ===
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,

            // === Issuer Validation ===
            // Use AAD issuer validator which handles B2C issuer format variations
            ValidateIssuer = true,
            IssuerValidator = AadIssuerValidator.GetAadIssuerValidator(authority).Validate,

            ValidateAudience = true,
            ValidAudiences = validAudiences,

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ClockSkew = SecurityConstants.DefaultClockSkew
        };
    }

    /// <summary>
    /// Configures JWT Bearer authentication for external service M2M tokens.
    /// M2M tokens use application permissions (roles claim) from client_credentials flow.
    /// </summary>
    private static void ConfigureExternalServiceScheme(JwtBearerOptions options, AzureAdOptions adOptions)
    {
        var authority = $"{adOptions.Instance.TrimEnd('/')}/{adOptions.TenantId}";
        options.Authority = authority;

        // Build valid audiences list - support both GUID and api:// URI formats
        var validAudiences = AzureAdConfiguration.BuildValidAudiences(adOptions.ClientId, []);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // === Signature Validation (Critical) ===
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,

            // === Issuer Validation ===
            ValidateIssuer = true,
            IssuerValidator = AadIssuerValidator.GetAadIssuerValidator(authority).Validate,

            // === Audience Validation ===
            ValidateAudience = true,
            ValidAudiences = validAudiences,

            // === Lifetime Validation ===
            ValidateLifetime = true,
            RequireExpirationTime = true,

            // === Clock Skew ===
            ClockSkew = SecurityConstants.DefaultClockSkew
        };
    }
}
