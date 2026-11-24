using Veterinar.Api.Security;

namespace Veterinar.Api.Configuration;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!environment.IsProduction()) return;

        var csrfSecret = configuration["CSRF_SECRET"];
        if (string.IsNullOrWhiteSpace(csrfSecret) || csrfSecret.Length < 32 || csrfSecret.Contains("dev-only", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CSRF_SECRET must be configured to a production-grade secret with at least 32 characters.");
        }

        if (!CookieSecurity.IsSecure(configuration)) throw new InvalidOperationException("COOKIE_SECURE=true is required in production.");
        if (string.IsNullOrWhiteSpace(configuration["CONNECTION_STRING"])) throw new InvalidOperationException("CONNECTION_STRING must be configured in production.");
        if (string.IsNullOrWhiteSpace(configuration["DATA_PROTECTION_KEYS"])) throw new InvalidOperationException("DATA_PROTECTION_KEYS must point to persistent private storage.");
        if (!Uri.TryCreate(configuration["PUBLIC_ORIGIN"], UriKind.Absolute, out var origin) || origin.Scheme != "https" || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment)) throw new InvalidOperationException("PUBLIC_ORIGIN must be the clinic's HTTPS origin.");
        if (configuration["AllowedHosts"] != origin.Host) throw new InvalidOperationException("AllowedHosts must match PUBLIC_ORIGIN host.");
        if (string.IsNullOrWhiteSpace(configuration["SMTP_HOST"]) || string.IsNullOrWhiteSpace(configuration["SMTP_FROM"])
            || (configuration["SMTP_TLS"] is { } tls && !string.Equals(tls, "true", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("SMTP_HOST, SMTP_FROM and TLS are required for public booking confirmation.");
        if (configuration["SMTP_PORT"] is { } port && (!int.TryParse(port, out var number) || number is < 1 or > 65535)) throw new InvalidOperationException("SMTP_PORT is invalid.");
    }
}
