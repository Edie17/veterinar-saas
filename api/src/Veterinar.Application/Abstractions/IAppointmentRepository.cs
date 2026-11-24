using Veterinar.Domain.Appointments;

namespace Veterinar.Application.Abstractions;

/// <summary>Persistence port for appointment types and reservations.</summary>
public interface IAppointmentRepository
{
    Task<AppointmentType?> FindPublishedTypeByTitleAsync(Guid clinicId, string title, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppointmentRequest>> GetLatestAsync(Guid clinicId, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppointmentType>> GetTypesAsync(Guid clinicId, bool publicOnly, CancellationToken cancellationToken = default);
    Task<AppointmentType?> FindTypeAsync(Guid clinicId, int id, CancellationToken cancellationToken = default);
    void Add(AppointmentType appointmentType);
    void Remove(AppointmentType appointmentType);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
