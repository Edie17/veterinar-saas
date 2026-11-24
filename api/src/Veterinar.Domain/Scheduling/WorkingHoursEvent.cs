using Veterinar.Domain.Common;

namespace Veterinar.Domain.Scheduling;

public sealed class WorkingHoursEvent : AuditableEntity
{
    public int Id { get; set; }
    public Guid ClinicId { get; set; }
    public string Title { get; set; } = "";
    public string? Note { get; set; }
    public DateOnly DisplayFrom { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsPublished { get; set; } = true;
    public List<WorkingHoursEventRule> Rules { get; set; } = [];

    public bool IsActiveOn(DateOnly date) => IsPublished && StartsOn <= date && EndsOn >= date;
    public bool IsVisibleBeforeStartOn(DateOnly date) => IsPublished && DisplayFrom <= date && StartsOn > date;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new InvalidOperationException("Event title is required.");
        }

        if (DisplayFrom > StartsOn)
        {
            throw new InvalidOperationException("Display date must be on or before event start.");
        }

        if (StartsOn > EndsOn)
        {
            throw new InvalidOperationException("Event start must be on or before event end.");
        }

        if (Rules.Count == 0)
        {
            throw new InvalidOperationException("Event must contain at least one opening-hours rule.");
        }

        foreach (var rule in Rules)
        {
            rule.Validate();
        }
    }
}
