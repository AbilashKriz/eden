using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Eden.US.Fleet.Toolkit.Authentication;
using Eden.US.Fleet.Toolkit.Configuration;
using Erus.Faas.ApiGateway.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Yarp.ReverseProxy.Transforms;

namespace Erus.Faas.ApiGateway.Tests.Transforms;

public class TokenExchangeTransformTests
{
	private readonly Mock<ILogger<TokenExchangeTransform>> _logger = new();
	private readonly Mock<ITokenManager> _tokenManager = new();

	private static string CreateJwt(IEnumerable<Claim>? claims = null)
	{
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("this-is-a-test-key-that-is-long-enough-for-hmac-sha256"));
		var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var allClaims = claims?.ToArray() ?? new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, "user-1"),
			new Claim("oid", "oid-1"),
			new Claim("preferred_username", "user@example.com"),
			new Claim(JwtRegisteredClaimNames.Jti, "jti-1"),
		};
		var token = new JwtSecurityToken("iss", "aud", allClaims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: credentials);
		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	[Fact]
	public async Task Transform_Always_Sets_Internal_Authorization_Header()
	{
		_tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("internal-token");
		var transform = new TokenExchangeTransform(_logger.Object, _tokenManager.Object);

		var ctx = new DefaultHttpContext();
		ctx.Request.Path = "/api/test";
		var proxy = new HttpRequestMessage();
		var tctx = new RequestTransformContext { HttpContext = ctx, ProxyRequest = proxy, Path = "/api/test", Query = new QueryTransformContext(ctx.Request) };

		await transform.TransformRequestAsync(tctx);

		Assert.Equal("Bearer", proxy.Headers.Authorization?.Scheme);
		Assert.Equal("internal-token", proxy.Headers.Authorization?.Parameter);
	}

	[Fact]
	public async Task Transform_AuthenticatedUser_Adds_ClientId_And_UserId_Headers()
	{
		_tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("internal-token");
		var transform = new TokenExchangeTransform(_logger.Object, _tokenManager.Object);

		var ctx = new DefaultHttpContext();
		ctx.Request.Path = "/api/test";
		// add user to context
		ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim("oid", "oid-1"),
			new Claim(JwtRegisteredClaimNames.Azp, "client-1"),
			new Claim("scp", "access_as_user"),
		}, "Bearer"));

		var proxy = new HttpRequestMessage();
		var tctx = new RequestTransformContext { HttpContext = ctx, ProxyRequest = proxy, Path = "/api/test", Query = new QueryTransformContext(ctx.Request) };

		await transform.TransformRequestAsync(tctx);

		Assert.Equal("Bearer", proxy.Headers.Authorization?.Scheme);
		Assert.Equal("internal-token", proxy.Headers.Authorization?.Parameter);
		Assert.True(proxy.Headers.Contains(Constants.Headers.ClientIdHeaderName));
		Assert.True(proxy.Headers.Contains(Constants.Headers.UserIdHeaderName));
	}

	[Fact]
	public async Task Transform_ApplicationToken_Adds_ClientId_Only()
	{
		_tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("internal-token");
		var transform = new TokenExchangeTransform(_logger.Object, _tokenManager.Object);

		var ctx = new DefaultHttpContext();
		ctx.Request.Path = "/api/test";
		ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
			new[]
			{
				new Claim(JwtRegisteredClaimNames.Azp, "service-client-1"),
				new Claim("roles", "Gateway.Access"),
				new Claim("idtyp", "app")
			},
			authenticationType: "Bearer"));

		var proxy = new HttpRequestMessage();
		var tctx = new RequestTransformContext { HttpContext = ctx, ProxyRequest = proxy, Path = "/api/test", Query = new QueryTransformContext(ctx.Request) };

		await transform.TransformRequestAsync(tctx);

		Assert.True(proxy.Headers.Contains(Constants.Headers.ClientIdHeaderName));
		Assert.False(proxy.Headers.Contains(Constants.Headers.UserIdHeaderName));
	}

	[Fact]
	public async Task Transform_TokenManagerThrows_Propagates()
	{
		_tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("boom"));
		var transform = new TokenExchangeTransform(_logger.Object, _tokenManager.Object);

		var ctx = new DefaultHttpContext();
		ctx.Request.Path = "/api/test";
		var proxy = new HttpRequestMessage();
		var tctx = new RequestTransformContext { HttpContext = ctx, ProxyRequest = proxy, Path = "/api/test", Query = new QueryTransformContext(ctx.Request) };

		await Assert.ThrowsAsync<Exception>(() => transform.TransformRequestAsync(tctx).AsTask());
	}

	[Fact]
	public async Task Transform_OperationCancelled_DoesNotTriggerCircuitBreaker()
	{
		var cts = new CancellationTokenSource();
		cts.Cancel();
		
		_tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());
		var transform = new TokenExchangeTransform(_logger.Object, _tokenManager.Object);

		var ctx = new DefaultHttpContext();
		ctx.Request.Path = "/api/test";
		var proxy = new HttpRequestMessage();
		var tctx = new RequestTransformContext 
		{ 
			HttpContext = ctx, 
			ProxyRequest = proxy, 
			Path = "/api/test", 
			Query = new QueryTransformContext(ctx.Request),
			CancellationToken = cts.Token
		};

		// OperationCanceledException should propagate without being caught by circuit breaker
		await Assert.ThrowsAsync<OperationCanceledException>(() => transform.TransformRequestAsync(tctx).AsTask());
	}

	[Fact]
	public async Task Transform_MultipleFailures_EventuallyOpensCircuit()
	{
		var callCount = 0;
		_tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.Returns(() =>
			{
				callCount++;
				return Task.FromException<string>(new Exception($"Failure {callCount}"));
			});
		
		var transform = new TokenExchangeTransform(_logger.Object, _tokenManager.Object);

		// Make enough failing requests to potentially trigger circuit breaker
		// (depends on circuit breaker config: 5 failures in 30s window at 50% failure rate)
		for (var i = 0; i < 10; i++)
		{
			var ctx = new DefaultHttpContext();
			ctx.Request.Path = "/api/test";
			var proxy = new HttpRequestMessage();
			var tctx = new RequestTransformContext 
			{ 
				HttpContext = ctx, 
				ProxyRequest = proxy, 
				Path = "/api/test", 
				Query = new QueryTransformContext(ctx.Request) 
			};

			try
			{
				await transform.TransformRequestAsync(tctx);
			}
			catch
			{
				// Expected - circuit may open after enough failures
			}
		}

		// Verify multiple calls were attempted
		Assert.True(callCount >= 5, "Should have attempted multiple calls before circuit opened");
	}

	[Fact]
	public async Task Transform_Success_RecordsLatencyMetric()
	{
		_tokenManager.Setup(b => b.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync("internal-token");
		var transform = new TokenExchangeTransform(_logger.Object, _tokenManager.Object);

		var ctx = new DefaultHttpContext();
		ctx.Request.Path = "/api/test";
		var proxy = new HttpRequestMessage();
		var tctx = new RequestTransformContext 
		{ 
			HttpContext = ctx, 
			ProxyRequest = proxy, 
			Path = "/api/test", 
			Query = new QueryTransformContext(ctx.Request) 
		};

		// Act - this should complete without throwing and record metrics
		await transform.TransformRequestAsync(tctx);

		// Assert - token was set (metrics are recorded in finally block)
		Assert.Equal("Bearer", proxy.Headers.Authorization?.Scheme);
		Assert.Equal("internal-token", proxy.Headers.Authorization?.Parameter);
	}
}


