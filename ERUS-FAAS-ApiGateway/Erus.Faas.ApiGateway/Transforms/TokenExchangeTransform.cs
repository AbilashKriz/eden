using System.Net.Http.Headers;
using Eden.US.Fleet.Toolkit.Authentication;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eden.US.Fleet.Toolkit.Configuration;
using Erus.Faas.ApiGateway.Extensions;
using Polly;
using Polly.CircuitBreaker;

namespace Erus.Faas.ApiGateway.Transforms;

/// <summary>
/// YARP transform that replaces the incoming Authorization header with an internal AAD token
/// and forwards user context via headers.
/// </summary>
public sealed class TokenExchangeTransform : ITransformProvider
{
	private static readonly Meter Meter = new("Erus.Faas.ApiGateway.TokenExchange");
	private static readonly Histogram<double> ExchangeLatency = Meter.CreateHistogram<double>("token_exchange_ms");
	private static readonly Counter<int> CircuitBreakerTrips = Meter.CreateCounter<int>("token_exchange_circuit_breaker_trips");

	private readonly ILogger<TokenExchangeTransform> _logger;
	private readonly ITokenManager _tokenManager;
	private readonly ResiliencePipeline _resiliencePipeline;

	public TokenExchangeTransform(
		ILogger<TokenExchangeTransform> logger,
        ITokenManager tokenManager)
	{
		_logger = logger;
		_tokenManager = tokenManager;
		
		// Circuit breaker: 5 failures in 30s window → break for 30s
		_resiliencePipeline = new ResiliencePipelineBuilder()
			.AddCircuitBreaker(new CircuitBreakerStrategyOptions
			{
				FailureRatio = 0.5,
				SamplingDuration = TimeSpan.FromSeconds(30),
				MinimumThroughput = 5,
				BreakDuration = TimeSpan.FromSeconds(30),
				ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not OperationCanceledException),
				OnOpened = args =>
				{
					logger.LogWarning("Token exchange circuit breaker OPENED. Requests will fail fast for {Duration}s", args.BreakDuration.TotalSeconds);
					CircuitBreakerTrips.Add(1);
					return ValueTask.CompletedTask;
				},
				OnClosed = _ =>
				{
					logger.LogInformation("Token exchange circuit breaker CLOSED. Normal operation resumed.");
					return ValueTask.CompletedTask;
				}
			})
			.Build();
	}

	public void ValidateRoute(TransformRouteValidationContext context) { }

	public void ValidateCluster(TransformClusterValidationContext context) { }

	public void Apply(TransformBuilderContext context)
	{
		context.AddRequestTransform(TransformRequestAsync);
	}

	public async ValueTask TransformRequestAsync(RequestTransformContext transformContext)
	{
		var sw = Stopwatch.StartNew();

		try
		{
			var user = transformContext.HttpContext.User;
			var authorizedParty = user.GetClientId();

			if (!string.IsNullOrEmpty(authorizedParty))
			{
				transformContext.ProxyRequest.Headers.TryAddWithoutValidation(Constants.Headers.ClientIdHeaderName, authorizedParty);
			}

			if (user.Identity?.IsAuthenticated == true && !user.IsApplicationToken())
			{
				var userId = user.GetObjectId() ?? user.GetSubject();

				// User context headers
				if (!string.IsNullOrEmpty(userId))
				{
					transformContext.ProxyRequest.Headers.TryAddWithoutValidation(Constants.Headers.UserIdHeaderName, userId);
				}
			}
			
			// Token acquisition with circuit breaker protection
			var token = await _resiliencePipeline.ExecuteAsync(
				async ct => await _tokenManager.GetAccessTokenAsync(ct),
				transformContext.CancellationToken);
			
			transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}
		catch (BrokenCircuitException)
		{
			_logger.LogWarning("Token exchange blocked by circuit breaker - IdP temporarily unavailable");
			throw;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Token exchange transform failed.");
			throw;
		}
		finally
		{
			sw.Stop();
			ExchangeLatency.Record(sw.Elapsed.TotalMilliseconds);
		}
	}
}


