namespace Veterinar.Application.Abstractions;

public interface ICsrfTokenService
{
    string CreateToken();
    bool IsValid(string? token);
}
