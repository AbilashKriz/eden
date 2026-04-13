using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Erus.Faas.ApiGateway.Transforms;

public static partial class DynamicServiceName
{
    public static bool TryNormalize(string? raw, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var candidate = raw.Trim().ToLowerInvariant();
        if (candidate.Length is < 1 or > 63)
        {
            return false;
        }

        if (!DnsLabelRegex().IsMatch(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    // Kubernetes service names are DNS labels:
    // - lower-case alphanumeric or '-'
    // - start/end with alphanumeric
    // - max 63 chars
    [GeneratedRegex("^[a-z0-9](?:[-a-z0-9]{0,61}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex DnsLabelRegex();
}


