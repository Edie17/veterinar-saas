using System.Net;
using System.Text.Encodings.Web;
using Veterinar.Application.Abstractions;

namespace Veterinar.Api.Development;

// In-memory capture only: this transport never contacts an SMTP server.
public sealed class DemoMailbox(IConfiguration configuration) : IBookingEmail
{
    public sealed record Message(string Email, string ConfirmationUrl, DateTimeOffset ExpiresAt, string? ManagementUrl);
    private readonly Queue<Message> messages = new();
    private readonly object gate = new();
    public Task SendConfirmationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct, string? managementToken = null)
    {
        ct.ThrowIfCancellationRequested();
        var origin = configuration["PUBLIC_ORIGIN"] ?? "http://localhost:5173";
        var link = new Uri(new Uri(origin), "/rezervace/potvrzeni").AbsoluteUri + "#token=" + Uri.EscapeDataString(token);
        var management = managementToken is null ? null : new Uri(new Uri(origin), "/rezervace/sprava").AbsoluteUri + "#token=" + Uri.EscapeDataString(managementToken);
        lock (gate)
        {
            messages.Enqueue(new Message(email, link, expiresAt, management));
            while (messages.Count > 100) messages.Dequeue();
        }
        return Task.CompletedTask;
    }
    public Message[] Read() { lock (gate) return messages.Reverse().ToArray(); }
    public static bool IsLocal(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        if (ip?.IsIPv4MappedToIPv6 == true) ip = ip.MapToIPv4();
        return ip is not null && IPAddress.IsLoopback(ip);
    }
    public string Html()
    {
        var encode = HtmlEncoder.Default;
        var rows = string.Join("", Read().Select(m => $"<li><strong>{encode.Encode(m.Email)}</strong> — <a href=\"{encode.Encode(m.ConfirmationUrl)}\">Otevřít potvrzení rezervace</a><p>Platnost potvrzení: {m.ExpiresAt:O}</p>"
            + (m.ManagementUrl is null ? "" : $"<p><a href=\"{encode.Encode(m.ManagementUrl)}\">Spravovat rezervaci</a> — po potvrzení, platí do začátku návštěvy.</p>") + "</li>"));
        return "<!doctype html><html lang=\"cs\"><meta charset=\"utf-8\"><title>Testovací schránka</title><main><h1>Testovací schránka</h1><p>Lokální demo. Žádný e-mail se neposílá příjemci. Zprávy se po restartu vymažou.</p><p><a href=\"\">Obnovit schránku</a> · <a href=\"/\">Zpět na web</a></p><ul>" + rows + "</ul></main></html>";
    }
}
