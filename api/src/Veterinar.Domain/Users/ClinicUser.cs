using Veterinar.Domain.Common;

namespace Veterinar.Domain.Users;

public sealed class ClinicUser : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicId { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public ClinicUserRole Role { get; set; } = ClinicUserRole.Admin;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? EmailVerifiedAt { get; set; }
}
