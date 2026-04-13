using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Erus.Faas.ApiGateway.Authentication.Azure;

namespace Erus.Faas.ApiGateway.Authentication;

public static class BearerSchemeSelector
{
    public static string SelectScheme(StringValues authorizationHeader)
    {
        var header = authorizationHeader.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
        {
            return AuthenticationSchemes.User;
        }

        const string bearerPrefix = "Bearer ";
        if (!header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationSchemes.User;
        }

        var token = header[bearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticationSchemes.User;
        }

        try
        {
            var jwt = new JsonWebToken(token);

            // B2C tokens reliably contain 'tfp' as Trust Framework Policy or Azure AD User Flow (preferred) or 'acr' (fallback).
            // External AAD service tokens typically do not.
            if (jwt.TryGetPayloadValue<string>("tfp", out _) ||
                (jwt.TryGetPayloadValue<string>("acr", out _) && jwt.Issuer.Contains("b2clogin", StringComparison.OrdinalIgnoreCase)))
            {
                return AuthenticationSchemes.User;
            }

            return AuthenticationSchemes.ExternalService;
        }
        catch
        {
            // If we can't parse it, let the default user scheme handle/deny it (401).
            return AuthenticationSchemes.User;
        }
    }
}


