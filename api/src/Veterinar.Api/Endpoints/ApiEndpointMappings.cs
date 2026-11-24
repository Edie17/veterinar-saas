using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Veterinar.Api.Security;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Appointments;
using Veterinar.Application.Auth;
using Veterinar.Application.Scheduling;
using Veterinar.Application.Site;
using Veterinar.Domain.Clinics;

namespace Veterinar.Api.Endpoints;

/// <summary>Maps the HTTP boundary to application use cases, grouped by feature.</summary>
public static class ApiEndpointMappings
{
    public static WebApplication MapVetEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));
        app.MapGet("/api/health/ready", async (Veterinar.Infrastructure.Persistence.AppDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
        MapAuthentication(app);
        MapPublicSite(app);
        MapAdministration(app);
        MapAppointments(app);
        return app;
    }

    private static void MapAuthentication(WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").AddEndpointFilter<RequestValidationFilter>();
        auth.MapGet("/csrf", (HttpContext http, ICsrfTokenService csrf) =>
        {
            var token = csrf.CreateToken();
            http.Response.Cookies.Append("XSRF-TOKEN", token, new CookieOptions { HttpOnly = false, SameSite = SameSiteMode.Strict, Secure = CookieSecurity.IsSecure(app.Configuration), Path = "/" });
            return Results.Ok(new { csrfToken = token });
        });
        auth.MapPost("/login", [EnableRateLimiting("login")] async ([FromBody] LoginRequest request, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            var admin = await service.ValidateAdminAsync(request, ct);
            if (admin is null) return Results.Unauthorized();
            var claims = new[] { new Claim(ClaimTypes.Email, admin.Email), new Claim(ClaimTypes.Role, admin.Role), new Claim("clinic_id", admin.ClinicId.ToString()), new Claim(ClaimTypes.NameIdentifier, admin.UserId.ToString()), new Claim("auth_version", admin.AuthVersion!) };
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Ok(new { admin.Email, admin.Role, admin.ClinicId });
        });
        auth.MapPost("/logout", async (HttpContext http) => { await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.NoContent(); }).RequireAuthorization("ClinicAdmin");
        auth.MapGet("/me", (ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
            ? Results.Ok(new AuthenticatedAdminDto(user.FindFirstValue(ClaimTypes.Email) ?? "", user.FindFirstValue(ClaimTypes.Role) ?? "", Guid.TryParse(user.FindFirstValue("clinic_id"), out var clinicId) ? clinicId : ClinicDefaults.DefaultClinicId))
            : Results.Unauthorized());
    }

    private static void MapPublicSite(WebApplication app)
    {
        app.MapGet("/api/site", async (SiteService service, CancellationToken ct) => Results.Ok(await service.GetPublicSiteAsync(ct)));
        app.MapGet("/api/public/working-hours", async (SchedulingService service, CancellationToken ct) => Results.Ok(await service.GetPublicScheduleAsync(ct)));
        app.MapGet("/api/appointment-types", async (AppointmentService service, CancellationToken ct) => Results.Ok(await service.GetAppointmentTypesAsync(true, ct)));
        app.MapGet("/api/availability", async ([FromQuery] DateOnly date, [FromQuery] int? durationMinutes, SchedulingService service, CancellationToken ct) => Results.Ok(await service.GetAvailableSlotsAsync(date, durationMinutes, ct))).RequireRateLimiting("availability");
        app.MapGet("/api/availability/next", async ([FromQuery] DateOnly from, [FromQuery] int durationMinutes, SchedulingService service, CancellationToken ct) => Results.Ok(await service.GetNextAvailableSlotAsync(from, durationMinutes, ct))).RequireRateLimiting("availability");
    }

    private static void MapAdministration(WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization("ClinicAdmin").AddEndpointFilter<RequestValidationFilter>();
        admin.MapPut("/site", async ([FromBody] UpdatePageContentRequest request, SiteService service, CancellationToken ct) => Results.Ok(await service.UpdateContentAsync(request, ct)));
        MapArticles(admin);
        MapAppointmentTypes(admin);
        MapWorkingHours(admin);
        admin.MapGet("/appointments", async (AppointmentService service, CancellationToken ct) => Results.Ok(await service.GetLatestAsync(1000, ct)));
        admin.MapPost("/appointments", async (CreateAppointmentRequest request, AppointmentService service, CancellationToken ct) => Results.Ok(new { id = await service.CreateManualAsync(request, ct) }));
        admin.MapPost("/appointments/{id:int}/cancel", async (int id, AppointmentService service, CancellationToken ct) => { await service.CancelAsync(id, ct); return Results.NoContent(); });
    }

    private static void MapArticles(RouteGroupBuilder admin)
    {
        admin.MapGet("/article-sections", async (SiteService service, CancellationToken ct) => Results.Ok(await service.GetAdminArticleSectionsAsync(ct)));
        admin.MapPost("/article-sections", async ([FromBody] UpsertArticleSectionRequest request, SiteService service, CancellationToken ct) => Results.Ok(await service.UpsertArticleSectionAsync(request, ct)));
        admin.MapPut("/article-sections/{id:int}", async (int id, [FromBody] UpsertArticleSectionRequest request, SiteService service, CancellationToken ct) => Results.Ok(await service.UpsertArticleSectionAsync(request with { Id = id }, ct)));
        admin.MapDelete("/article-sections/{id:int}", async (int id, SiteService service, CancellationToken ct) => { await service.DeleteArticleSectionAsync(id, ct); return Results.NoContent(); });
        admin.MapPost("/articles", async ([FromBody] UpsertArticleRequest request, SiteService service, CancellationToken ct) => Results.Ok(await service.UpsertArticleAsync(request, ct)));
        admin.MapPut("/articles/{id:int}", async (int id, [FromBody] UpsertArticleRequest request, SiteService service, CancellationToken ct) => Results.Ok(await service.UpsertArticleAsync(request with { Id = id }, ct)));
        admin.MapDelete("/articles/{id:int}", async (int id, SiteService service, CancellationToken ct) => { await service.DeleteArticleAsync(id, ct); return Results.NoContent(); });
    }

    private static void MapAppointmentTypes(RouteGroupBuilder admin)
    {
        admin.MapGet("/appointment-types", async (AppointmentService service, CancellationToken ct) => Results.Ok(await service.GetAppointmentTypesAsync(false, ct)));
        admin.MapPost("/appointment-types", async ([FromBody] AppointmentTypeInput request, AppointmentService service, CancellationToken ct) => Results.Ok(await service.UpsertAppointmentTypeAsync(request, ct)));
        admin.MapPut("/appointment-types/{id:int}", async (int id, [FromBody] AppointmentTypeInput request, AppointmentService service, CancellationToken ct) => Results.Ok(await service.UpsertAppointmentTypeAsync(request with { Id = id }, ct)));
        admin.MapDelete("/appointment-types/{id:int}", async (int id, AppointmentService service, CancellationToken ct) => { await service.DeleteAppointmentTypeAsync(id, ct); return Results.NoContent(); });
    }

    private static void MapWorkingHours(RouteGroupBuilder admin)
    {
        admin.MapGet("/working-hours", async (SchedulingService service, CancellationToken ct) => Results.Ok(await service.GetWorkingHoursAsync(ct)));
        admin.MapPut("/working-hours", async ([FromBody] UpsertWorkingHoursRequest request, SchedulingService service, CancellationToken ct) => Results.Ok(await service.UpsertWorkingHoursAsync(request, ct)));
        admin.MapGet("/working-hours/events", async (SchedulingService service, CancellationToken ct) => Results.Ok(await service.GetWorkingHoursEventsAsync(ct)));
        admin.MapPost("/working-hours/events", async ([FromBody] WorkingHoursEventInput request, SchedulingService service, CancellationToken ct) => Results.Ok(await service.UpsertWorkingHoursEventAsync(request, ct)));
        admin.MapPut("/working-hours/events/{id:int}", async (int id, [FromBody] WorkingHoursEventInput request, SchedulingService service, CancellationToken ct) => Results.Ok(await service.UpsertWorkingHoursEventAsync(request with { Id = id }, ct)));
        admin.MapDelete("/working-hours/events/{id:int}", async (int id, SchedulingService service, CancellationToken ct) => { await service.DeleteWorkingHoursEventAsync(id, ct); return Results.NoContent(); });
    }

    private static void MapAppointments(WebApplication app)
    {
        app.MapPost("/api/appointments", async (CreateAppointmentRequest request, AppointmentService service, CancellationToken ct) =>
            Results.Ok(new { id = await service.CreateAsync(request, ct), expiresInMinutes = 15 })).AddEndpointFilter<RequestValidationFilter>().RequireRateLimiting("booking");
        app.MapPost("/api/appointments/confirm", async (ConfirmationRequest request, AppointmentService service, CancellationToken ct) =>
        { await service.ConfirmAsync(request.Token, ct); return Results.NoContent(); }).RequireRateLimiting("confirm");
        var management = app.MapGroup("/api/appointments/manage").RequireRateLimiting("confirm");
        management.MapPost("", async (ConfirmationRequest request, AppointmentService service, CancellationToken ct) => Results.Ok(await service.GetManagedAsync(request.Token, ct)));
        management.MapPost("/cancel", async (ConfirmationRequest request, AppointmentService service, CancellationToken ct) =>
        { await service.CancelManagedAsync(request.Token, ct); return Results.NoContent(); });
        management.MapPost("/move", async (MoveRequest request, AppointmentService service, CancellationToken ct) => Results.Ok(await service.MoveManagedAsync(request.Token, request.StartAt, ct)));
    }
    public sealed record ConfirmationRequest(string Token);
    public sealed record MoveRequest(string Token, DateTimeOffset StartAt);
}
