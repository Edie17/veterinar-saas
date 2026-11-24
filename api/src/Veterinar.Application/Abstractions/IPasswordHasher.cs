namespace Veterinar.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    // A missing identity must incur current password-verification work and always fail.
    bool Verify(string password, string? encodedHash);
}
