using Veterinar.Application.Mapping;
using Veterinar.Application.Abstractions;

namespace Veterinar.Application.Auth;

public sealed class AuthService(IClinicUserRepository users, IPasswordHasher passwordHasher, DtoMapper mapper)
{
    public async Task<AuthenticatedAdminDto?> ValidateAdminAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 180 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256) return null;
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await users.FindActiveByEmailAsync(email, ct);
        var verified = passwordHasher.Verify(request.Password, user?.PasswordHash);
        if (user is null || !verified) return null;

        return mapper.Map<AuthenticatedAdminDto>(user);
    }
}
