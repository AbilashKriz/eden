using Correlate;
using Eden.US.Fleet.Toolkit.Configuration;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Erus.Faas.ApiGateway.Transforms;

/// <summary>
/// YARP transform that propagates correlation ID to downstream services.
/// </summary>
public sealed class CorrelationTransform(
    ILogger<CorrelationTransform> logger,
    ICorrelationContextAccessor correlationContextAccessor) : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(TransformRequestAsync);
    }

    public ValueTask TransformRequestAsync(RequestTransformContext transformContext)
    {
        var correlationId = correlationContextAccessor.CorrelationContext?.CorrelationId;

        if (!string.IsNullOrEmpty(correlationId))
        {
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                Constants.Headers.CorrelationIdHeaderName, 
                correlationId);
            
            logger.LogDebug("Propagated correlation ID {CorrelationId} to downstream", correlationId);
        }

        return ValueTask.CompletedTask;
    }
}

