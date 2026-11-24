using Microsoft.EntityFrameworkCore;
using Veterinar.Application.Abstractions;
using Veterinar.Domain.Scheduling;

namespace Veterinar.Infrastructure.Persistence.Repositories;

public sealed class SchedulingRepository(AppDbContext db) : ISchedulingRepository
{
    public async Task<IReadOnlyList<WorkingHoursRule>> GetRulesAsync(Guid clinicId, CancellationToken ct = default) => await db.WorkingHours.AsNoTracking().Where(x => x.ClinicId == clinicId).OrderBy(x => x.DayOfWeek).ToListAsync(ct);
    public Task<WorkingHoursRule?> FindRuleAsync(Guid clinicId, DayOfWeek dayOfWeek, CancellationToken ct = default) => db.WorkingHours.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.ClinicId == clinicId && x.DayOfWeek == dayOfWeek, ct);
    public async Task<IReadOnlyList<WorkingHoursEvent>> GetEventsAsync(Guid clinicId, CancellationToken ct = default) => await db.WorkingHoursEvents.AsNoTracking().Include(x => x.Rules).Where(x => x.ClinicId == clinicId).OrderBy(x => x.StartsOn).ToListAsync(ct);
    public Task<WorkingHoursEvent?> FindEventAsync(Guid clinicId, int id, CancellationToken ct = default) => db.WorkingHoursEvents.Include(x => x.Rules).SingleOrDefaultAsync(x => x.Id == id && x.ClinicId == clinicId, ct);
    public Task<WorkingHoursEventRule?> FindActiveEventRuleAsync(Guid clinicId, DateOnly date, CancellationToken ct = default) => db.WorkingHoursEvents.AsNoTracking().Where(x => x.ClinicId == clinicId && x.IsPublished && x.StartsOn <= date && x.EndsOn >= date).OrderByDescending(x => x.StartsOn).SelectMany(x => x.Rules).FirstOrDefaultAsync(x => x.DayOfWeek == date.DayOfWeek, ct);
    public async Task ReplaceRulesAsync(Guid clinicId, IReadOnlyCollection<WorkingHoursRule> rules, CancellationToken ct = default) { var current = await db.WorkingHours.Where(x => x.ClinicId == clinicId).ToListAsync(ct); db.WorkingHours.RemoveRange(current); db.WorkingHours.AddRange(rules); await db.SaveChangesAsync(ct); }
    public void Add(WorkingHoursEvent workingHoursEvent) => db.WorkingHoursEvents.Add(workingHoursEvent);
    public void Remove(WorkingHoursEvent workingHoursEvent) => db.WorkingHoursEvents.Remove(workingHoursEvent);
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
