using Veterinar.Domain.Common;

namespace Veterinar.Domain.Appointments;

public sealed class AppointmentType : AuditableEntity
{
    public int Id { get; set; }
    public Guid ClinicId { get; set; }
    public required string Title { get; set; }
    public int DurationMinutes { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title)) throw new InvalidOperationException("Appointment type title is required.");
        if (DurationMinutes is < 5 or > 240) throw new InvalidOperationException("Appointment type duration must be between 5 and 240 minutes.");
    }
}
