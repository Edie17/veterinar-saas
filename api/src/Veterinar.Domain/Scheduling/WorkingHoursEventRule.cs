using Veterinar.Domain.Common;

namespace Veterinar.Domain.Scheduling;

public sealed class WorkingHoursEventRule : AuditableEntity
{
    public int Id { get; set; }
    public int WorkingHoursEventId { get; set; }
    public WorkingHoursEvent? Event { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public int SlotMinutes { get; set; } = 30;
    public bool IsClosed { get; set; }

    public void Validate()
    {
        if (SlotMinutes is < 5 or > 240)
        {
            throw new InvalidOperationException("Slot length must be between 5 and 240 minutes.");
        }

        if (!IsClosed && OpensAt >= ClosesAt)
        {
            throw new InvalidOperationException("Opening time must be before closing time for open event days.");
        }
    }
}
