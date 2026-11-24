using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Veterinar.Api.Middleware;

namespace Veterinar.Api.Tests;

public sealed class ApiDiagnosticsMiddlewareTests
{
    [Fact]
    public async Task Unexpected_failure_returns_correlated_problem_without_exception_message()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = CreateContext(services);
        var logger = new RecordingLogger();
        var middleware = new ApiDiagnosticsMiddleware(_ => throw new InvalidOperationException("private-password-value"), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Contains("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("test-request", problem.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("test-request", context.Response.Headers["X-Request-ID"].ToString());
        Assert.DoesNotContain("private-password-value", problem.RootElement.GetRawText());
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(nameof(InvalidOperationException), entry.Text);
        Assert.DoesNotContain("private-password-value", entry.Text);
    }

    [Fact]
    public async Task Mutation_logs_template_status_and_clinic_without_personal_input()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = CreateContext(services);
        context.Request.Method = "PUT";
        context.Request.Path = "/api/admin/articles/private-record";
        context.Request.QueryString = new QueryString("?email=private-contact");
        context.Request.Headers.Cookie = "vet_admin_auth=private-token";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("private-body"));
        var clinicId = Guid.NewGuid();
        var logger = new RecordingLogger();
        var middleware = new ApiDiagnosticsMiddleware(http =>
        {
            // Authentication happens downstream of diagnostics in the actual pipeline.
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("clinic_id", clinicId.ToString())], "test"));
            http.Response.StatusCode = 204;
            return Task.CompletedTask;
        }, logger);

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("/api/admin/articles/{id:int}", entry.Text);
        Assert.Contains("204", entry.Text);
        Assert.Contains(clinicId.ToString(), entry.Text);
        Assert.DoesNotContain("private-", entry.Text);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    public async Task Authentication_and_throttle_rejections_are_warnings(int status)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = CreateContext(services);
        var logger = new RecordingLogger();
        var middleware = new ApiDiagnosticsMiddleware(http =>
        {
            http.Response.StatusCode = status;
            return Task.CompletedTask;
        }, logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Malformed_body_keeps_client_error_status_and_hides_parser_details()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = CreateContext(services);
        var logger = new RecordingLogger();
        var middleware = new ApiDiagnosticsMiddleware(_ => throw new BadHttpRequestException("private-parser-details", 400), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(400, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.DoesNotContain("private-parser-details", await new StreamReader(context.Response.Body).ReadToEndAsync());
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Client_cancellation_does_not_become_a_server_error()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = CreateContext(services);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.RequestAborted = cancellation.Token;
        var logger = new RecordingLogger();
        var middleware = new ApiDiagnosticsMiddleware(_ => throw new OperationCanceledException(), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Started_response_is_not_replaced_when_downstream_fails()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = CreateContext(services);
        var logger = new RecordingLogger();
        var exception = new IOException("private-stream-details");
        var middleware = new ApiDiagnosticsMiddleware(http =>
        {
            http.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
            return Task.FromException(exception);
        }, logger);

        Assert.Same(exception, await Assert.ThrowsAsync<IOException>(() => middleware.InvokeAsync(context)));
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
        Assert.DoesNotContain("private-stream-details", logger.Entries[0].Text);
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "test-request" };
        context.Request.Path = "/api/admin/articles/7";
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/admin/articles/{id:int}"), 0, EndpointMetadataCollection.Empty, "article"));
        return context;
    }

    private sealed class RecordingLogger : ILogger<ApiDiagnosticsMiddleware>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
