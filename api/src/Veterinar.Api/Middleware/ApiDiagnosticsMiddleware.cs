using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Routing;

namespace Veterinar.Api.Middleware;

/// <summary>Correlates API outcomes without recording caller-supplied payloads or exception messages.</summary>
public sealed class ApiDiagnosticsMiddleware(RequestDelegate next, ILogger<ApiDiagnosticsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        // Use a route template, never the concrete path, query, headers or body.
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        var started = Stopwatch.GetTimestamp();
        Exception? failure = null;
        var aborted = false;
        context.Response.Headers["X-Request-ID"] = traceId;

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            aborted = true;
            if (!context.Response.HasStarted) context.Response.StatusCode = 499;
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context, exception.StatusCode, "Požadavek nelze zpracovat.", traceId);
        }
        catch (Veterinar.Application.Common.RequestException exception) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context, exception.StatusCode, exception.Message, traceId);
        }
        catch (Exception exception)
        {
            failure = exception;
            // A streaming response cannot safely be replaced once its headers have been sent.
            if (context.Response.HasStarted) throw;
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Při zpracování požadavku došlo k chybě. Zkuste to prosím znovu.", traceId);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var clinicId = context.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(context.User.FindFirstValue("clinic_id"), out var parsedClinicId)
                    ? parsedClinicId.ToString() : null;
            var status = context.Response.StatusCode;

            if (failure is not null)
            {
                // Exception.Message can contain submitted values or database credentials.
                logger.LogError("API {Method} {Route} failed: status {StatusCode}, elapsed {ElapsedMs} ms, trace {TraceId}, clinic {ClinicId}, exception {ExceptionType}, stack {StackTrace}",
                    context.Request.Method, route, status, elapsedMs, traceId, clinicId,
                    failure.GetType().FullName, failure.StackTrace);
            }
            else if (aborted)
            {
                logger.LogDebug("API {Method} {Route} cancelled by client, elapsed {ElapsedMs} ms, trace {TraceId}",
                    context.Request.Method, route, elapsedMs, traceId);
            }
            else
            {
                var level = status >= 500 ? LogLevel.Error
                    : status is 401 or 403 or 429 ? LogLevel.Warning
                    : IsMutation(context.Request.Method) ? LogLevel.Information : LogLevel.Debug;
                logger.Log(level, "API {Method} {Route} completed: status {StatusCode}, elapsed {ElapsedMs} ms, trace {TraceId}, clinic {ClinicId}",
                    context.Request.Method, route, status, elapsedMs, traceId, clinicId);
            }
        }
    }

    private static bool IsMutation(string method) => HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string traceId)
    {
        context.Response.Clear();
        context.Response.Headers["X-Request-ID"] = traceId;
        await Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["traceId"] = traceId }).ExecuteAsync(context);
    }
}
