using System.ComponentModel.DataAnnotations;

namespace Veterinar.Application.Scheduling;

public sealed record WorkingHoursRuleInput(
    [property: Range(0, 6)] DayOfWeek DayOfWeek,
    [property: Required, MaxLength(5)] string OpensAt,
    [property: Required, MaxLength(5)] string ClosesAt,
    [property: Range(5, 240)] int SlotMinutes,
    bool IsClosed);

public sealed record UpsertWorkingHoursRequest([property: Required, MinLength(1), MaxLength(7)] IReadOnlyList<WorkingHoursRuleInput> Rules);

public sealed record WorkingHoursRuleDto(
    int Id,
    DayOfWeek DayOfWeek,
    string OpensAt,
    string ClosesAt,
    int SlotMinutes,
    bool IsClosed);

public sealed record WorkingHoursEventInput(
    int? Id,
    [property: Required, MaxLength(180)] string Title,
    [property: MaxLength(1000)] string? Note,
    DateOnly DisplayFrom,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsPublished,
    [property: Required, MinLength(1), MaxLength(7)] IReadOnlyList<WorkingHoursRuleInput> Rules);

public sealed record WorkingHoursEventDto(
    int Id,
    string Title,
    string? Note,
    DateOnly DisplayFrom,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsPublished,
    IReadOnlyList<WorkingHoursRuleDto> Rules);

public sealed record PublicWorkingHoursScheduleDto(
    DateOnly Today,
    IReadOnlyList<WorkingHoursRuleDto> StandardRules,
    WorkingHoursEventDto? ActiveEvent,
    IReadOnlyList<WorkingHoursEventDto> UpcomingEvents);

public sealed record AvailableSlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt, bool IsAvailable = true, string? UnavailableReason = null);
