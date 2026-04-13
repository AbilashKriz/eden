using System.Security.Claims;
using Erus.Faas.ApiGateway.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Authentication.Azure;

/// <summary>
/// Validates that B2C tokens come from an expected user flow policy.
/// This prevents tokens from unexpected policies (e.g., password reset) being used.
/// </summary>
public sealed class B2CPolicyClaimValidator
{
    private readonly ILogger<B2CPolicyClaimValidator> _logger;
    private readonly HashSet<string> _allowedPolicies;
    private readonly string _primaryPolicy;

    /// <summary>
    /// Creates a new B2C policy claim validator.
    /// </summary>
    /// <param name="options">B2C configuration options containing the expected policy.</param>
    /// <param name="logger">Logger for validation events.</param>
    public B2CPolicyClaimValidator(
        IOptions<AzureAdB2COptions> options,
        ILogger<B2CPolicyClaimValidator> logger)
    {
        _logger = logger;
        _primaryPolicy = options.Value.SignUpSignInPolicyId;

        // Build allowed policies set - currently just the primary policy
        // Can be extended via configuration to allow multiple policies
        _allowedPolicies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            _primaryPolicy
        };

        // Also add with common variations
        if (!string.IsNullOrEmpty(_primaryPolicy))
        {
            // Some issuers include the policy as a full path
            _allowedPolicies.Add($"b2c_1_{_primaryPolicy.Replace("B2C_1_", "", StringComparison.OrdinalIgnoreCase)}");
        }
    }

    /// <summary>
    /// Validates the B2C policy claim in the token.
    /// Should be called during OnTokenValidated event.
    /// </summary>
    /// <param name="principal">The authenticated claims principal.</param>
    /// <returns>True if the policy is valid, false otherwise.</returns>
    public bool ValidatePolicy(ClaimsPrincipal principal)
    {
        var policyFromToken = principal.GetPolicyFromClaims();

        if (string.IsNullOrEmpty(policyFromToken))
        {
            _logger.LogWarning(
                "B2C token missing policy claim (tfp/acr). Expected policy: {ExpectedPolicy}",
                _primaryPolicy);

            // Missing claim - be strict in enterprise environments
            return false;
        }

        // Extract just the policy name if it's a full URI
        var policyName = ExtractPolicyName(policyFromToken);

        if (_allowedPolicies.Contains(policyName))
        {
            _logger.LogDebug(
                "B2C policy validated successfully. Policy: {Policy}",
                policyName);
            return true;
        }

        _logger.LogWarning(
            "B2C token has unexpected policy. Expected: {ExpectedPolicies}, Got: {ActualPolicy}",
            string.Join(", ", _allowedPolicies),
            policyName);

        return false;
    }
    
    /// <summary>
    /// Extracts the policy name from a potentially full URI.
    /// B2C can return the policy as either just the name or a full URI path.
    /// </summary>
    private static string ExtractPolicyName(string policyValue)
    {
        // If it contains a slash, extract the last segment
        if (policyValue.Contains('/'))
        {
            var segments = policyValue.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Last();
        }

        return policyValue;
    }
}

/// <summary>
/// Extension methods for configuring B2C policy validation.
/// </summary>
public static class B2CPolicyValidationExtensions
{
    /// <summary>
    /// Adds B2C policy claim validation to JWT Bearer events.
    /// Call this in the OnTokenValidated event to validate the policy claim.
    /// </summary>
    public static JwtBearerEvents AddB2CPolicyValidation(
        this JwtBearerEvents events,
        B2CPolicyClaimValidator validator)
    {
        var originalOnTokenValidated = events.OnTokenValidated;

        events.OnTokenValidated = async context =>
        {
            // Run the original handler first
            await originalOnTokenValidated(context);

            // Skip if already failed
            if (context.Result?.Failure != null)
            {
                return;
            }

            // Validate B2C policy claim
            if (context.Principal != null && !validator.ValidatePolicy(context.Principal))
            {
                context.Fail("Token policy claim validation failed. Token may be from an unexpected B2C user flow.");
            }
        };

        return events;
    }

    /// <summary>
    /// Registers B2C policy claim validator in the service collection.
    /// </summary>
    public static IServiceCollection AddB2CPolicyValidation(this IServiceCollection services)
    {
        services.AddSingleton<B2CPolicyClaimValidator>();
        return services;
    }
}

