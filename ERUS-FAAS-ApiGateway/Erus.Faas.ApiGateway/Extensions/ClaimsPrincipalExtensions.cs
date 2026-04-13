using System.Security.Claims;
using Erus.Faas.ApiGateway.Authentication;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Erus.Faas.ApiGateway.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal validation.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Checks if the principal has the specified application role (roles claim).
    /// Used for M2M tokens from client_credentials flow.
    /// </summary>
    public static bool HasAppRole(this ClaimsPrincipal principal, string role)
    {
        // Check the 'roles' claim (used in Azure AD v2 tokens)
        var roles = principal.FindAll(ClaimConstants.Roles)
            .Select(c => c.Value)
            .ToList();
        
        // Also check the standard role claim type
        if (!roles.Any())
        {
            roles = principal.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }
        
        return roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the principal has the specified delegated scope (scp claim).
    /// Used for user tokens from authorization code/PKCE flow.
    /// </summary>
    public static bool HasScope(this ClaimsPrincipal principal, string scope)
    {
        // Check both short form (scp) and B2C long form claim types
        var scopeClaim = principal.FindFirstValue(ClaimConstants.Scp)
            ?? principal.FindFirstValue(ClaimConstants.Scope);
        
        if (string.IsNullOrEmpty(scopeClaim))
        {
            return false;
        }
        
        var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the token is an application/M2M token (client_credentials flow).
    /// The 'idtyp' claim is "app" for application tokens.
    /// </summary>
    public static bool IsApplicationToken(this ClaimsPrincipal principal)
    {
        var idtyp = principal.FindFirst(SecurityConstants.Claims.IdentityType)?.Value;
        
        // If idtyp is present, it should be "app" for M2M tokens
        // If not present (some token versions), check for absence of 'sub' with 'oid' present
        if (!string.IsNullOrEmpty(idtyp))
        {
            return string.Equals(idtyp, SecurityConstants.TokenTypes.Application, StringComparison.OrdinalIgnoreCase);
        }
        
        // Fallback: M2M tokens have roles but no scp claim
        var hasRoles = principal.HasClaim(c => 
            string.Equals(c.Type, ClaimConstants.Role, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Type, ClaimConstants.Roles, StringComparison.OrdinalIgnoreCase));
        var hasScope = principal.HasClaim(c => 
            string.Equals(c.Type, ClaimConstants.Scp, StringComparison.OrdinalIgnoreCase) || 
            string.Equals(c.Type, ClaimConstants.Scope, StringComparison.OrdinalIgnoreCase));
        
        return hasRoles && !hasScope;
    }

    /// <summary>
    /// Checks if the principal has a valid subject claim.
    /// Required for user tokens to identify the authenticated user.
    /// </summary>
    public static bool HasSubjectClaim(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(SecurityConstants.Claims.NameIdentifier)
                  ?? principal.FindFirstValue(SecurityConstants.Claims.ObjectIdB2C);
        return !string.IsNullOrEmpty(sub);
    }

    /// <summary>
    /// Checks if the principal's client ID (azp/appid) is in the allowed list.
    /// Use this for additional client-level restrictions.
    /// </summary>
    public static bool IsClientIdIn(this ClaimsPrincipal principal, params string[] clientIds)
    {
        var clientId = principal.FindFirstValue(SecurityConstants.Claims.AppId)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Azp);
        
        return !string.IsNullOrEmpty(clientId) && clientIds.Contains(clientId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the client ID (azp/appid) from the principal.
    /// </summary>
    public static string? GetClientId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(SecurityConstants.Claims.AppId)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Azp);
    }

    /// <summary>
    /// Gets the object ID (oid) from the principal.
    /// This is the unique identifier for the principal in Azure AD.
    /// </summary>
    public static string? GetObjectId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimConstants.ObjectId)
            ?? principal.FindFirstValue(ClaimConstants.Oid);
    }

    /// <summary>
    /// Gets the subject claim (sub) from the principal.
    /// For users, this is unique per app+user combination.
    /// </summary>
    public static string? GetSubject(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(SecurityConstants.Claims.NameIdentifier)
            ?? principal.FindFirstValue(SecurityConstants.Claims.ObjectIdB2C);
    }
    
    /// <summary>
    /// Gets the B2C policy from the tfp or acr claim.
    /// </summary>
    public static string? GetPolicyFromClaims(this ClaimsPrincipal principal)
    {
        // Try tfp first (Trust Framework Policy - B2C specific)
        var tfp = principal.FindFirstValue(ClaimConstants.Tfp);

        if (!string.IsNullOrEmpty(tfp))
        {
            return tfp;
        }

        // Fall back to acr (Authentication Context Class Reference)
        return principal.FindFirstValue(JwtRegisteredClaimNames.Acr);
    }
}

