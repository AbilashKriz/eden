using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Authentication.Azure;

/// <summary>
/// Validates <see cref="AzureAdB2COptions"/> on startup.
/// Includes validation for security-related properties.
/// </summary>
public sealed class AzureAdB2COptionsValidator : IValidateOptions<AzureAdB2COptions>
{
    public ValidateOptionsResult Validate(string? name, AzureAdB2COptions? options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail($"{nameof(AzureAdB2COptions)} cannot be null.");
        }

        var failures = new List<string>();

        // === Core Configuration ===

        // Validate Instance
        if (string.IsNullOrWhiteSpace(options.Instance))
        {
            failures.Add($"{nameof(options.Instance)} is required in {AzureAdB2COptions.Section} configuration.");
        }
        else if (!Uri.TryCreate(options.Instance, UriKind.Absolute, out var instanceUri))
        {
            failures.Add($"{nameof(options.Instance)} must be a valid absolute URL.");
        }
        else if (instanceUri.Scheme != "https")
        {
            failures.Add($"{nameof(options.Instance)} must be a valid HTTPS URL.");
        }

        // Validate Domain
        if (string.IsNullOrWhiteSpace(options.Domain))
        {
            failures.Add($"{nameof(options.Domain)} is required in {AzureAdB2COptions.Section} configuration.");
        }
        else if (!options.Domain.Contains('.'))
        {
            failures.Add($"{nameof(options.Domain)} must be a valid domain (e.g., tenant.onmicrosoft.com).");
        }

        // Validate SignUpSignInPolicyId
        if (string.IsNullOrWhiteSpace(options.SignUpSignInPolicyId))
        {
            failures.Add($"{nameof(options.SignUpSignInPolicyId)} is required in {AzureAdB2COptions.Section} configuration.");
        }

        // Validate ClientId
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            failures.Add($"{nameof(options.ClientId)} is required in {AzureAdB2COptions.Section} configuration.");
        }
        else if (!Guid.TryParse(options.ClientId, out _))
        {
            failures.Add($"{nameof(options.ClientId)} must be a valid GUID format.");
        }

        // Validate Scopes (if provided, cannot have empty entries)
        if (options.Scopes.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{nameof(options.Scopes)} cannot contain empty or whitespace entries.");
        }
        

        // Validate ValidAudiences (if provided, cannot have empty entries)
        if (options.ValidAudiences.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{nameof(options.ValidAudiences)} cannot contain empty or whitespace entries.");
        }

        // Return validation result
        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
