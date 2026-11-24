using Veterinar.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Scheduling;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Content;
using Veterinar.Domain.Scheduling;
using Veterinar.Domain.Clinics;
using Veterinar.Domain.Users;

namespace Veterinar.Application.Tests;

public sealed class SchedulingServiceTests
{
    [Fact]
    public async Task GetAvailableSlotsAsync_Returns_Working_Hours_Slots_And_Excludes_Already_Booked_Time()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        var clinicContext = new FakeClinicContext(clinicId);
        db.WorkingHours.Add(new WorkingHoursRule
        {
            ClinicId = clinicId,
            DayOfWeek = DayOfWeek.Monday,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(11, 0),
            SlotMinutes = 30,
            IsClosed = false
        });
        db.AppointmentRequests.Add(new AppointmentRequest
        {
            ClinicId = clinicId,
            Name = "Jan Novák",
            Phone = "+420777123456",
            PetName = "Bady",
            PetType = "Pes",
            RequestedStartAt = new DateTimeOffset(2026, 6, 29, 9, 30, 0, TimeSpan.FromHours(2)),
            RequestedEndAt = new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.FromHours(2))
        });
        await db.SaveChangesAsync();

        var slots = await CreateService(db, clinicContext).GetAvailableSlotsAsync(new DateOnly(2026, 6, 29));

        Assert.Equal(
            new[]
            {
                new DateTimeOffset(2026, 6, 29, 9, 0, 0, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 6, 29, 10, 30, 0, TimeSpan.FromHours(2))
            },
            slots.Where(x => x.IsAvailable).Select(x => x.StartAt));
        Assert.False(slots.Single(x => x.StartAt == new DateTimeOffset(2026, 6, 29, 9, 30, 0, TimeSpan.FromHours(2))).IsAvailable);
    }

    [Fact]
    public async Task UpsertWorkingHoursAsync_Replaces_Only_Current_Clinic_Rules()
    {
        var clinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        db.WorkingHours.AddRange(
            new WorkingHoursRule { ClinicId = clinicId, DayOfWeek = DayOfWeek.Monday, OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(10, 0), SlotMinutes = 30 },
            new WorkingHoursRule { ClinicId = otherClinicId, DayOfWeek = DayOfWeek.Monday, OpensAt = new TimeOnly(7, 0), ClosesAt = new TimeOnly(8, 0), SlotMinutes = 30 });
        await db.SaveChangesAsync();

        await CreateService(db, new FakeClinicContext(clinicId)).UpsertWorkingHoursAsync(new UpsertWorkingHoursRequest(new[]
        {
            new WorkingHoursRuleInput(DayOfWeek.Tuesday, "13:00", "15:00", 20, false),
            new WorkingHoursRuleInput(DayOfWeek.Wednesday, "00:00", "00:00", 30, true)
        }));

        var currentClinicRules = await db.WorkingHours.Where(x => x.ClinicId == clinicId).OrderBy(x => x.DayOfWeek).ToListAsync();
        var otherClinicRule = await db.WorkingHours.SingleAsync(x => x.ClinicId == otherClinicId);

        Assert.Equal(2, currentClinicRules.Count);
        Assert.Equal(DayOfWeek.Tuesday, currentClinicRules[0].DayOfWeek);
        Assert.Equal(new TimeOnly(13, 0), currentClinicRules[0].OpensAt);
        Assert.Equal(20, currentClinicRules[0].SlotMinutes);
        Assert.True(currentClinicRules[1].IsClosed);
        Assert.Equal(new TimeOnly(7, 0), otherClinicRule.OpensAt);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_Uses_Selected_Appointment_Duration_As_Step()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        db.WorkingHours.Add(new WorkingHoursRule
        {
            ClinicId = clinicId,
            DayOfWeek = DayOfWeek.Monday,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(9, 30),
            SlotMinutes = 30,
            IsClosed = false
        });
        await db.SaveChangesAsync();

        var slots = await CreateService(db, new FakeClinicContext(clinicId))
            .GetAvailableSlotsAsync(new DateOnly(2026, 6, 29), durationMinutes: 10);

        Assert.Equal(
            new[]
            {
                new DateTimeOffset(2026, 6, 29, 9, 0, 0, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 6, 29, 9, 10, 0, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 6, 29, 9, 20, 0, TimeSpan.FromHours(2))
            },
            slots.Select(x => x.StartAt));
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_Marks_Past_Slots_Unavailable()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        db.WorkingHours.Add(new WorkingHoursRule
        {
            ClinicId = clinicId,
            DayOfWeek = DayOfWeek.Monday,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(10, 0),
            SlotMinutes = 30,
            IsClosed = false
        });
        await db.SaveChangesAsync();

        var service = new SchedulingService(
            new FakeSchedulingRepository(db),
            new FakeClinicContext(clinicId),
            CreateMapper(),
            new FakeTimeProvider(new DateTimeOffset(2026, 6, 29, 9, 20, 0, TimeSpan.FromHours(2))), new TestBookingStore(db));

        var slots = await service.GetAvailableSlotsAsync(new DateOnly(2026, 6, 29));

        Assert.False(slots.Single(x => x.StartAt == new DateTimeOffset(2026, 6, 29, 9, 0, 0, TimeSpan.FromHours(2))).IsAvailable);
        Assert.True(slots.Single(x => x.StartAt == new DateTimeOffset(2026, 6, 29, 9, 30, 0, TimeSpan.FromHours(2))).IsAvailable);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_Uses_Active_Event_Rules_Instead_Of_Standard_Rules()
    {
        var clinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        db.WorkingHours.Add(new WorkingHoursRule
        {
            ClinicId = clinicId,
            DayOfWeek = DayOfWeek.Monday,
            OpensAt = new TimeOnly(8, 0),
            ClosesAt = new TimeOnly(16, 0),
            SlotMinutes = 60,
            IsClosed = false
        });
        db.WorkingHoursEvents.Add(new WorkingHoursEvent
        {
            ClinicId = clinicId,
            Title = "Holiday hours",
            DisplayFrom = new DateOnly(2026, 6, 1),
            StartsOn = new DateOnly(2026, 6, 29),
            EndsOn = new DateOnly(2026, 7, 3),
            IsPublished = true,
            Rules =
            [
                new WorkingHoursEventRule
                {
                    DayOfWeek = DayOfWeek.Monday,
                    OpensAt = new TimeOnly(10, 0),
                    ClosesAt = new TimeOnly(12, 0),
                    SlotMinutes = 30,
                    IsClosed = false
                }
            ]
        });
        await db.SaveChangesAsync();

        var slots = await CreateService(db, new FakeClinicContext(clinicId)).GetAvailableSlotsAsync(new DateOnly(2026, 6, 29));

        Assert.Equal(
            new[]
            {
                new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 6, 29, 10, 30, 0, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 6, 29, 11, 0, 0, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 6, 29, 11, 30, 0, TimeSpan.FromHours(2))
            },
            slots.Select(x => x.StartAt));
    }

    [Fact]
    public async Task GetWorkingHoursEventsAsync_Preserves_History_Without_Mutating_On_Read()
    {
        var clinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        await using var db = new FakeAppDbContext();
        db.WorkingHoursEvents.AddRange(
            CreateEvent(clinicId, new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 2)),
            CreateEvent(otherClinicId, new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 2)),
            CreateEvent(clinicId, new DateOnly(2099, 1, 1), new DateOnly(2099, 1, 2)));
        await db.SaveChangesAsync();

        var events = await CreateService(db, new FakeClinicContext(clinicId)).GetWorkingHoursEventsAsync();

        Assert.Equal(2, events.Count);
        Assert.Contains(events, x => x.StartsOn == new DateOnly(2099, 1, 1));
        Assert.Equal(3, await db.WorkingHoursEvents.CountAsync());
        Assert.True(await db.WorkingHoursEvents.AnyAsync(x => x.ClinicId == otherClinicId));
    }

    private static WorkingHoursEvent CreateEvent(Guid clinicId, DateOnly startsOn, DateOnly endsOn) => new()
    {
        ClinicId = clinicId,
        Title = "Test event",
        DisplayFrom = startsOn,
        StartsOn = startsOn,
        EndsOn = endsOn,
        IsPublished = true,
        Rules =
        [
            new WorkingHoursEventRule
            {
                DayOfWeek = startsOn.DayOfWeek,
                OpensAt = TimeOnly.MinValue,
                ClosesAt = TimeOnly.MinValue,
                SlotMinutes = 30,
                IsClosed = true
            }
        ]
    };

    private static DtoMapper CreateMapper() => new DtoMapper();

    private static SchedulingService CreateService(FakeAppDbContext db, IClinicContext clinicContext) =>
        new(new FakeSchedulingRepository(db), clinicContext, CreateMapper(), new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.FromHours(2))), new TestBookingStore(db));

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
        public async Task<IReadOnlyList<AppointmentRequest>> GetLatestAsync(Guid clinicId, int take, CancellationToken cancellationToken = default) => await db.AppointmentRequests.Where(x => x.ClinicId == clinicId).Take(take).ToListAsync(cancellationToken);
        public async Task<IReadOnlyList<AppointmentType>> GetTypesAsync(Guid clinicId, bool publicOnly, CancellationToken cancellationToken = default) => await db.AppointmentTypes.Where(x => x.ClinicId == clinicId).ToListAsync(cancellationToken);
        public Task<AppointmentType?> FindTypeAsync(Guid clinicId, int id, CancellationToken cancellationToken = default) => db.AppointmentTypes.SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.Id == id, cancellationToken);
        public async Task<IReadOnlyList<AppointmentRequest>> GetBookedAsync(Guid clinicId, CancellationToken cancellationToken = default) => await db.AppointmentRequests.Where(x => x.ClinicId == clinicId && x.Status != AppointmentStatus.Cancelled && x.RequestedStartAt != null).ToListAsync(cancellationToken);
        public void Add(AppointmentRequest appointment) => db.AppointmentRequests.Add(appointment); public void Add(AppointmentType type) => db.AppointmentTypes.Add(type); public void Remove(AppointmentType type) => db.AppointmentTypes.Remove(type); public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
    }

    private sealed class FakeSchedulingRepository(FakeAppDbContext db) : ISchedulingRepository
    {
        public async Task<IReadOnlyList<WorkingHoursRule>> GetRulesAsync(Guid clinicId, CancellationToken ct = default) => await db.WorkingHours.Where(x => x.ClinicId == clinicId).OrderBy(x => x.DayOfWeek).ToListAsync(ct);
        public Task<WorkingHoursRule?> FindRuleAsync(Guid clinicId, DayOfWeek day, CancellationToken ct = default) => db.WorkingHours.FirstOrDefaultAsync(x => x.ClinicId == clinicId && x.DayOfWeek == day, ct);
        public async Task<IReadOnlyList<WorkingHoursEvent>> GetEventsAsync(Guid clinicId, CancellationToken ct = default) => await db.WorkingHoursEvents.Include(x => x.Rules).Where(x => x.ClinicId == clinicId).OrderBy(x => x.StartsOn).ToListAsync(ct);
        public Task<WorkingHoursEvent?> FindEventAsync(Guid clinicId, int id, CancellationToken ct = default) => db.WorkingHoursEvents.Include(x => x.Rules).SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.Id == id, ct);
        public Task<WorkingHoursEventRule?> FindActiveEventRuleAsync(Guid clinicId, DateOnly date, CancellationToken ct = default) => db.WorkingHoursEvents.Where(x => x.ClinicId == clinicId && x.IsPublished && x.StartsOn <= date && x.EndsOn >= date).OrderByDescending(x => x.StartsOn).SelectMany(x => x.Rules).FirstOrDefaultAsync(x => x.DayOfWeek == date.DayOfWeek, ct);
        public async Task RemoveExpiredEventsAsync(Guid clinicId, DateOnly today, CancellationToken ct = default) { var e = await db.WorkingHoursEvents.Where(x => x.ClinicId == clinicId && x.EndsOn < today).ToListAsync(ct); db.WorkingHoursEvents.RemoveRange(e); await db.SaveChangesAsync(ct); }
        public async Task ReplaceRulesAsync(Guid clinicId, IReadOnlyCollection<WorkingHoursRule> rules, CancellationToken ct = default) { var old = await db.WorkingHours.Where(x => x.ClinicId == clinicId).ToListAsync(ct); db.WorkingHours.RemoveRange(old); db.WorkingHours.AddRange(rules); await db.SaveChangesAsync(ct); }
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
