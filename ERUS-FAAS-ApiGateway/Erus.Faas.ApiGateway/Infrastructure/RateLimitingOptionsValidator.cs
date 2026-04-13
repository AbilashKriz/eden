using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Infrastructure;

/// <summary>
/// Validates <see cref="RateLimitingOptions"/> on startup.
/// </summary>
public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions? options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail($"{nameof(RateLimitingOptions)} cannot be null.");
        }

        var failures = new List<string>();

        if (options.PermitLimit < 1)
        {
            failures.Add($"{nameof(options.PermitLimit)} must be at least 1 in {RateLimitingOptions.Section} configuration.");
        }

        if (options.WindowSeconds < 1)
        {
            failures.Add($"{nameof(options.WindowSeconds)} must be at least 1 in {RateLimitingOptions.Section} configuration.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
