using Veterinar.Application.Mapping;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Appointments;
using Veterinar.Domain.Scheduling;

namespace Veterinar.Application.Scheduling;

public sealed class SchedulingService(ISchedulingRepository scheduling, IClinicContext clinicContext, DtoMapper mapper, TimeProvider timeProvider, IBookingStore bookings)
{
    public async Task<AvailableSlotDto?> GetNextAvailableSlotAsync(DateOnly from, int durationMinutes, CancellationToken ct = default)
    {
        var today = GetToday();
        if (from < today || from > today.AddDays(60)) throw new Common.RequestException("Vyberte datum v následujících 60 dnech.");
        for (var day = from; day <= today.AddDays(60); day = day.AddDays(1))
        {
            ct.ThrowIfCancellationRequested();
            var available = (await GetAvailableSlotsAsync(day, durationMinutes, ct)).FirstOrDefault(x => x.IsAvailable);
            if (available is not null) return available;
        }
        return null;
    }
    public async Task<IReadOnlyList<WorkingHoursRuleDto>> GetWorkingHoursAsync(CancellationToken ct = default)
    {


        var rules = await scheduling.GetRulesAsync(clinicContext.ClinicId, ct);
        return mapper.Map<IReadOnlyList<WorkingHoursRuleDto>>(rules);
    }

    public async Task<IReadOnlyList<WorkingHoursRuleDto>> UpsertWorkingHoursAsync(UpsertWorkingHoursRequest request, CancellationToken ct = default)
    {
        var clinicId = clinicContext.ClinicId;
        if (request.Rules is null || request.Rules.Count is < 1 or > 7 || request.Rules.Select(x => x.DayOfWeek).Distinct().Count() != request.Rules.Count) throw new Common.RequestException("Každý den lze zadat jen jednou.");
        var rules = new List<WorkingHoursRule>();
        foreach (var input in request.Rules)
        {
            var rule = new WorkingHoursRule
            {
                ClinicId = clinicId,
                DayOfWeek = input.DayOfWeek,
                OpensAt = ParseTime(input.OpensAt),
                ClosesAt = ParseTime(input.ClosesAt),
                SlotMinutes = input.SlotMinutes,
                IsClosed = input.IsClosed
            };
            Common.ValidationRules.Check(rule.Validate);
            rules.Add(rule);
        }

        await scheduling.ReplaceRulesAsync(clinicId, rules, ct);
        return await GetWorkingHoursAsync(ct);
    }

    public async Task<PublicWorkingHoursScheduleDto> GetPublicScheduleAsync(CancellationToken ct = default)
    {
        var today = GetToday();


        var standardRules = mapper.Map<IReadOnlyList<WorkingHoursRuleDto>>(await scheduling.GetRulesAsync(clinicContext.ClinicId, ct));
        var events = (await scheduling.GetEventsAsync(clinicContext.ClinicId, ct)).Where(x => x.IsPublished && x.DisplayFrom <= today && x.EndsOn >= today).ToList();

        var activeEvent = events
            .Where(x => x.IsActiveOn(today))
            .OrderByDescending(x => x.StartsOn)
            .FirstOrDefault();
        var upcomingEvents = events
            .Where(x => x.IsVisibleBeforeStartOn(today))
            .ToList();

        return new PublicWorkingHoursScheduleDto(
            today,
            standardRules,
            activeEvent is null ? null : mapper.Map<WorkingHoursEventDto>(activeEvent),
            mapper.Map<IReadOnlyList<WorkingHoursEventDto>>(upcomingEvents));
    }

    public async Task<IReadOnlyList<WorkingHoursEventDto>> GetWorkingHoursEventsAsync(CancellationToken ct = default)
    {


        var events = await scheduling.GetEventsAsync(clinicContext.ClinicId, ct);

        return mapper.Map<IReadOnlyList<WorkingHoursEventDto>>(events);
    }

    public async Task<WorkingHoursEventDto> UpsertWorkingHoursEventAsync(WorkingHoursEventInput request, CancellationToken ct = default)
    {
        var clinicId = clinicContext.ClinicId;
        WorkingHoursEvent entity;

        if (request.Id is { } id)
        {
            entity = await scheduling.FindEventAsync(clinicId, id, ct)
                ?? throw new Veterinar.Application.Common.RequestException("Working-hours event was not found.");

            entity.Rules.Clear();
        }
        else
        {
            entity = new WorkingHoursEvent { ClinicId = clinicId };
            scheduling.Add(entity);
        }

        if (request.Rules is null || request.Rules.Count is < 1 or > 7 || request.Rules.Select(x => x.DayOfWeek).Distinct().Count() != request.Rules.Count) throw new Common.RequestException("Každý den lze zadat jen jednou.");
        entity.Title = request.Title.Trim();
        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        entity.DisplayFrom = request.DisplayFrom;
        entity.StartsOn = request.StartsOn;
        entity.EndsOn = request.EndsOn;
        entity.IsPublished = request.IsPublished;

        foreach (var input in request.Rules)
        {
            entity.Rules.Add(new WorkingHoursEventRule
            {
                DayOfWeek = input.DayOfWeek,
                OpensAt = ParseTime(input.OpensAt),
                ClosesAt = ParseTime(input.ClosesAt),
                SlotMinutes = input.SlotMinutes,
                IsClosed = input.IsClosed
            });
        }

        Common.ValidationRules.Check(entity.Validate);
        await scheduling.SaveChangesAsync(ct);

        return mapper.Map<WorkingHoursEventDto>(entity);
    }

