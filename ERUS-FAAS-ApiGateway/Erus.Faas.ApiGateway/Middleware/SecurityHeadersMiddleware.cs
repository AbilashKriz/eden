using Eden.US.Fleet.Toolkit.Configuration;

namespace Erus.Faas.ApiGateway.Middleware;

/// <summary>
/// Middleware that adds security headers to all responses.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly bool _isProduction = configuration.GetEnvironment() == EnvironmentType.Production;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        
        if (context.Request.IsHttps && _isProduction)
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }
        
        await next(context);
    }
}

/// <summary>
/// Extension methods for security headers middleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}

