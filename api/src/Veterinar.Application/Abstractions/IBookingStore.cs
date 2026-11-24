using Veterinar.Domain.Appointments;

namespace Veterinar.Application.Abstractions;

public sealed record BookedInterval(long Start, long End);

/// <summary>All capacity allocation goes through this transactional boundary.</summary>
public interface IBookingStore
{
    Task ReserveAsync(AppointmentRequest request, long now, CancellationToken ct = default);
    Task ConfirmAsync(Guid clinicId, string tokenHash, long now, CancellationToken ct = default);
    Task CancelAsync(Guid clinicId, int id, CancellationToken ct = default);
    Task<AppointmentRequest?> FindManagedAsync(Guid clinicId, string tokenHash, CancellationToken ct = default);
    Task CancelManagedAsync(Guid clinicId, string tokenHash, long now, CancellationToken ct = default);
    Task MoveManagedAsync(Guid clinicId, string tokenHash, DateTimeOffset start, DateTimeOffset end, long now, CancellationToken ct = default);
    Task<IReadOnlyList<BookedInterval>> GetIntervalsAsync(Guid clinicId, long start, long end, long now, CancellationToken ct = default);
}
