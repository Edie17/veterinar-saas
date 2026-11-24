using Veterinar.Domain.Scheduling;

namespace Veterinar.Application.Abstractions;

/// <summary>Persistence port for clinic opening hours and temporary exceptions.</summary>
public interface ISchedulingRepository
{
    Task<IReadOnlyList<WorkingHoursRule>> GetRulesAsync(Guid clinicId, CancellationToken cancellationToken = default);
    Task<WorkingHoursRule?> FindRuleAsync(Guid clinicId, DayOfWeek dayOfWeek, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkingHoursEvent>> GetEventsAsync(Guid clinicId, CancellationToken cancellationToken = default);
    Task<WorkingHoursEvent?> FindEventAsync(Guid clinicId, int id, CancellationToken cancellationToken = default);
    Task<WorkingHoursEventRule?> FindActiveEventRuleAsync(Guid clinicId, DateOnly date, CancellationToken cancellationToken = default);
    Task ReplaceRulesAsync(Guid clinicId, IReadOnlyCollection<WorkingHoursRule> rules, CancellationToken cancellationToken = default);
    void Add(WorkingHoursEvent workingHoursEvent);
    void Remove(WorkingHoursEvent workingHoursEvent);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
