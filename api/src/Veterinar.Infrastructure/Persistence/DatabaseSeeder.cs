using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Content;
using Veterinar.Domain.Scheduling;
using Veterinar.Domain.Clinics;
using Veterinar.Domain.Users;

namespace Veterinar.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var defaultClinic = await db.Clinics.SingleOrDefaultAsync(x => x.Id == ClinicDefaults.DefaultClinicId);
        if (defaultClinic is null)
        {
            defaultClinic = new Clinic
            {
                Id = ClinicDefaults.DefaultClinicId,
                Name = "Veterinární ordinace Demo",
                Slug = "veterinarni-ordinace-demo",
                ContactEmail = "ordinace@example.test",
                Phone = "+420 000 000 000",
                Address = "Ukázková adresa",
                TimeZone = "Europe/Prague"
            };
            db.Clinics.Add(defaultClinic);
        }

        if (!await db.PageContents.AnyAsync(x => x.ClinicId == defaultClinic.Id))
        {
            db.PageContents.Add(new PageContent { ClinicId = defaultClinic.Id });
        }

        if (!await db.Services.AnyAsync(x => x.ClinicId == defaultClinic.Id))
        {
            db.Services.AddRange(
                new ServiceItem { ClinicId = defaultClinic.Id, Title = "Preventivní péče", Description = "Prohlídky, očkování, čipování a výživové poradenství.", Icon = "🩺", SortOrder = 1 },
                new ServiceItem { ClinicId = defaultClinic.Id, Title = "Stomatologie", Description = "Kontrola chrupu, odstranění zubního kamene a plán domácí péče.", Icon = "🦷", SortOrder = 2 },
                new ServiceItem { ClinicId = defaultClinic.Id, Title = "Diagnostika", Description = "Laboratorní testy, základní vyšetření a doporučení dalšího postupu.", Icon = "🔬", SortOrder = 3 }
            );
        }

        if (!await db.WorkingHours.AnyAsync(x => x.ClinicId == defaultClinic.Id))
        {
            db.WorkingHours.AddRange(
                CreateDefaultWorkingHours(defaultClinic.Id, DayOfWeek.Monday),
                CreateDefaultWorkingHours(defaultClinic.Id, DayOfWeek.Tuesday),
                CreateDefaultWorkingHours(defaultClinic.Id, DayOfWeek.Wednesday),
                CreateDefaultWorkingHours(defaultClinic.Id, DayOfWeek.Thursday),
                CreateDefaultWorkingHours(defaultClinic.Id, DayOfWeek.Friday),
                CreateClosedWorkingHours(defaultClinic.Id, DayOfWeek.Saturday),
                CreateClosedWorkingHours(defaultClinic.Id, DayOfWeek.Sunday));
        }

        if (!await db.AppointmentTypes.AnyAsync(x => x.ClinicId == defaultClinic.Id))
        {
            db.AppointmentTypes.AddRange(
                new AppointmentType { ClinicId = defaultClinic.Id, Title = "Preventivní prohlídka", DurationMinutes = 20, SortOrder = 1 },
                new AppointmentType { ClinicId = defaultClinic.Id, Title = "Očkování", DurationMinutes = 10, SortOrder = 2 },
                new AppointmentType { ClinicId = defaultClinic.Id, Title = "Akutní problém", DurationMinutes = 30, SortOrder = 3 },
                new AppointmentType { ClinicId = defaultClinic.Id, Title = "Kontrola", DurationMinutes = 15, SortOrder = 4 },
                new AppointmentType { ClinicId = defaultClinic.Id, Title = "Stomatologie", DurationMinutes = 45, SortOrder = 5 });
        }

        var email = (config["ADMIN_EMAIL"] ?? "admin@example.cz").Trim().ToLowerInvariant();
        if (!await db.ClinicUsers.AnyAsync(x => x.Email == email && x.ClinicId == defaultClinic.Id))
        {
            var password = GetSeedAdminPassword(config);
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.ClinicUsers.Add(new ClinicUser
            {
                ClinicId = defaultClinic.Id,
                Email = email,
                PasswordHash = hasher.Hash(password),
                Role = ClinicUserRole.Owner,
                EmailVerifiedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static string GetSeedAdminPassword(IConfiguration config)
    {
        var configured = config["ADMIN_PASSWORD"];
        var environment = config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(configured) || configured.Length < 16 || configured == "ChangeMe123!" || string.IsNullOrWhiteSpace(config["ADMIN_EMAIL"])))
            throw new InvalidOperationException("Set ADMIN_EMAIL and a unique ADMIN_PASSWORD of at least 16 characters for initialization.");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return "ChangeMe123!";
        }

        throw new InvalidOperationException("ADMIN_PASSWORD must be configured outside Development.");
    }

    private static WorkingHoursRule CreateDefaultWorkingHours(Guid clinicId, DayOfWeek dayOfWeek) => new()
    {
        ClinicId = clinicId,
        DayOfWeek = dayOfWeek,
        OpensAt = new TimeOnly(8, 0),
        ClosesAt = new TimeOnly(16, 0),
        SlotMinutes = 30,
        IsClosed = false
    };

    private static WorkingHoursRule CreateClosedWorkingHours(Guid clinicId, DayOfWeek dayOfWeek) => new()
    {
        ClinicId = clinicId,
        DayOfWeek = dayOfWeek,
        OpensAt = TimeOnly.MinValue,
        ClosesAt = TimeOnly.MinValue,
        SlotMinutes = 30,
        IsClosed = true
    };
}
