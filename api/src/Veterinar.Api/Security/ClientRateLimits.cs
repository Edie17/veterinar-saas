using System.Threading.RateLimiting;

namespace Veterinar.Api.Security;

public static class ClientRateLimits
{
    public static string ClientKey(HttpContext context) => context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
    public static RateLimitPartition<string> Partition(HttpContext context, int permits, int minutes) =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        { PermitLimit = permits, Window = TimeSpan.FromMinutes(minutes), QueueLimit = 0 });
}
