using Veterinar.Domain.Common;

namespace Veterinar.Domain.Appointments;

public sealed class AppointmentRequest : AuditableEntity
{
    public int Id { get; set; }
    public Guid ClinicId { get; set; }
    public required string Name { get; set; }
    public required string Phone { get; set; }
    public string? Email { get; set; }
    public required string PetName { get; set; }
    public required string PetType { get; set; }
    public string? Message { get; set; }
    public string? VisitReason { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public DateTimeOffset? RequestedStartAt { get; set; }
    public DateTimeOffset? RequestedEndAt { get; set; }
    public long? StartUtcSeconds { get; set; }
    public long? EndUtcSeconds { get; set; }
    public long CreatedUtcSeconds { get; set; }
    public long? HoldExpiresUtcSeconds { get; set; }
    public string? ConfirmationTokenHash { get; set; }
    public string? ManagementTokenHash { get; set; }
    public bool IsPublicBooking { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.New;
}
