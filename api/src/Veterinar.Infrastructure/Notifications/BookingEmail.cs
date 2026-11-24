using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Veterinar.Application.Abstractions;
using Veterinar.Application.Common;

namespace Veterinar.Infrastructure.Notifications;

public sealed class BookingEmail(IConfiguration config, ILogger<BookingEmail> logger) : IBookingEmail
{
    public async Task SendConfirmationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct, string? managementToken = null)
    {
        if (string.IsNullOrWhiteSpace(config["SMTP_HOST"]))
            throw new RequestException("Online rezervace nyní nejsou dostupné. Kontaktujte prosím ordinaci telefonicky.", 503);
        var origin = new Uri(config["PUBLIC_ORIGIN"] ?? "http://localhost:5173");
        var link = new Uri(origin, "/rezervace/potvrzeni").AbsoluteUri + "#token=" + Uri.EscapeDataString(token);
        var management = managementToken is null ? "" : "\n\nPo potvrzení můžete termín přesunout, zrušit nebo přidat do kalendáře:\n"
            + new Uri(origin, "/rezervace/sprava").AbsoluteUri + "#token=" + Uri.EscapeDataString(managementToken)
            + "\nTento soukromý odkaz nikomu nepřeposílejte. Platí do začátku návštěvy.";
        using var message = new MailMessage(config["SMTP_FROM"]!, email)
        {
            Subject = "Potvrzení rezervace veterinární ordinace",
            Body = $"Potvrďte rezervaci otevřením odkazu a stiskem tlačítka Potvrdit rezervaci:\n{link}\n\nOdkaz platí 15 minut. Pokud jste rezervaci nevytvořili, zprávu ignorujte.{management}",
            IsBodyHtml = false
        };
        using var smtp = new SmtpClient(config["SMTP_HOST"], int.Parse(config["SMTP_PORT"] ?? "587"))
        {
            EnableSsl = !string.Equals(config["SMTP_TLS"], "false", StringComparison.OrdinalIgnoreCase),
            Timeout = 15000
        };
        if (!string.IsNullOrWhiteSpace(config["SMTP_USER"])) smtp.Credentials = new NetworkCredential(config["SMTP_USER"], config["SMTP_PASSWORD"]);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { await smtp.SendMailAsync(message, timeout.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or OperationCanceledException)
        {
            logger.LogError("Booking confirmation delivery failed: {ExceptionType}", ex.GetType().Name);
            throw new RequestException("Potvrzovací e-mail se nepodařilo odeslat. Zkuste to později nebo zavolejte do ordinace.", 503);
        }
    }
}
