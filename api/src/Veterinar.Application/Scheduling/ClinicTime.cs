namespace Veterinar.Application.Scheduling;

/// <summary>The clinic's wall clock is independent of the server and browser time zones.</summary>
public static class ClinicTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
    public static DateOnly Date(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);
    public static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local) || Zone.IsAmbiguousTime(local))
            throw new Common.RequestException("Tento čas není dostupný kvůli změně letního času.");
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }
}
