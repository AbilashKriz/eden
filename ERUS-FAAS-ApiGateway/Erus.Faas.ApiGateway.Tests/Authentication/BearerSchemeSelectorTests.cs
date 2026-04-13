using System.Text;
using Erus.Faas.ApiGateway.Authentication;
using Erus.Faas.ApiGateway.Authentication.Azure;
using Microsoft.Extensions.Primitives;

namespace Erus.Faas.ApiGateway.Tests.Authentication;

public sealed class BearerSchemeSelectorTests
{
    [Fact]
    public void SelectScheme_NoHeader_DefaultsToUserScheme()
    {
        var scheme = BearerSchemeSelector.SelectScheme(StringValues.Empty);
        Assert.Equal(AuthenticationSchemes.User, scheme);
    }

    [Fact]
    public void SelectScheme_NonBearerHeader_DefaultsToUserScheme()
    {
        var scheme = BearerSchemeSelector.SelectScheme(new StringValues("Basic abc"));
        Assert.Equal(AuthenticationSchemes.User, scheme);
    }

    [Fact]
    public void SelectScheme_B2CToken_WithTfp_SelectsUserScheme()
    {
        var jwt = CreateJwt(new
        {
            iss = "https://contoso.b2clogin.com/contoso.onmicrosoft.com/v2.0/",
            tfp = "B2C_1_signup_signin",
            scp = "access_as_user"
        });

        var scheme = BearerSchemeSelector.SelectScheme(new StringValues($"Bearer {jwt}"));
        Assert.Equal(AuthenticationSchemes.User, scheme);
    }

    [Fact]
    public void SelectScheme_ExternalServiceToken_SelectsExternalServiceScheme()
    {
        var jwt = CreateJwt(new
        {
            iss = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0",
            idtyp = "app",
            roles = new[] { "Gateway.Access" }
        });

        var scheme = BearerSchemeSelector.SelectScheme(new StringValues($"Bearer {jwt}"));
        Assert.Equal(AuthenticationSchemes.ExternalService, scheme);
    }

    private static string CreateJwt(object payloadObj)
    {
        var headerJson = """{"alg":"none","typ":"JWT"}""";
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadObj);

        var header = Base64UrlEncode(headerJson);
        var payload = Base64UrlEncode(payloadJson);
        var sig = Base64UrlEncode("sig");

        return $"{header}.{payload}.{sig}";
    }

    private static string Base64UrlEncode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}


