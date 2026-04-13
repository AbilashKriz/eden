using System.Net;
using System.Net.Http.Headers;
using Eden.US.Fleet.Toolkit.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Erus.Faas.ApiGateway.IntegrationTests.Authentication;

public class TokenExchangeIntegrationTests
{
	private static void ReplaceTokenManager(IServiceCollection services, ITokenManager mock)
		=> IntegrationTestHelpers.ReplaceTokenManager(services, mock);

	private string CreateTestJwtToken(string jti = "jti", int expiresInMinutes = 60)
		=> IntegrationTestHelpers.CreateTestJwtToken(jti, expiresInMinutes);

	[Fact]
	public async Task Request_WithoutToken_ShouldRejectWithUnauthorized()
	{
		var tokenManager = new Mock<ITokenManager>();
		tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("internal-token");

		await using var apiGateway = IntegrationWebApplicationFactory.CreateApiGatewayService(
			configureServices: services => ReplaceTokenManager(services, tokenManager.Object));

		using var client = await apiGateway.CreateHealthyClientAsync();
		
		// Reset invocations after health check setup
		tokenManager.Invocations.Clear();
		
		var response = await client.GetAsync("/test/api/endpoint");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		// Token exchange should NOT be called for unauthenticated requests
		tokenManager.Verify(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Request_WithValidToken_ShouldExchangeViaBroker()
	{
		var incomingToken = CreateTestJwtToken();
		var tokenManager = new Mock<ITokenManager>();
		tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("internal-token");

		await using var apiGateway = IntegrationWebApplicationFactory.CreateApiGatewayService(
			configureServices: services => ReplaceTokenManager(services, tokenManager.Object));

		using var client = await apiGateway.CreateHealthyClientAsync();
		
		// Reset invocations after health check setup
		tokenManager.Invocations.Clear();
		
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", incomingToken);
		var response = await client.GetAsync("/test/api/endpoint");

		// Token exchange should have been invoked for this request
		tokenManager.Verify(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Request_WithInvalidHeader_ShouldReturnUnauthorized()
	{
		await using var apiGateway = IntegrationWebApplicationFactory.CreateApiGatewayService(services => services.AddDistributedMemoryCache());

		using var client = await apiGateway.CreateHealthyClientAsync();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", string.Empty);

		var response = await client.GetAsync("/test/api/endpoint");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}
}


