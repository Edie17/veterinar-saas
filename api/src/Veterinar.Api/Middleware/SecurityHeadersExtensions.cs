namespace Veterinar.Api.Middleware;

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        // Apply at response start so an exception handler clearing the response keeps these headers.
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.TryAdd("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
            if (context.Request.Path.StartsWithSegments("/api")) headers.TryAdd("Cache-Control", "no-store");
            if (context.Request.IsHttps) headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            return Task.CompletedTask;
        });
        await next();
    });
}
