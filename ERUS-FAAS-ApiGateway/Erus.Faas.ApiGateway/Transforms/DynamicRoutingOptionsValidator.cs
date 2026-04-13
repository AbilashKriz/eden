using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Transforms;

/// <summary>
/// Validates <see cref="DynamicRoutingOptions"/> on startup.
/// </summary>
public sealed class DynamicRoutingOptionsValidator : IValidateOptions<DynamicRoutingOptions>
{
    public ValidateOptionsResult Validate(string? name, DynamicRoutingOptions? options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail($"{nameof(DynamicRoutingOptions)} cannot be null.");
        }

        var failures = new List<string>();

        var allowedServices = options.AllowedServices ?? [];
        if (allowedServices.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{nameof(options.AllowedServices)} cannot contain empty or whitespace entries.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
