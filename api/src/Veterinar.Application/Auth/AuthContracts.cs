namespace Veterinar.Application.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record AuthenticatedAdminDto(string Email, string Role, Guid ClinicId, Guid UserId = default, string? AuthVersion = null);
