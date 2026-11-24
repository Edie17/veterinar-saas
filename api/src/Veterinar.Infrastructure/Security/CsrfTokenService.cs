using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Veterinar.Application.Abstractions;

namespace Veterinar.Infrastructure.Security;

public sealed class CsrfTokenService(IConfiguration config) : ICsrfTokenService
{
    private readonly byte[] _secret = Encoding.UTF8.GetBytes(config["CSRF_SECRET"] ?? "dev-only-change-this-csrf-secret");

    public string CreateToken()
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = $"{nonce}.{timestamp}";
        return $"{payload}.{Sign(payload)}";
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 160) return false;
        var parts = token.Split('.');
        if (parts.Length != 3 || !long.TryParse(parts[1], out var unix)) return false;
        if (unix < -62135596800 || unix > 253402300799) return false;
        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix);
        if (age > TimeSpan.FromHours(8) || age < TimeSpan.FromMinutes(-5)) return false;
        var expected = Sign($"{parts[0]}.{parts[1]}");
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[2]));
    }

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(_secret);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
