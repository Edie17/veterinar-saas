namespace Veterinar.Api.Security;

public static class CookieSecurity
{
    public static bool IsSecure(IConfiguration configuration) =>
        bool.TryParse(configuration["COOKIE_SECURE"], out var secure) && secure;
}
