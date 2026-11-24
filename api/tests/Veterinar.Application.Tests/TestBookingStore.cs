using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Appointments;

namespace Veterinar.Application.Tests;

internal sealed class TestBookingStore(DbContext db) : IBookingStore
{
    public async Task ReserveAsync(AppointmentRequest request, long now, CancellationToken ct = default)
    { db.Set<AppointmentRequest>().Add(request); await db.SaveChangesAsync(ct); }
    public Task ConfirmAsync(Guid clinicId, string tokenHash, long now, CancellationToken ct = default) => throw new NotSupportedException();
    public Task CancelAsync(Guid clinicId, int id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<AppointmentRequest?> FindManagedAsync(Guid clinicId, string tokenHash, CancellationToken ct = default) => throw new NotSupportedException();
    public Task CancelManagedAsync(Guid clinicId, string tokenHash, long now, CancellationToken ct = default) => throw new NotSupportedException();
    public Task MoveManagedAsync(Guid clinicId, string tokenHash, DateTimeOffset start, DateTimeOffset end, long now, CancellationToken ct = default) => throw new NotSupportedException();
    public async Task<IReadOnlyList<BookedInterval>> GetIntervalsAsync(Guid clinicId, long start, long end, long now, CancellationToken ct = default) =>
        (await db.Set<AppointmentRequest>().Where(x => x.ClinicId == clinicId && x.Status != AppointmentStatus.Cancelled && x.RequestedStartAt != null).ToListAsync(ct))
            .Select(x => new BookedInterval(x.RequestedStartAt!.Value.ToUnixTimeSeconds(), (x.RequestedEndAt ?? x.RequestedStartAt.Value.AddMinutes(x.DurationMinutes)).ToUnixTimeSeconds()))
            .Where(x => x.Start < end && x.End > start).ToList();
}
internal sealed class TestBookingEmail : IBookingEmail
{
    public Task SendConfirmationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct, string? managementToken = null) => Task.CompletedTask;
}
