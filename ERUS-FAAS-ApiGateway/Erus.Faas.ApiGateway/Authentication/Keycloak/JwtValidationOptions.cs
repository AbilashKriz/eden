namespace Erus.Faas.ApiGateway.Authentication.Keycloak;

/// <summary>
/// Configuration options for JWT issuer and audience validation.
/// </summary>
public sealed class JwtValidationOptions
{
    public const string Section = "JwtValidation";

    /// <summary>
    /// Valid issuers accepted for incoming JWTs.
    /// </summary>
    public List<string> ValidIssuers { get; set; } = [];

    /// <summary>
    /// Valid audiences accepted for incoming JWTs.
    /// </summary>
    public List<string> ValidAudiences { get; set; } = [];
}
