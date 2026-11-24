using Veterinar.Domain.Users;

namespace Veterinar.Application.Abstractions;

/// <summary>Query port for the authentication use case.</summary>
public interface IClinicUserRepository
{
    Task<ClinicUser?> FindActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClinicUser?> FindActiveByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
}
