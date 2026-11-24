using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Users;

namespace Veterinar.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of the application authentication query port.</summary>
public sealed class ClinicUserRepository(AppDbContext db) : IClinicUserRepository
{
    public Task<ClinicUser?> FindActiveByIdAsync(Guid id, CancellationToken cancellationToken = default) => db.ClinicUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.ClinicId == Veterinar.Domain.Clinics.ClinicDefaults.DefaultClinicId, cancellationToken);
    public Task<ClinicUser?> FindActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        db.ClinicUsers.SingleOrDefaultAsync(user => user.Email == normalizedEmail && user.IsActive && user.ClinicId == Veterinar.Domain.Clinics.ClinicDefaults.DefaultClinicId, cancellationToken);
}