    public async Task DeleteWorkingHoursEventAsync(int id, CancellationToken ct = default)
    {
        var clinicId = clinicContext.ClinicId;
        var entity = await scheduling.FindEventAsync(clinicId, id, ct);

        if (entity is null) return;

        scheduling.Remove(entity);
        await scheduling.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AvailableSlotDto>> GetAvailableSlotsAsync(DateOnly date, int? durationMinutes = null, CancellationToken ct = default)
    {
        var clinicId = clinicContext.ClinicId;
        if (durationMinutes is < 5 or > 240) throw new Common.RequestException("Délka návštěvy musí být 5 až 240 minut.");
        if (date < GetToday() || date > GetToday().AddDays(60)) throw new Common.RequestException("Vyberte datum v následujících 60 dnech.");

        var rule = await GetEffectiveRuleAsync(clinicId, date, ct);

        if (rule is null || rule.IsClosed) return Array.Empty<AvailableSlotDto>();
        ValidateRule(rule);
        var duration = durationMinutes ?? rule.SlotMinutes;
        if (duration is < 5 or > 240) throw new Veterinar.Application.Common.RequestException("Appointment duration must be between 5 and 240 minutes.");

        var dayStart = ClinicTime.At(date, TimeOnly.MinValue);
        var dayEnd = ClinicTime.At(date.AddDays(1), TimeOnly.MinValue);
        var bookedIntervals = await bookings.GetIntervalsAsync(clinicId, dayStart.ToUnixTimeSeconds(), dayEnd.ToUnixTimeSeconds(), timeProvider.GetUtcNow().ToUnixTimeSeconds(), ct);
        var cursor = ClinicTime.At(date, rule.OpensAt);
        var close = ClinicTime.At(date, rule.ClosesAt);
        var result = new List<AvailableSlotDto>();
        var now = timeProvider.GetUtcNow();
        while (cursor.AddMinutes(duration) <= close)
        {
            var end = cursor.AddMinutes(duration);
            var overlaps = bookedIntervals.Any(x => cursor.ToUnixTimeSeconds() < x.End && end.ToUnixTimeSeconds() > x.Start);
            var isPast = cursor <= now;
            var unavailableReason = isPast ? "Termín už proběhl" : overlaps ? "Obsazeno" : null;
            result.Add(new AvailableSlotDto(cursor, end, unavailableReason is null, unavailableReason));
            cursor = cursor.AddMinutes(duration);
        }
        return result;
    }

    private async Task<EffectiveWorkingHoursRule?> GetEffectiveRuleAsync(Guid clinicId, DateOnly date, CancellationToken ct)
    {
        var eventRule = await scheduling.FindActiveEventRuleAsync(clinicId, date, ct);

        if (eventRule is not null) return new EffectiveWorkingHoursRule(eventRule.OpensAt, eventRule.ClosesAt, eventRule.SlotMinutes, eventRule.IsClosed);

        var standardRule = await scheduling.FindRuleAsync(clinicId, date.DayOfWeek, ct);
        return standardRule is null ? null : new EffectiveWorkingHoursRule(standardRule.OpensAt, standardRule.ClosesAt, standardRule.SlotMinutes, standardRule.IsClosed);
    }

    private static void ValidateRule(EffectiveWorkingHoursRule rule)
    {
        if (rule.SlotMinutes is < 5 or > 240)
        {
            throw new Veterinar.Application.Common.RequestException("Slot length must be between 5 and 240 minutes.");
        }

        if (!rule.IsClosed && rule.OpensAt >= rule.ClosesAt)
        {
            throw new Veterinar.Application.Common.RequestException("Opening time must be before closing time for open days.");
        }
    }

    private static TimeOnly ParseTime(string value)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            throw new Veterinar.Application.Common.RequestException($"Invalid time value '{value}'. Use HH:mm format.");
        }
        return parsed;
    }

    private DateOnly GetToday() => ClinicTime.Date(timeProvider.GetUtcNow());

    private sealed record EffectiveWorkingHoursRule(TimeOnly OpensAt, TimeOnly ClosesAt, int SlotMinutes, bool IsClosed);
}
