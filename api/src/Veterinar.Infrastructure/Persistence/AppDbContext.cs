using Microsoft.EntityFrameworkCore;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Content;
using Veterinar.Domain.Scheduling;
using Veterinar.Domain.Clinics;
using Veterinar.Domain.Users;

namespace Veterinar.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<ClinicUser> ClinicUsers => Set<ClinicUser>();
    public DbSet<PageContent> PageContents => Set<PageContent>();
    public DbSet<ServiceItem> Services => Set<ServiceItem>();
    public DbSet<ArticleSection> ArticleSections => Set<ArticleSection>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();
    public DbSet<AppointmentRequest> AppointmentRequests => Set<AppointmentRequest>();
    public DbSet<WorkingHoursRule> WorkingHours => Set<WorkingHoursRule>();
    public DbSet<WorkingHoursEvent> WorkingHoursEvents => Set<WorkingHoursEvent>();
    public DbSet<WorkingHoursEventRule> WorkingHoursEventRules => Set<WorkingHoursEventRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
