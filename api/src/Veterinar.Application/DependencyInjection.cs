using Microsoft.Extensions.DependencyInjection;
using Veterinar.Application.Appointments;
using Veterinar.Application.Auth;
using Veterinar.Application.Mapping;
using Veterinar.Application.Scheduling;
using Veterinar.Application.Site;

namespace Veterinar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuthService>();
        services.AddScoped<SiteService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<SchedulingService>();
        services.AddSingleton<DtoMapper>();
        return services;
    }
}
