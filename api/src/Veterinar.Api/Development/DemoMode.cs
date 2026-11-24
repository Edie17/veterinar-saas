using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Scheduling;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Clinics;
using Veterinar.Domain.Content;
using Veterinar.Infrastructure.Persistence;

namespace Veterinar.Api.Development;

public static class DemoMode
{
    public static bool Configure(WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("DEMO_MODE")) return false;
        if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("DEMO_MODE is permitted only in Development.");
        builder.Services.AddSingleton<DemoMailbox>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IBookingEmail>(s => s.GetRequiredService<DemoMailbox>()));
        return true;
    }

    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clinic = ClinicDefaults.DefaultClinicId;
        if (!await db.ArticleSections.AnyAsync(x => x.ClinicId == clinic && x.Slug == "ukazkova-poradna"))
            db.ArticleSections.Add(new ArticleSection
            {
                ClinicId = clinic, Title = "Ukázková poradna", Slug = "ukazkova-poradna", Description = "Testovací obsah pro vyzkoušení CMS.",
                Articles = [new Article { ClinicId = clinic, Title = "Jak připravit mazlíčka na návštěvu", Slug = "priprava-na-navstevu", Summary = "Ukázkový článek pro testování webu.", Body = "<h2>Před návštěvou</h2><p>Připravte očkovací průkaz a seznam užívaných léků.</p>" }]
            });
        var day = ClinicTime.Date(DateTimeOffset.UtcNow).AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);
        for (var i = 0; i < 2; i++)
        {
            var email = $"demo.{i + 1}@example.test";
            if (await db.AppointmentRequests.AnyAsync(x => x.ClinicId == clinic && x.Email == email)) continue;
            var start = ClinicTime.At(day, new TimeOnly(10 + i, 0));
            db.AppointmentRequests.Add(new AppointmentRequest
            {
                ClinicId = clinic, Name = i == 0 ? "Ukázková Jana" : "Ukázkový Petr", Email = email, Phone = "+420777000000",
                PetName = i == 0 ? "Bady" : "Míca", PetType = i == 0 ? "Pes" : "Kočka", VisitReason = "Preventivní prohlídka",
                Message = "Syntetická testovací rezervace.", DurationMinutes = 20, Status = AppointmentStatus.Confirmed,
                RequestedStartAt = start, RequestedEndAt = start.AddMinutes(20), StartUtcSeconds = start.ToUnixTimeSeconds(),
                EndUtcSeconds = start.AddMinutes(20).ToUnixTimeSeconds(), CreatedUtcSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }
        await db.SaveChangesAsync();
        app.MapGet("/api/dev/status", (HttpContext http) => DemoMailbox.IsLocal(http) ? Results.Ok(new { demo = true }) : Results.NotFound());
        app.MapGet("/api/dev/mailbox", (HttpContext http, DemoMailbox mailbox) => DemoMailbox.IsLocal(http) ? Results.Ok(mailbox.Read()) : Results.NotFound());
        app.MapGet("/api/dev/mailbox/view", (HttpContext http, DemoMailbox mailbox) => DemoMailbox.IsLocal(http) ? Results.Content(mailbox.Html(), "text/html; charset=utf-8") : Results.NotFound());
    }
}
