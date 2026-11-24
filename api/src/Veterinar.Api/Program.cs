using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Veterinar.Application;
using Veterinar.Application.Abstractions;
using Veterinar.Api.Configuration;
using Veterinar.Api.Endpoints;
using Veterinar.Api.Middleware;
using Veterinar.Api.Security;
using Veterinar.Api.Tenancy;
using Veterinar.Api.Development;
using Veterinar.Infrastructure;
using Veterinar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args.Where(x => x != "--initialize-database").ToArray());
builder.Configuration.AddEnvironmentVariables();
ProductionConfiguration.Validate(builder.Configuration, builder.Environment);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClinicContext, SingleClinicContext>();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 262144);
builder.Services.AddScoped<ClinicCookieEvents>();
var keys = builder.Configuration["DATA_PROTECTION_KEYS"];
if (!string.IsNullOrWhiteSpace(keys)) builder.Services.AddDataProtection().SetApplicationName("VeterinarClinic").PersistKeysToFileSystem(new DirectoryInfo(keys));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var value in (builder.Configuration["TRUSTED_PROXIES"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)) options.KnownProxies.Add(IPAddress.Parse(value.Trim()));
    // With no configured proxy, do not consume forwarded headers at all.
    if (options.KnownProxies.Count == 0) options.ForwardedHeaders = ForwardedHeaders.None;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var demoMode = DemoMode.Configure(builder);

var corsOrigin = builder.Environment.IsProduction() ? builder.Configuration["PUBLIC_ORIGIN"]! : builder.Configuration["CORS_ORIGIN"] ?? "http://localhost:5173";
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(corsOrigin)
    .AllowCredentials()
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "vet_admin_auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurity.IsSecure(builder.Configuration) ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = false;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.EventsType = typeof(ClinicCookieEvents);
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ClinicAdmin", policy => policy.RequireRole("Owner", "Admin"));
    options.AddPolicy("ClinicStaff", policy => policy.RequireRole("Owner", "Admin", "Vet", "Receptionist"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => ClientRateLimits.Partition(context, 5, 1));
    options.AddPolicy("booking", context => ClientRateLimits.Partition(context, 5, 15));
    options.AddPolicy("confirm", context => ClientRateLimits.Partition(context, 20, 1));
    options.AddPolicy("availability", context => ClientRateLimits.Partition(context, 60, 1));
});

var app = builder.Build();
if (args.Contains("--initialize-database"))
{
    await DatabaseSeeder.SeedAsync(app.Services, app.Configuration);
    return;
}
app.UseForwardedHeaders();
app.UseRouting();
app.UseMiddleware<ApiDiagnosticsMiddleware>();
app.UseSecurityHeaders();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseCsrfProtection();

if (app.Environment.IsDevelopment()) await DatabaseSeeder.SeedAsync(app.Services, app.Configuration);
else
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if ((await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetPendingMigrationsAsync(db.Database)).Any())
        throw new InvalidOperationException("Initialize the database before serving production traffic.");
    if (!await db.Clinics.AnyAsync(x => x.Id == Veterinar.Domain.Clinics.ClinicDefaults.DefaultClinicId)
        || !await db.ClinicUsers.AnyAsync(x => x.ClinicId == Veterinar.Domain.Clinics.ClinicDefaults.DefaultClinicId && x.IsActive && x.Role == Veterinar.Domain.Users.ClinicUserRole.Owner))
        throw new InvalidOperationException("Initialize the clinic and owner account before serving production traffic.");
}

app.MapVetEndpoints();
if (demoMode) await DemoMode.InitializeAsync(app);
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
public partial class Program { }
