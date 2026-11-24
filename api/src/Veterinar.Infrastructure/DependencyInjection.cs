using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veterinar.Application.Abstractions;
using Veterinar.Infrastructure.Persistence;
using Veterinar.Infrastructure.Persistence.Repositories;
using Veterinar.Infrastructure.Security;

namespace Veterinar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var databaseProvider = config["DATABASE_PROVIDER"]?.ToLowerInvariant() ?? "sqlite";
        var connectionString = config["CONNECTION_STRING"] ?? "Data Source=veterinar.db";

        if (databaseProvider != "sqlite") throw new InvalidOperationException("This single-clinic application supports SQLite only.");
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IBookingStore, BookingStore>();
        services.AddScoped<IBookingEmail, Veterinar.Infrastructure.Notifications.BookingEmail>();

        services.AddScoped<IClinicUserRepository, ClinicUserRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<ISchedulingRepository, SchedulingRepository>();
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ICsrfTokenService, CsrfTokenService>();
        return services;
    }
}
