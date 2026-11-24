using Veterinar.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Appointments;
using Veterinar.Application.Scheduling;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Content;
using Veterinar.Domain.Scheduling;
using Veterinar.Domain.Clinics;
using Veterinar.Domain.Users;

namespace Veterinar.Application.Tests;

public sealed class AppointmentServiceTests
{
    [Fact]
    public async Task CreateAsync_Rejects_Script_Content_In_Message()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        SeedAppointmentSetup(db, clinicId);
        await db.SaveChangesAsync();
        var service = CreateService(db, clinicId);

        var exception = await Assert.ThrowsAsync<AppointmentReservationException>(() => service.CreateAsync(CreateRequest(message: "<script>alert(1)</script>")));

        Assert.Contains("Zpráva nesmí obsahovat HTML", exception.Message);
        Assert.Empty(db.AppointmentRequests);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Html_Event_Handler_In_Name()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        SeedAppointmentSetup(db, clinicId);
        await db.SaveChangesAsync();
        var service = CreateService(db, clinicId);

        var exception = await Assert.ThrowsAsync<AppointmentReservationException>(() => service.CreateAsync(CreateRequest(name: "<img src=x onerror=alert(1)>")));

        Assert.Contains("Jméno nesmí obsahovat HTML", exception.Message);
        Assert.Empty(db.AppointmentRequests);
    }

    [Fact]
    public async Task CreateAsync_Derives_Duration_From_Published_Appointment_Type()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        SeedAppointmentSetup(db, clinicId, durationMinutes: 10);
        await db.SaveChangesAsync();
        var service = CreateService(db, clinicId);

        var id = await service.CreateAsync(CreateRequest(durationMinutes: 999));

        var appointment = await db.AppointmentRequests.SingleAsync(x => x.Id == id);
        Assert.Equal(10, appointment.DurationMinutes);
        Assert.Equal("Očkování", appointment.VisitReason);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Request_When_End_Does_Not_Match_Configured_Duration()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        SeedAppointmentSetup(db, clinicId, durationMinutes: 10);
        await db.SaveChangesAsync();
        var service = CreateService(db, clinicId);

        var exception = await Assert.ThrowsAsync<AppointmentReservationException>(() => service.CreateAsync(CreateRequest(
            durationMinutes: 999,
            requestedEndAt: new DateTimeOffset(2026, 6, 29, 9, 30, 0, TimeSpan.FromHours(2)))));

        Assert.Equal("Vybraný termín neodpovídá délce návštěvy.", exception.Message);
        Assert.Empty(db.AppointmentRequests);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Unpublished_Appointment_Type()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        SeedAppointmentSetup(db, clinicId, isPublished: false);
        await db.SaveChangesAsync();
        var service = CreateService(db, clinicId);

        var exception = await Assert.ThrowsAsync<AppointmentReservationException>(() => service.CreateAsync(CreateRequest()));

        Assert.Equal("Vyberte prosím platný důvod návštěvy.", exception.Message);
    }

    private static CreateAppointmentRequest CreateRequest(
        string name = "Jan Novák",
        string message = "Vše v pořádku.",
        int durationMinutes = 10,
        DateTimeOffset? requestedEndAt = null) =>
        new(
            name,
            "+420 777 123 456",
            "jan@example.cz",
            "Bady",
            "Pes",
            "Očkování",
            durationMinutes,
            message,
            new DateTimeOffset(2026, 6, 29, 9, 0, 0, TimeSpan.FromHours(2)),
            requestedEndAt ?? new DateTimeOffset(2026, 6, 29, 9, 10, 0, TimeSpan.FromHours(2)));

    private static void SeedAppointmentSetup(FakeAppDbContext db, Guid clinicId, int durationMinutes = 10, bool isPublished = true)
    {
        db.WorkingHours.Add(new WorkingHoursRule
        {
            ClinicId = clinicId,
            DayOfWeek = DayOfWeek.Monday,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(10, 0),
            SlotMinutes = 10,
            IsClosed = false
        });
        db.AppointmentTypes.Add(new AppointmentType
        {
            ClinicId = clinicId,
            Title = "Očkování",
            DurationMinutes = durationMinutes,
            SortOrder = 1,
            IsPublished = isPublished
        });
    }

    private static AppointmentService CreateService(FakeAppDbContext db, Guid clinicId)
    {
        var clinicContext = new FakeClinicContext(clinicId);
        var mapper = CreateMapper();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.FromHours(2)));
        return new AppointmentService(new FakeAppointmentRepository((FakeAppDbContext)db), clinicContext, new SchedulingService(new FakeSchedulingRepository((FakeAppDbContext)db), clinicContext, mapper, timeProvider, new TestBookingStore(db)), mapper, timeProvider, new TestBookingStore(db), new TestBookingEmail());
    }

    private static DtoMapper CreateMapper() => new DtoMapper();

    private sealed record FakeClinicContext(Guid ClinicId) : IClinicContext
    {
        public bool HasClinic => true;
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeAppointmentRepository(FakeAppDbContext db) : IAppointmentRepository
    {
        public Task<AppointmentType?> FindPublishedTypeByTitleAsync(Guid clinicId, string title, CancellationToken cancellationToken = default) => db.AppointmentTypes.SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.IsPublished && x.Title == title, cancellationToken);
        public Task<bool> IsSlotBookedAsync(Guid clinicId, DateTimeOffset startAt, CancellationToken cancellationToken = default) => db.AppointmentRequests.AnyAsync(x => x.ClinicId == clinicId && x.Status != AppointmentStatus.Cancelled && x.RequestedStartAt == startAt, cancellationToken);
        public async Task<IReadOnlyList<AppointmentRequest>> GetLatestAsync(Guid clinicId, int take, CancellationToken cancellationToken = default) => await db.AppointmentRequests.Where(x => x.ClinicId == clinicId).OrderByDescending(x => x.Id).Take(take).ToListAsync(cancellationToken);
        public async Task<IReadOnlyList<AppointmentType>> GetTypesAsync(Guid clinicId, bool publicOnly, CancellationToken cancellationToken = default) => await db.AppointmentTypes.Where(x => x.ClinicId == clinicId && (!publicOnly || x.IsPublished)).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToListAsync(cancellationToken);
        public Task<AppointmentType?> FindTypeAsync(Guid clinicId, int id, CancellationToken cancellationToken = default) => db.AppointmentTypes.SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.Id == id, cancellationToken);
        public async Task<IReadOnlyList<AppointmentRequest>> GetBookedAsync(Guid clinicId, CancellationToken cancellationToken = default) => await db.AppointmentRequests.Where(x => x.ClinicId == clinicId && x.Status != AppointmentStatus.Cancelled && x.RequestedStartAt != null).ToListAsync(cancellationToken);
        public void Add(AppointmentRequest appointment) => db.AppointmentRequests.Add(appointment);
        public void Add(AppointmentType appointmentType) => db.AppointmentTypes.Add(appointmentType);
        public void Remove(AppointmentType appointmentType) => db.AppointmentTypes.Remove(appointmentType);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
    }

    private sealed class FakeSchedulingRepository(FakeAppDbContext db) : ISchedulingRepository
    {
        public async Task<IReadOnlyList<WorkingHoursRule>> GetRulesAsync(Guid clinicId, CancellationToken ct = default) => await db.WorkingHours.Where(x => x.ClinicId == clinicId).ToListAsync(ct);
        public Task<WorkingHoursRule?> FindRuleAsync(Guid clinicId, DayOfWeek day, CancellationToken ct = default) => db.WorkingHours.FirstOrDefaultAsync(x => x.ClinicId == clinicId && x.DayOfWeek == day, ct);
        public async Task<IReadOnlyList<WorkingHoursEvent>> GetEventsAsync(Guid clinicId, CancellationToken ct = default) => await db.WorkingHoursEvents.Include(x => x.Rules).Where(x => x.ClinicId == clinicId).ToListAsync(ct);
        public Task<WorkingHoursEvent?> FindEventAsync(Guid clinicId, int id, CancellationToken ct = default) => db.WorkingHoursEvents.Include(x => x.Rules).SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.Id == id, ct);
        public Task<WorkingHoursEventRule?> FindActiveEventRuleAsync(Guid clinicId, DateOnly date, CancellationToken ct = default) => db.WorkingHoursEvents.Where(x => x.ClinicId == clinicId && x.IsPublished && x.StartsOn <= date && x.EndsOn >= date).SelectMany(x => x.Rules).FirstOrDefaultAsync(x => x.DayOfWeek == date.DayOfWeek, ct);
        public Task RemoveExpiredEventsAsync(Guid clinicId, DateOnly today, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplaceRulesAsync(Guid clinicId, IReadOnlyCollection<WorkingHoursRule> rules, CancellationToken ct = default) => Task.CompletedTask;
        public void Add(WorkingHoursEvent value) => db.WorkingHoursEvents.Add(value); public void Remove(WorkingHoursEvent value) => db.WorkingHoursEvents.Remove(value); public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    }

    private sealed class FakeAppDbContext : DbContext
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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }
    }
}
