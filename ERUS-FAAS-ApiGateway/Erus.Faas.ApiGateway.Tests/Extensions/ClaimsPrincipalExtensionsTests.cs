using System.Security.Claims;
using Erus.Faas.ApiGateway.Authentication;
using Erus.Faas.ApiGateway.Extensions;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Erus.Faas.ApiGateway.Tests.Extensions;

public class ClaimsPrincipalExtensionsTests
{
    #region HasAppRole Tests

    [Fact]
    public void HasAppRole_WithRolesClaim_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Roles, "Gateway.Access"));

        // Act & Assert
        Assert.True(principal.HasAppRole("Gateway.Access"));
    }

    [Fact]
    public void HasAppRole_WithRoleClaimType_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimTypes.Role, "Gateway.Access"));

        // Act & Assert
        Assert.True(principal.HasAppRole("Gateway.Access"));
    }

    [Fact]
    public void HasAppRole_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Roles, "gateway.access"));

        // Act & Assert
        Assert.True(principal.HasAppRole("GATEWAY.ACCESS"));
    }

    [Fact]
    public void HasAppRole_NoMatchingRole_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Roles, "Other.Role"));

        // Act & Assert
        Assert.False(principal.HasAppRole("Gateway.Access"));
    }

    [Fact]
    public void HasAppRole_NoRoles_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipal();

        // Act & Assert
        Assert.False(principal.HasAppRole("Gateway.Access"));
    }

    #endregion

    #region HasScope Tests

    [Fact]
    public void HasScope_WithScpClaim_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Scp, "access_as_user"));

        // Act & Assert
        Assert.True(principal.HasScope("access_as_user"));
    }

    [Fact]
    public void HasScope_WithMultipleScopes_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Scp, "openid profile access_as_user"));

        // Act & Assert
        Assert.True(principal.HasScope("access_as_user"));
        Assert.True(principal.HasScope("openid"));
        Assert.True(principal.HasScope("profile"));
    }

    [Fact]
    public void HasScope_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Scp, "Access_As_User"));

        // Act & Assert
        Assert.True(principal.HasScope("access_as_user"));
    }

    [Fact]
    public void HasScope_NoMatchingScope_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Scp, "openid profile"));

        // Act & Assert
        Assert.False(principal.HasScope("access_as_user"));
    }

    [Fact]
    public void HasScope_NoScopes_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipal();

        // Act & Assert
        Assert.False(principal.HasScope("access_as_user"));
    }

    #endregion

    #region IsApplicationToken Tests

    [Fact]
    public void IsApplicationToken_WithIdtypApp_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(SecurityConstants.Claims.IdentityType, "app"));

        // Act & Assert
        Assert.True(principal.IsApplicationToken());
    }

    [Fact]
    public void IsApplicationToken_WithRolesNoScopes_ReturnsTrue()
    {
        // Arrange (M2M tokens have roles but no scp)
        var principal = CreatePrincipal(new Claim(ClaimConstants.Roles, "Gateway.Access"));

        // Act & Assert
        Assert.True(principal.IsApplicationToken());
    }

    [Fact]
    public void IsApplicationToken_WithScopesNoRoles_ReturnsFalse()
    {
        // Arrange (User tokens have scopes)
        var principal = CreatePrincipal(new Claim(ClaimConstants.Scp, "access_as_user"));

        // Act & Assert
        Assert.False(principal.IsApplicationToken());
    }

    [Fact]
    public void IsApplicationToken_WithIdtypUser_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(SecurityConstants.Claims.IdentityType, "user"));

        // Act & Assert
        Assert.False(principal.IsApplicationToken());
    }

    #endregion

    #region HasSubjectClaim Tests

    [Fact]
    public void HasSubjectClaim_WithSubClaim_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(JwtRegisteredClaimNames.Sub, "user-123"));

        // Act & Assert
        Assert.True(principal.HasSubjectClaim());
    }

    [Fact]
    public void HasSubjectClaim_WithNameIdentifier_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(
            new Claim(SecurityConstants.Claims.NameIdentifier, "user-123"));

        // Act & Assert
        Assert.True(principal.HasSubjectClaim());
    }

    [Fact]
    public void HasSubjectClaim_WithObjectIdB2C_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(
            new Claim(SecurityConstants.Claims.ObjectIdB2C, "user-123"));

        // Act & Assert
        Assert.True(principal.HasSubjectClaim());
    }

    [Fact]
    public void HasSubjectClaim_NoSubjectClaims_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipal();

        // Act & Assert
        Assert.False(principal.HasSubjectClaim());
    }

    #endregion

    #region GetClientId Tests

    [Fact]
    public void GetClientId_WithAppId_ReturnsValue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(SecurityConstants.Claims.AppId, "client-123"));

        // Act
        var clientId = principal.GetClientId();

        // Assert
        Assert.Equal("client-123", clientId);
    }

    [Fact]
    public void GetClientId_WithAzp_ReturnsValue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(JwtRegisteredClaimNames.Azp, "client-456"));

        // Act
        var clientId = principal.GetClientId();

        // Assert
        Assert.Equal("client-456", clientId);
    }

    [Fact]
    public void GetClientId_NoClaims_ReturnsNull()
    {
        // Arrange
        var principal = CreatePrincipal();

        // Act
        var clientId = principal.GetClientId();

        // Assert
        Assert.Null(clientId);
    }

    #endregion

    #region GetObjectId Tests

    [Fact]
    public void GetObjectId_WithOid_ReturnsValue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Oid, "oid-123"));

        // Act
        var oid = Erus.Faas.ApiGateway.Extensions.ClaimsPrincipalExtensions.GetObjectId(principal);

        // Assert
        Assert.Equal("oid-123", oid);
    }

    [Fact]
    public void GetObjectId_NoClaims_ReturnsNull()
    {
        // Arrange
        var principal = CreatePrincipal();

        // Act
        var oid = Erus.Faas.ApiGateway.Extensions.ClaimsPrincipalExtensions.GetObjectId(principal);

        // Assert
        Assert.Null(oid);
    }

    #endregion

    #region GetSubject Tests

    [Fact]
    public void GetSubject_WithSub_ReturnsValue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(JwtRegisteredClaimNames.Sub, "sub-123"));

        // Act
        var sub = principal.GetSubject();

        // Assert
        Assert.Equal("sub-123", sub);
    }

    [Fact]
    public void GetSubject_FallbackToNameIdentifier_ReturnsValue()
    {
        // Arrange
        var principal = CreatePrincipal(
            new Claim(SecurityConstants.Claims.NameIdentifier, "name-id-123"));

        // Act
        var sub = principal.GetSubject();

        // Assert
        Assert.Equal("name-id-123", sub);
    }

    #endregion

    #region GetPolicyFromClaims Tests

    [Fact]
    public void GetPolicyFromClaims_WithTfp_ReturnsValue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimConstants.Tfp, "B2C_1_SignIn"));

        // Act
        var policy = principal.GetPolicyFromClaims();

        // Assert
        Assert.Equal("B2C_1_SignIn", policy);
    }

    [Fact]
    public void GetPolicyFromClaims_WithAcr_ReturnsValue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(JwtRegisteredClaimNames.Acr, "B2C_1_SignUp"));

        // Act
        var policy = principal.GetPolicyFromClaims();

        // Assert
        Assert.Equal("B2C_1_SignUp", policy);
    }

    [Fact]
    public void GetPolicyFromClaims_TfpPreferredOverAcr()
    {
        // Arrange
        var principal = CreatePrincipal(
            new Claim(ClaimConstants.Tfp, "B2C_1_SignIn"),
            new Claim(JwtRegisteredClaimNames.Acr, "B2C_1_SignUp"));

        // Act
        var policy = principal.GetPolicyFromClaims();

        // Assert
        Assert.Equal("B2C_1_SignIn", policy);
    }

    [Fact]
    public void GetPolicyFromClaims_NoClaims_ReturnsNull()
    {
        // Arrange
        var principal = CreatePrincipal();

        // Act
        var policy = principal.GetPolicyFromClaims();

        // Assert
        Assert.Null(policy);
    }

    #endregion

    #region IsClientIdIn Tests

    [Fact]
    public void IsClientIdIn_MatchingClientId_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(SecurityConstants.Claims.AppId, "client-123"));

        // Act & Assert
        Assert.True(principal.IsClientIdIn("client-123", "client-456"));
    }

    [Fact]
    public void IsClientIdIn_NoMatch_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(SecurityConstants.Claims.AppId, "client-999"));

        // Act & Assert
        Assert.False(principal.IsClientIdIn("client-123", "client-456"));
    }

    [Fact]
    public void IsClientIdIn_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(SecurityConstants.Claims.AppId, "CLIENT-123"));

        // Act & Assert
        Assert.True(principal.IsClientIdIn("client-123"));
    }

    #endregion

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Bearer");
        return new ClaimsPrincipal(identity);
    }
}

