using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Authentication.Keycloak;

/// <summary>
/// Validates <see cref="JwtValidationOptions"/> on startup.
/// </summary>
public sealed class JwtValidationOptionsValidator : IValidateOptions<JwtValidationOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtValidationOptions? options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail($"{nameof(JwtValidationOptions)} cannot be null.");
        }

        var failures = new List<string>();

        var validIssuers = options.ValidIssuers ?? [];
        var validAudiences = options.ValidAudiences ?? [];

        if (validIssuers.Count == 0)
        {
            failures.Add($"{nameof(options.ValidIssuers)} must contain at least one issuer in {JwtValidationOptions.Section} configuration.");
        }
        else if (validIssuers.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{nameof(options.ValidIssuers)} cannot contain empty or whitespace entries.");
        }

        if (validAudiences.Count == 0)
        {
            failures.Add($"{nameof(options.ValidAudiences)} must contain at least one audience in {JwtValidationOptions.Section} configuration.");
        }
        else if (validAudiences.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{nameof(options.ValidAudiences)} cannot contain empty or whitespace entries.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
