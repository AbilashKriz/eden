using Microsoft.Extensions.Options;

namespace Erus.Faas.ApiGateway.Infrastructure;

/// <summary>
/// Validates <see cref="GatewayOptions"/> on startup.
/// </summary>
public sealed class GatewayOptionsValidator : IValidateOptions<GatewayOptions>
{
    public ValidateOptionsResult Validate(string? name, GatewayOptions? options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail($"{nameof(GatewayOptions)} cannot be null.");
        }

        var failures = new List<string>();

        if (options.AppName != null && string.IsNullOrWhiteSpace(options.AppName))
        {
            failures.Add($"{nameof(options.AppName)} cannot be empty when configured.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
