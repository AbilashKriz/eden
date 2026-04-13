using System.ComponentModel.DataAnnotations;

namespace Erus.Faas.ApiGateway.Authentication.Azure;

/// <summary>
/// Configuration options for Azure AD B2C (User-to-Service) authentication.
/// Includes security properties for enterprise-grade token validation.
/// </summary>
public sealed class AzureAdB2COptions
{
    public const string Section = "AzureAdB2C";

    /// <summary>
    /// Gets or sets the Azure AD B2C instance URL (e.g., https://login.microsoftonline.com/).
    /// Must be a valid HTTPS URL.
    /// </summary>
    [Required(ErrorMessage = "Azure AD B2C Instance URL is required")]
    [Url(ErrorMessage = "Azure AD B2C Instance must be a valid URL")]
    public required string Instance { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD B2C tenant domain
    /// </summary>
    [Required(ErrorMessage = "Azure AD B2C Domain is required")]
    public required string Domain { get; set; }

    /// <summary>
    /// Gets or sets the client ID (Application ID) from Azure AD.
    /// Must be a valid GUID.
    /// </summary>
    [Required(ErrorMessage = "Azure AD B2C Client ID is required")]
    public required string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD B2C sign-up/sign-in policy ID
    /// Used to construct the authority and validate the tfp claim.
    /// </summary>
    [Required(ErrorMessage = "Azure AD B2C Policy ID is required")]
    public required string SignUpSignInPolicyId { get; set; }

    /// <summary>
    /// Gets or sets the scopes for the Azure AD B2C application.
    /// </summary>
    public List<string> Scopes { get; set; } = [];
    
    /// <summary>
    /// Gets or sets additional valid audiences for token validation.
    /// The ClientId is always included.
    /// </summary>
    public List<string> ValidAudiences { get; set; } = [];
}
