using Eden.US.Fleet.Toolkit.Authentication;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Erus.Faas.ApiGateway.IntegrationTests;

/// <summary>
/// Helper methods for integration tests
/// </summary>
public static class IntegrationTestHelpers
{
    /// <summary>
    /// Creates a test JWT token for B2C user authentication with proper claims.
    /// Token includes scp claim for scope-based authorization.
    /// </summary>
    public static string CreateTestJwtToken(string jti = "test-jti", int expiresInMinutes = 60)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("this-is-a-test-key-that-is-long-enough-for-hmac-sha256"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "test-user-sub-123"),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Azp, "test-client-id"),
            new Claim("oid", "test-oid-123"),
            new Claim("preferred_username", "test@example.com"),
            // B2C tokens use 'scp' claim for delegated permissions (access_as_user)
            new Claim("scp", "access_as_user"),
            // tfp claim for B2C policy validation
            new Claim("tfp", "B2C_1_SignIn_page")
        };

        var token = new JwtSecurityToken(
            issuer: "https://test-b2c.b2clogin.com/test-tenant/v2.0/",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates a test JWT token for M2M/service-to-service authentication.
    /// Token includes roles claim for role-based authorization.
    /// </summary>
    public static string CreateTestM2MToken(string jti = "test-jti", int expiresInMinutes = 60)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("this-is-a-test-key-that-is-long-enough-for-hmac-sha256"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Azp, "test-service-client-id"),
            new Claim("oid", "test-service-oid"),
            // M2M tokens use 'roles' claim for application permissions
            new Claim("roles", "Gateway.Access"),
            // idtyp claim identifies this as an application token
            new Claim("idtyp", "app")
        };

        var token = new JwtSecurityToken(
            issuer: "https://login.microsoftonline.com/test-tenant/v2.0",
            audience: "api://test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void ReplaceTokenManager(IServiceCollection services, ITokenManager tokenManager)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITokenManager));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddScoped<ITokenManager>(_ => tokenManager);
        services.AddDistributedMemoryCache();
    }
}

/// <summary>
/// Simple test token manager that returns a static token for integration tests.
/// </summary>
public sealed class TestTokenManager : ITokenManager
{
    private readonly string _token;

    public TestTokenManager(string token = "test-internal-token")
    {
        _token = token;
    }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_token);
    }
}

