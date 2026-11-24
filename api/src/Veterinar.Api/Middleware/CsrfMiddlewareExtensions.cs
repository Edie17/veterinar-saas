using Veterinar.Application.Abstractions;

namespace Veterinar.Api.Middleware;

public static class CsrfMiddlewareExtensions
{
    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (IsUnsafeApiRequest(context.Request))
        {
            var csrf = context.RequestServices.GetRequiredService<ICsrfTokenService>();
            var cookieToken = context.Request.Cookies["XSRF-TOKEN"];
            var headerToken = context.Request.Headers["X-CSRF-TOKEN"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(headerToken) || cookieToken != headerToken || !csrf.IsValid(headerToken))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid CSRF token." });
                return;
            }
        }
        await next();
    });

    private static bool IsUnsafeApiRequest(HttpRequest request) => request.Path.StartsWithSegments("/api")
        && (HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method) || HttpMethods.IsPatch(request.Method) || HttpMethods.IsDelete(request.Method));
}
