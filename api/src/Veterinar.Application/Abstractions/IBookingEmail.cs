namespace Veterinar.Application.Abstractions;

public interface IBookingEmail
{
    Task SendConfirmationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct, string? managementToken = null);
}
