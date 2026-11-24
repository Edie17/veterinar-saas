using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Clinics;

namespace Veterinar.Api.Security;

public sealed class ClinicCookieEvents(IClinicUserRepository users) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var validId = Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
        var user = validId ? await users.FindActiveByIdAsync(id, context.HttpContext.RequestAborted) : null;
        var version = user is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash)));
        if (user is null || principal?.FindFirstValue("clinic_id") != ClinicDefaults.DefaultClinicId.ToString()
            || principal.FindFirstValue(ClaimTypes.Role) != user.Role.ToString() || principal.FindFirstValue("auth_version") != version)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    }
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) { context.Response.StatusCode = 401; return Task.CompletedTask; }
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) { context.Response.StatusCode = 403; return Task.CompletedTask; }
}
