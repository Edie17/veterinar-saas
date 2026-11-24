using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Common;
using Veterinar.Domain.Appointments;

namespace Veterinar.Infrastructure.Persistence.Repositories;

/// <summary>SQLite's immediate write transaction serializes reservations across processes.</summary>
public sealed class BookingStore(AppDbContext db) : IBookingStore
{
    public async Task ReserveAsync(AppointmentRequest request, long now, CancellationToken ct = default)
    {
        await WithWriteLockAsync(async () =>
        {
            await db.AppointmentRequests.Where(x => x.Status == AppointmentStatus.PendingConfirmation && x.HoldExpiresUtcSeconds <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AppointmentStatus.Cancelled).SetProperty(x => x.ConfirmationTokenHash, (string?)null).SetProperty(x => x.ManagementTokenHash, (string?)null), ct);
            if (request.StartUtcSeconds is null || request.EndUtcSeconds is null || request.StartUtcSeconds >= request.EndUtcSeconds)
                throw new RequestException("Vybraný termín není platný.");
            var conflict = await Occupying(request.ClinicId, now)
                .AnyAsync(x => x.StartUtcSeconds < request.EndUtcSeconds && x.EndUtcSeconds > request.StartUtcSeconds, ct);
            if (conflict) throw new RequestException("Vybraný termín už není dostupný. Vyberte prosím jiný čas.", 409);
            if (request.IsPublicBooking) await CheckConfirmedQuotaAsync(request, now, ct);
            db.AppointmentRequests.Add(request);
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    public Task ConfirmAsync(Guid clinicId, string tokenHash, long now, CancellationToken ct = default) => WithWriteLockAsync(async () =>
    {
        var request = await db.AppointmentRequests.SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.ConfirmationTokenHash == tokenHash, ct);
        if (request is null || request.Status != AppointmentStatus.PendingConfirmation || request.HoldExpiresUtcSeconds <= now || request.StartUtcSeconds <= now)
            throw new RequestException("Odkaz je neplatný nebo vypršel. Vyberte prosím nový termín.", 409);
        // Mailbox possession is established here. Check inside the same write lock as the transition.
        if (request.IsPublicBooking) await CheckConfirmedQuotaAsync(request, now, ct);
        request.Status = AppointmentStatus.Confirmed;
        request.HoldExpiresUtcSeconds = null;
        request.ConfirmationTokenHash = null;
        await db.SaveChangesAsync(ct);
    }, ct);

    public Task<AppointmentRequest?> FindManagedAsync(Guid clinicId, string tokenHash, CancellationToken ct = default) =>
        db.AppointmentRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.ManagementTokenHash == tokenHash, ct);

    private async Task<AppointmentRequest> RequireManagedAsync(Guid clinicId, string tokenHash, long now, CancellationToken ct)
    {
        var request = await db.AppointmentRequests.SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.ManagementTokenHash == tokenHash, ct);
        if (request is null || request.Status != AppointmentStatus.Confirmed || request.StartUtcSeconds <= now)
            throw new RequestException("Odkaz pro správu rezervace je neplatný nebo už není platný.", 404);
        return request;
    }
    public Task CancelManagedAsync(Guid clinicId, string tokenHash, long now, CancellationToken ct = default) => WithWriteLockAsync(async () =>
    {
        var request = await RequireManagedAsync(clinicId, tokenHash, now, ct);
        request.Status = AppointmentStatus.Cancelled;
        request.ConfirmationTokenHash = null;
        request.ManagementTokenHash = null;
        await db.SaveChangesAsync(ct);
    }, ct);
    public Task MoveManagedAsync(Guid clinicId, string tokenHash, DateTimeOffset start, DateTimeOffset end, long now, CancellationToken ct = default) => WithWriteLockAsync(async () =>
    {
        var request = await RequireManagedAsync(clinicId, tokenHash, now, ct);
        if (start.ToUnixTimeSeconds() <= now || end != start.AddMinutes(request.DurationMinutes)) throw new RequestException("Vybraný termín není platný.");
        if (await Occupying(clinicId, now).AnyAsync(x => x.Id != request.Id && x.StartUtcSeconds < end.ToUnixTimeSeconds() && x.EndUtcSeconds > start.ToUnixTimeSeconds(), ct))
            throw new RequestException("Vybraný termín už není dostupný. Původní rezervace zůstává platná.", 409);
        request.RequestedStartAt = start;
        request.RequestedEndAt = end;
        request.StartUtcSeconds = start.ToUnixTimeSeconds();
        request.EndUtcSeconds = end.ToUnixTimeSeconds();
        await db.SaveChangesAsync(ct);
    }, ct);

    public Task CancelAsync(Guid clinicId, int id, CancellationToken ct = default) => WithWriteLockAsync(async () =>
    {
        var request = await db.AppointmentRequests.SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.Id == id, ct)
            ?? throw new RequestException("Rezervace nebyla nalezena.", 404);
        request.Status = AppointmentStatus.Cancelled;
        request.ConfirmationTokenHash = null;
        request.ManagementTokenHash = null;
        await db.SaveChangesAsync(ct);
    }, ct);

    public async Task<IReadOnlyList<BookedInterval>> GetIntervalsAsync(Guid clinicId, long start, long end, long now, CancellationToken ct = default) =>
        await Occupying(clinicId, now).AsNoTracking().Where(x => x.StartUtcSeconds < end && x.EndUtcSeconds > start)
            .Select(x => new BookedInterval(x.StartUtcSeconds!.Value, x.EndUtcSeconds!.Value)).ToListAsync(ct);

    private async Task CheckConfirmedQuotaAsync(AppointmentRequest request, long now, CancellationToken ct)
    {
        if (await db.AppointmentRequests.CountAsync(x => x.ClinicId == request.ClinicId && x.IsPublicBooking
            && x.CreatedUtcSeconds > now - 86400 && x.Email == request.Email && x.Status == AppointmentStatus.Confirmed, ct) >= 2)
            throw new RequestException("Pro tento kontakt bylo dosaženo denního limitu rezervací. Kontaktujte prosím ordinaci.", 429);
    }

    private IQueryable<AppointmentRequest> Occupying(Guid clinicId, long now) => db.AppointmentRequests.Where(x => x.ClinicId == clinicId
        && x.Status != AppointmentStatus.Cancelled && x.StartUtcSeconds != null && x.EndUtcSeconds != null
        && (x.Status != AppointmentStatus.PendingConfirmation || x.HoldExpiresUtcSeconds > now));

    private async Task WithWriteLockAsync(Func<Task> action, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        // No read occurs before the lock. A contending writer waits up to the connection timeout.
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using var attached = await db.Database.UseTransactionAsync(transaction, ct);
        try { await action(); await transaction.CommitAsync(ct); }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
