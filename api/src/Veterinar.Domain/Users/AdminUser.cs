using Veterinar.Domain.Common;

namespace Veterinar.Domain.Users;

public sealed class AdminUser : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string Role { get; set; } = "Admin";
    public bool IsActive { get; set; } = true;
}
