using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Appointments;

namespace Veterinar.Infrastructure.Persistence.Repositories;

public sealed class AppointmentRepository(AppDbContext db) : IAppointmentRepository
{
    public Task<AppointmentType?> FindPublishedTypeByTitleAsync(Guid clinicId, string title, CancellationToken cancellationToken = default) =>
        db.AppointmentTypes.AsNoTracking().SingleOrDefaultAsync(x => x.ClinicId == clinicId && x.IsPublished && x.Title == title, cancellationToken);
    public async Task<IReadOnlyList<AppointmentRequest>> GetLatestAsync(Guid clinicId, int take, CancellationToken cancellationToken = default) =>
        await db.AppointmentRequests.AsNoTracking().Where(x => x.ClinicId == clinicId).OrderByDescending(x => x.Id).Take(take).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<AppointmentType>> GetTypesAsync(Guid clinicId, bool publicOnly, CancellationToken cancellationToken = default) =>
        await db.AppointmentTypes.AsNoTracking().Where(x => x.ClinicId == clinicId && (!publicOnly || x.IsPublished)).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToListAsync(cancellationToken);
    public Task<AppointmentType?> FindTypeAsync(Guid clinicId, int id, CancellationToken cancellationToken = default) =>
        db.AppointmentTypes.SingleOrDefaultAsync(x => x.Id == id && x.ClinicId == clinicId, cancellationToken);
    public void Add(AppointmentType appointmentType) => db.AppointmentTypes.Add(appointmentType);
    public void Remove(AppointmentType appointmentType) => db.AppointmentTypes.Remove(appointmentType);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
