using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Veterinar.Api.Security;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Scheduling;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Clinics;
using Veterinar.Domain.Users;
using Veterinar.Infrastructure.Persistence;

namespace Veterinar.Api.Tests;

public sealed class ApiSecurityTests
{
    private sealed class EmailFake : IBookingEmail
    {
        public string? Token { get; private set; }
        public string? ManagementToken { get; private set; }
        public bool Fail { get; set; }
        public Task SendConfirmationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct, string? managementToken = null)
        { if (Fail) throw new Veterinar.Application.Common.RequestException("Mail unavailable", 503); Token = token; ManagementToken = managementToken; return Task.CompletedTask; }
    }
    private sealed class Factory(bool demo = false) : WebApplicationFactory<Program>
    {
        public sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
        public readonly Clock Time = new();
        public readonly EmailFake Email = new();
        public readonly string Password = Guid.NewGuid().ToString("N");
        private readonly string path = Path.Combine(Path.GetTempPath(), "vet-api-test-" + Guid.NewGuid() + ".db");
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("CONNECTION_STRING", $"Data Source={path};Pooling=False");
            builder.UseSetting("ADMIN_PASSWORD", Password);
            builder.UseSetting("ADMIN_EMAIL", "test-admin@example.cz");
            builder.UseSetting("COOKIE_SECURE", "false");
            builder.UseSetting("DEMO_MODE", demo ? "true" : "false");
            builder.ConfigureServices(services => { if (!demo) { services.RemoveAll<IBookingEmail>(); services.AddSingleton<IBookingEmail>(Email); } services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Time); });
        }
        public override async ValueTask DisposeAsync() { await base.DisposeAsync(); File.Delete(path); }
        public async Task<HttpClient> Client(bool login = false)
        {
            var client = CreateClient();
            var csrf = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/auth/csrf");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("csrfToken").GetString());
            if (login) Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "test-admin@example.cz", password = Password })).StatusCode);
            return client;
        }
    }
    private static object Request()
    {
        var date = ClinicTime.Date(DateTimeOffset.UtcNow).AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        var start = ClinicTime.At(date, new TimeOnly(8, 0));
        return new { name = "Test User", phone = "+420 777 123 456", email = "test@example.cz", petName = "Bady", petType = "Pes", visitReason = "Očkování", durationMinutes = 999, message = "Test", requestedStartAt = start, requestedEndAt = start.AddMinutes(10) };
    }
    [Fact]
    public async Task Management_Link_Requires_Confirmation_And_Can_Move_Then_Cancel()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        var created = await client.PostAsJsonAsync("/api/appointments", Request());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var token = factory.Email.ManagementToken!;
        Assert.NotEqual(factory.Email.Token, token);
        Assert.DoesNotContain(token, await created.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/appointments/manage", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/appointments/confirm", new { token = factory.Email.Token })).StatusCode);
        var details = await client.PostAsJsonAsync("/api/appointments/manage", new { token });
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var text = await details.Content.ReadAsStringAsync();
        Assert.DoesNotContain("test@example.cz", text); Assert.DoesNotContain("777", text); Assert.DoesNotContain("Test User", text);
        var data = await details.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var start = data.GetProperty("startAt").GetDateTimeOffset().AddMinutes(30);
        var moved = await client.PostAsJsonAsync("/api/appointments/manage/move", new { token, startAt = start });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(start, (await moved.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("startAt").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/appointments/manage/cancel", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/appointments/manage", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/appointments/manage/cancel", new { token })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var appointment = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppointmentRequests.SingleAsync();
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status); Assert.Null(appointment.ManagementTokenHash);
    }
    [Fact]
    public async Task Management_Rejects_Confirmation_Token_Unknown_Token_And_Missing_Csrf()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        await client.PostAsJsonAsync("/api/appointments", Request());
        foreach (var token in new[] { "", "malformed", new string('A', 64), factory.Email.Token! })
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/appointments/manage", new { token })).StatusCode);
        using var bare = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await bare.PostAsJsonAsync("/api/appointments/manage/cancel", new { token = factory.Email.ManagementToken })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppointmentRequests.SingleAsync();
        Assert.NotEqual(factory.Email.ManagementToken, saved.ManagementTokenHash); Assert.Equal(64, saved.ManagementTokenHash!.Length);
    }
    [Fact]
    public async Task Failed_Reschedule_Preserves_Original_Booking_And_Expired_Link_Cannot_Mutate()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        await client.PostAsJsonAsync("/api/appointments", Request());
        var token = factory.Email.ManagementToken!;
        await client.PostAsJsonAsync("/api/appointments/confirm", new { token = factory.Email.Token });
        var original = (await (await client.PostAsJsonAsync("/api/appointments/manage", new { token })).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("startAt").GetDateTimeOffset();
        var payload = System.Text.Json.JsonSerializer.SerializeToNode(Request())!;
        payload["requestedStartAt"] = original.AddMinutes(30).ToString("O"); payload["requestedEndAt"] = original.AddMinutes(40).ToString("O");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/appointments", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/appointments/manage/move", new { token, startAt = original.AddMinutes(30) })).StatusCode);
        var unchanged = await client.PostAsJsonAsync("/api/appointments/manage", new { token });
        Assert.Equal(original, (await unchanged.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("startAt").GetDateTimeOffset());
        factory.Time.Now = original.AddMinutes(1);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/appointments/manage/cancel", new { token })).StatusCode);
    }
    [Fact]
    public async Task Concurrent_Reschedules_Cannot_Claim_The_Same_Time()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        var original = System.Text.Json.JsonSerializer.SerializeToElement(Request()).GetProperty("requestedStartAt").GetDateTimeOffset();
        var tokens = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            var payload = System.Text.Json.JsonSerializer.SerializeToNode(Request())!;
            payload["requestedStartAt"] = original.AddMinutes(i * 20).ToString("O"); payload["requestedEndAt"] = original.AddMinutes(i * 20 + 10).ToString("O");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/appointments", payload)).StatusCode);
            tokens.Add(factory.Email.ManagementToken!);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/appointments/confirm", new { token = factory.Email.Token })).StatusCode);
        }
        var outcomes = await Task.WhenAll(tokens.Select(token => client.PostAsJsonAsync("/api/appointments/manage/move", new { token, startAt = original.AddHours(1) })));
        Assert.Single(outcomes.Where(x => x.StatusCode == HttpStatusCode.OK)); Assert.Single(outcomes.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppointmentRequests.ToListAsync();
        Assert.Equal(2, rows.Count(x => x.Status == AppointmentStatus.Confirmed));
        Assert.Single(rows.Where(x => x.RequestedStartAt == original.AddHours(1)));
    }
    [Fact]
    public async Task Nearest_Availability_Skips_Closed_Weekend_And_Rejects_Invalid_Range()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        var saturday = ClinicTime.Date(factory.Time.Now).AddDays(1);
        while (saturday.DayOfWeek != DayOfWeek.Saturday) saturday = saturday.AddDays(1);
        var next = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/availability/next?from={saturday:yyyy-MM-dd}&durationMinutes=20");
        Assert.Equal(saturday.AddDays(2), ClinicTime.Date(next.GetProperty("startAt").GetDateTimeOffset()));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/availability/next?from={saturday:yyyy-MM-dd}&durationMinutes=999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/availability/next?from={saturday.AddDays(70):yyyy-MM-dd}&durationMinutes=20")).StatusCode);
    }
    [Fact]
    public async Task Demo_Mode_Seeds_Data_And_Captures_Mail_Without_SMTP()
    {
        using var factory = new Factory(true); using var client = await factory.Client();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(2, await db.AppointmentRequests.CountAsync());
            Assert.Equal(1, await db.Articles.CountAsync());
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/appointments", Request())).StatusCode);
        var mailbox = factory.Services.GetRequiredService<Veterinar.Api.Development.DemoMailbox>();
        Assert.Single(mailbox.Read());
        Assert.Contains("/rezervace/potvrzeni#token=", mailbox.Read()[0].ConfirmationUrl);
        foreach (var address in new[] { "127.0.0.1", "203.0.113.22" })
        {
            var context = await factory.Server.SendAsync(http => { http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(address); http.Request.Path = "/api/dev/mailbox"; http.Request.Method = "GET"; });
            Assert.Equal(address == "127.0.0.1" ? 200 : 404, context.Response.StatusCode);
        }
    }
    [Fact]
    public async Task Normal_Mode_Does_Not_Expose_Demo_Mailbox()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/dev/mailbox")).StatusCode);
    }
    [Fact]
    public void Demo_Mode_Rejects_Production()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration["DEMO_MODE"] = "true";
        Assert.Throws<InvalidOperationException>(() => Veterinar.Api.Development.DemoMode.Configure(builder));
    }
    [Fact]
    public async Task Claimed_Email_Cannot_Poison_Quota_And_Confirmation_Enforces_Normalized_Limit()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        var date = ClinicTime.Date(factory.Time.Now).AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        var tokens = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var start = ClinicTime.At(date, new TimeOnly(8 + i, 0));
            var response = await client.PostAsJsonAsync("/api/appointments", new
            {
                name = "Test User", phone = "+420 777 123 456", email = i < 2 ? "VICTIM@example.cz" : "victim@example.cz",
                petName = "Bady", petType = "Pes", visitReason = "Očkování", durationMinutes = 10,
                requestedStartAt = start, requestedEndAt = start.AddMinutes(10)
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            tokens.Add(factory.Email.Token!);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/appointments/confirm", new { token = tokens[2] })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/appointments/confirm", new { token = tokens[0] })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/appointments/confirm", new { token = tokens[1] })).StatusCode);
    }
    [Fact]
    public async Task Cms_Optional_Description_And_Summary_Remain_Optional()
    {
        using var factory = new Factory(); using var client = await factory.Client(true);
        var section = await client.PostAsJsonAsync("/api/admin/article-sections", new { title = "Poradna", description = "", sortOrder = 1, isPublished = true });
        Assert.Equal(HttpStatusCode.OK, section.StatusCode);
        var data = await section.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var article = await client.PostAsJsonAsync("/api/admin/articles", new { articleSectionId = data.GetProperty("id").GetInt32(), title = "Test", summary = "", body = "<p>Text článku.</p>", sortOrder = 1, isPublished = true });
        Assert.Equal(HttpStatusCode.OK, article.StatusCode);
    }
    [Fact]
    public async Task Public_Booking_Needs_Confirmation_And_Token_Is_Not_In_Response()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        var response = await client.PostAsJsonAsync("/api/appointments", Request()); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(); Assert.NotNull(factory.Email.Token); Assert.DoesNotContain(factory.Email.Token!, body);
        using (var scope = factory.Services.CreateScope()) Assert.Equal(AppointmentStatus.PendingConfirmation, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppointmentRequests.Select(x => x.Status).SingleAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/appointments/confirm", new { token = factory.Email.Token })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/appointments/confirm", new { token = factory.Email.Token })).StatusCode);
    }
    [Fact]
    public async Task Expired_Hold_Is_Not_Presented_As_A_Booked_Patient_Without_A_Write()
    {
        using var factory = new Factory(); using var client = await factory.Client(); using var admin = await factory.Client(true);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/appointments", Request())).StatusCode);
        var pending = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/admin/appointments");
        Assert.Equal("PendingConfirmation", pending[0].GetProperty("status").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, pending[0].GetProperty("holdExpiresAt").ValueKind);
        factory.Time.Now = factory.Time.Now.AddMinutes(16);
        var expired = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/admin/appointments");
        Assert.Equal("Cancelled", expired[0].GetProperty("status").GetString());
        using var scope = factory.Services.CreateScope(); Assert.Equal(AppointmentStatus.PendingConfirmation, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppointmentRequests.Select(x => x.Status).SingleAsync());
    }
    [Fact]
    public async Task Delivery_Failure_Releases_Hold_And_Returns_503()
    {
        using var factory = new Factory(); factory.Email.Fail = true; using var client = await factory.Client();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/appointments", Request())).StatusCode);
        using var scope = factory.Services.CreateScope(); Assert.Equal(AppointmentStatus.Cancelled, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppointmentRequests.Select(x => x.Status).SingleAsync());
    }
    [Fact]
    public async Task Staff_Booking_Requires_Auth_And_Bypasses_Email_Confirmation()
    {
        using var factory = new Factory(); using var anonymous = await factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/admin/appointments", Request())).StatusCode);
        using var admin = await factory.Client(true); Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/admin/appointments", Request())).StatusCode);
        Assert.Null(factory.Email.Token);
        using var scope = factory.Services.CreateScope(); Assert.Equal(AppointmentStatus.Confirmed, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AppointmentRequests.Select(x => x.Status).SingleAsync());
    }
    [Theory]
    [InlineData("disable")]
    [InlineData("role")]
    [InlineData("password")]
    public async Task Account_Changes_Revoke_Cookie(string change)
    {
        using var factory = new Factory(); using var client = await factory.Client(true);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = await db.ClinicUsers.SingleAsync();
            if (change == "disable") user.IsActive = false;
            if (change == "role") user.Role = ClinicUserRole.Viewer;
            if (change == "password") user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("replacement");
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/appointments")).StatusCode);
    }
    [Fact]
    public async Task Other_Clinic_User_Cannot_Authenticate_And_Csrf_Is_Required()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ClinicUsers.Add(new ClinicUser { ClinicId = Guid.NewGuid(), Email = "other@example.cz", PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(factory.Password), Role = ClinicUserRole.Owner }); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "other@example.cz", password = factory.Password })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/appointments", Request())).StatusCode);
    }
    [Fact]
    public async Task Oversized_Phone_And_Invalid_Duration_Are_Safe_Client_Errors()
    {
        using var factory = new Factory(); using var client = await factory.Client();
        var response = await client.PostAsJsonAsync("/api/appointments", new { name = "Test", phone = new string('<', 200000), email = "t@example.cz", petName = "A", petType = "Pes", visitReason = "Očkování" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var date = ClinicTime.Date(DateTimeOffset.UtcNow).AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/availability?date={date:yyyy-MM-dd}&durationMinutes=999")).StatusCode);
    }
    [Fact]
    public void Login_Quota_Is_Per_Connection_And_Ignores_Spoofed_Header()
    {
        var a = new DefaultHttpContext(); a.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        var b = new DefaultHttpContext(); b.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.2");
        var partitionA = ClientRateLimits.Partition(a, 5, 1); var partitionB = ClientRateLimits.Partition(b, 5, 1);
        using var limiterA = partitionA.Factory(partitionA.PartitionKey); using var limiterB = partitionB.Factory(partitionB.PartitionKey);
        for (var i = 0; i < 5; i++) Assert.True(limiterA.AttemptAcquire().IsAcquired);
        Assert.False(limiterA.AttemptAcquire().IsAcquired); Assert.True(limiterB.AttemptAcquire().IsAcquired);
        a.Request.Headers["X-Forwarded-For"] = "192.0.2.2"; Assert.Equal(partitionA.PartitionKey, ClientRateLimits.ClientKey(a));
    }
    [Theory]
    [InlineData("192.0.2.1", "192.0.2.1")]
    [InlineData("127.0.0.1", "198.51.100.2")]
    public async Task Only_Trusted_Proxy_Can_Forward_Client_IP(string peer, string expected)
    {
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = 1 };
        options.KnownIPNetworks.Clear(); options.KnownProxies.Clear(); options.KnownProxies.Add(IPAddress.Loopback);
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Parse(peer); context.Request.Headers["X-Forwarded-For"] = "198.51.100.2";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context); Assert.Equal(IPAddress.Parse(expected), context.Connection.RemoteIpAddress);
    }
}
