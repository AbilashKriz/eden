namespace Erus.Faas.ApiGateway.Authentication;

/// <summary>
/// Centralized constants for security-related claim names, roles, and scopes.
/// Aligns with Azure AD B2C app registration configuration.
/// </summary>
public static class SecurityConstants
{
    /// <summary>
    /// JWT claim names used in token validation.
    /// </summary>
    public static class Claims
    {

        /// <summary>Application ID (alternative to azp in some token versions).</summary>
        public const string AppId = "appid";

        /// <summary>Token type - "app" for M2M, absent or "user" for delegated.</summary>
        public const string IdentityType = "idtyp";

        /// <summary>Object ID (B2C long form).</summary>
        public const string ObjectIdB2C = "http://schemas.microsoft.com/identity/claims/objectidentifier";

        /// <summary>Name identifier (B2C alternative to sub).</summary>
        public const string NameIdentifier = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
    }

    /// <summary>
    /// Token type values for the idtyp claim.
    /// </summary>
    public static class TokenTypes
    {
        /// <summary>Application/M2M token (client_credentials flow).</summary>
        public const string Application = "app";

    }

    /// <summary>
    /// Application roles defined in Azure AD app registrations.
    /// These must match the appRoles configured in 1_configure-apps.sh.
    /// </summary>
    public static class AppRoles
    {
        /// <summary>Role for external services to access the Gateway (GatewayApp).</summary>
        public const string GatewayAccess = "Gateway.Access";

        /// <summary>Role for internal microservices (InternalServiceApp).</summary>
        public const string ServiceAccess = "Service.Access";
    }

    /// <summary>
    /// OAuth2 scopes defined in Azure AD app registrations.
    /// These must match the oauth2PermissionScopes configured in 1_configure-apps.sh.
    /// </summary>
    public static class Scopes
    {
        /// <summary>Delegated scope for B2C users via GatewayB2cBrokerApp.</summary>
        public const string AccessAsUser = "access_as_user";
    }

    /// <summary>
    /// Default clock skew for token validation.
    /// </summary>
    public static readonly TimeSpan DefaultClockSkew = TimeSpan.FromMinutes(1);
}

