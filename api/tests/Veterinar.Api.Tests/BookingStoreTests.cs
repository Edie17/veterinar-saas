using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Veterinar.Application.Common;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Clinics;
using Veterinar.Infrastructure.Persistence;
using Veterinar.Infrastructure.Persistence.Repositories;

namespace Veterinar.Api.Tests;

public sealed class BookingStoreTests : IAsyncLifetime
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "vet-booking-test-" + Guid.NewGuid() + ".db");
    private const long Now = 1800000000;
    private AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False;Default Timeout=5").Options);
    public async Task InitializeAsync() { await using var db = Open(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() { File.Delete(path); return Task.CompletedTask; }
    private static AppointmentRequest Request(long start, long end, bool pending = false, string? hash = null) => new()
    {
        ClinicId = ClinicDefaults.DefaultClinicId, Name = "Test", Phone = "+420777123456", Email = "test@example.cz", PetName = "Pes", PetType = "Pes",
        RequestedStartAt = DateTimeOffset.FromUnixTimeSeconds(start), RequestedEndAt = DateTimeOffset.FromUnixTimeSeconds(end), StartUtcSeconds = start,
        EndUtcSeconds = end, CreatedUtcSeconds = Now, CreatedAt = DateTimeOffset.FromUnixTimeSeconds(Now), IsPublicBooking = pending,
        Status = pending ? AppointmentStatus.PendingConfirmation : AppointmentStatus.Confirmed, HoldExpiresUtcSeconds = pending ? Now + 900 : null, ConfirmationTokenHash = hash
    };
    private async Task<bool> Reserve(long start, long end)
    {
        await using var db = Open();
        try { await new BookingStore(db).ReserveAsync(Request(start, end), Now); return true; }
        catch (RequestException ex) when (ex.StatusCode == 409) { return false; }
    }
    [Fact]
    public async Task Unverified_Email_Holds_Do_Not_Consume_Victims_Quota()
    {
        for (var i = 1; i <= 2; i++)
        {
            await using var db = Open();
            await new BookingStore(db).ReserveAsync(Request(Now + i * 3600, Now + i * 3600 + 600, true, "attacker" + i), Now);
        }
        await using var victimDb = Open();
        var store = new BookingStore(victimDb);
        await store.ReserveAsync(Request(Now + 10800, Now + 11400, true, "victim"), Now + 1);
        await store.ConfirmAsync(ClinicDefaults.DefaultClinicId, "victim", Now + 2);
        Assert.Equal(1, await victimDb.AppointmentRequests.CountAsync(x => x.Status == AppointmentStatus.Confirmed));
    }
    [Fact]
    public async Task Concurrent_Distinct_Starts_Cannot_Overlap()
    {
        var outcomes = await Task.WhenAll(Task.Run(() => Reserve(Now + 3600, Now + 5400)), Task.Run(() => Reserve(Now + 4200, Now + 6000)));
        Assert.Single(outcomes.Where(x => x));
        await using var db = Open(); Assert.Equal(1, await db.AppointmentRequests.CountAsync());
    }
    [Fact]
    public async Task Adjacent_Intervals_Are_Accepted()
    { Assert.True(await Reserve(Now + 3600, Now + 4200)); Assert.True(await Reserve(Now + 4200, Now + 4800)); }
    [Fact]
    public async Task Same_Instant_In_Another_Offset_Still_Conflicts()
    {
        Assert.True(await Reserve(Now + 3600, Now + 4200));
        await using var db = Open(); var request = Request(Now + 3600, Now + 4200);
        request.RequestedStartAt = request.RequestedStartAt!.Value.ToOffset(TimeSpan.FromHours(2));
        await Assert.ThrowsAsync<RequestException>(() => new BookingStore(db).ReserveAsync(request, Now));
    }
    [Fact]
    public async Task Expired_Hold_Releases_Capacity_And_Cannot_Confirm()
    {
        await using (var db = Open()) await new BookingStore(db).ReserveAsync(Request(Now + 3600, Now + 4200, true, "old-token"), Now);
        await using (var db = Open())
        {
            Assert.Empty(await new BookingStore(db).GetIntervalsAsync(ClinicDefaults.DefaultClinicId, Now, Now + 86400, Now + 901));
            await Assert.ThrowsAsync<RequestException>(() => new BookingStore(db).ConfirmAsync(ClinicDefaults.DefaultClinicId, "old-token", Now + 901));
        }
        await using (var db = Open()) await new BookingStore(db).ReserveAsync(Request(Now + 3600, Now + 4200), Now + 901);
        await using (var db = Open()) Assert.Equal(1, await db.AppointmentRequests.CountAsync(x => x.Status == AppointmentStatus.Cancelled));
    }
    [Fact]
    public async Task Confirm_Is_One_Time_And_Keeps_Capacity_Blocked()
    {
        await using (var db = Open()) await new BookingStore(db).ReserveAsync(Request(Now + 3600, Now + 4200, true, "token"), Now);
        await using (var db = Open()) await new BookingStore(db).ConfirmAsync(ClinicDefaults.DefaultClinicId, "token", Now + 1);
        await using (var db = Open()) await Assert.ThrowsAsync<RequestException>(() => new BookingStore(db).ConfirmAsync(ClinicDefaults.DefaultClinicId, "token", Now + 2));
        Assert.False(await Reserve(Now + 3700, Now + 4300));
    }
    [Fact]
    public async Task Contact_Quota_And_Admin_Override_Use_Same_Overlap_Boundary()
    {
        for (var i = 1; i <= 2; i++)
        {
            await using var db = Open(); var store = new BookingStore(db);
            await store.ReserveAsync(Request(Now + i * 3600, Now + i * 3600 + 600, true, "token" + i), Now);
            await store.ConfirmAsync(ClinicDefaults.DefaultClinicId, "token" + i, Now + 1);
        }
        await using (var db = Open())
        {
            var ex = await Assert.ThrowsAsync<RequestException>(() => new BookingStore(db).ReserveAsync(Request(Now + 10800, Now + 11400, true, "token3"), Now));
            Assert.Equal(429, ex.StatusCode);
        }
        Assert.True(await Reserve(Now + 10800, Now + 11400));
    }
    [Fact]
    public async Task Precreated_Holds_Cannot_Bypass_Quota_With_Concurrent_Confirmation()
    {
        for (var i = 1; i <= 3; i++)
        {
            await using var db = Open();
            await new BookingStore(db).ReserveAsync(Request(Now + i * 3600, Now + i * 3600 + 600, true, "quota" + i), Now);
        }
        await using (var db = Open()) await new BookingStore(db).ConfirmAsync(ClinicDefaults.DefaultClinicId, "quota1", Now + 1);
        async Task<bool> Confirm(string token)
        {
            await using var db = Open();
            try { await new BookingStore(db).ConfirmAsync(ClinicDefaults.DefaultClinicId, token, Now + 2); return true; }
            catch (RequestException ex) when (ex.StatusCode == 429) { return false; }
        }
        var outcomes = await Task.WhenAll(Task.Run(() => Confirm("quota2")), Task.Run(() => Confirm("quota3")));
        Assert.Single(outcomes.Where(x => x));
        await using var checkDb = Open();
        Assert.Equal(2, await checkDb.AppointmentRequests.CountAsync(x => x.Status == AppointmentStatus.Confirmed));
        Assert.Equal(1, await checkDb.AppointmentRequests.CountAsync(x => x.Status == AppointmentStatus.PendingConfirmation));
    }
    [Fact]
    public async Task Daily_Range_Includes_Cross_Midnight_And_Excludes_Unrelated_History()
    {
        Assert.True(await Reserve(Now - 300, Now + 300)); Assert.True(await Reserve(Now - 86400, Now - 85800));
        await using var db = Open(); var intervals = await new BookingStore(db).GetIntervalsAsync(ClinicDefaults.DefaultClinicId, Now, Now + 86400, Now);
        Assert.Single(intervals); Assert.Equal(Now - 300, intervals[0].Start);
    }
    [Fact]
    public async Task Cancel_Preserves_Record_And_Frees_Capacity()
    {
        Assert.True(await Reserve(Now + 3600, Now + 4200));
        await using (var db = Open()) { var id = await db.AppointmentRequests.Select(x => x.Id).SingleAsync(); await new BookingStore(db).CancelAsync(ClinicDefaults.DefaultClinicId, id); }
        Assert.True(await Reserve(Now + 3600, Now + 4200));
    }
    [Fact]
    public async Task Unverified_Phone_Cannot_Deny_Another_Customers_Quota()
    {
        for (var i = 1; i <= 2; i++) { await using var db = Open(); await new BookingStore(db).ReserveAsync(Request(Now + i * 3600, Now + i * 3600 + 600, true, "token" + i), Now); }
        await using var victimDb = Open(); var victim = Request(Now + 10800, Now + 11400, true, "victim-token"); victim.Email = "victim@example.cz";
        await new BookingStore(victimDb).ReserveAsync(victim, Now + 901);
    }
    [Fact]
    public async Task Cancelled_Delivery_Attempts_Do_Not_Consume_Contact_Quota()
    {
        for (var i = 1; i <= 2; i++)
        {
            await using var db = Open(); var request = Request(Now + i * 3600, Now + i * 3600 + 600, true, "token" + i);
            var store = new BookingStore(db); await store.ReserveAsync(request, Now); await store.CancelAsync(request.ClinicId, request.Id);
        }
        await using var retryDb = Open(); await new BookingStore(retryDb).ReserveAsync(Request(Now + 10800, Now + 11400, true, "retry"), Now);
    }
    [Fact]
    public async Task Migration_Preserves_Legacy_Appointment_And_Content_Tables()
    {
        // Dedicated second file, never the caller's clinic database.
        var legacyPath = path + ".legacy";
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={legacyPath};Pooling=False").Options);
            var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20260701200000_AddAppointmentTypes");
            var clinicId = ClinicDefaults.DefaultClinicId;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO AppointmentRequests (ClinicId,Name,Phone,PetName,PetType,Status,CreatedAt,RequestedStartAt,DurationMinutes) VALUES ({clinicId},'Legacy','123','B','Pes','New','2026-10-12 08:00:00+02:00','2026-10-13 23:50:00+02:00',30)");
            await migrator.MigrateAsync();
            var item = await db.AppointmentRequests.SingleAsync();
            Assert.Equal(AppointmentStatus.New, item.Status);
            Assert.Equal(new DateTimeOffset(2026, 10, 13, 23, 50, 0, TimeSpan.FromHours(2)).ToUnixTimeSeconds(), item.StartUtcSeconds);
            Assert.Equal(item.StartUtcSeconds + 1800, item.EndUtcSeconds);
            Assert.False(item.IsPublicBooking);
            Assert.Empty(await db.Articles.ToListAsync()); Assert.Empty(await db.WorkingHoursEvents.ToListAsync());
        }
        finally { File.Delete(legacyPath); }
    }
}
