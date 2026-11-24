using System.ComponentModel.DataAnnotations;

namespace Veterinar.Application.Appointments;

public sealed record CreateAppointmentRequest(
    [property: Required, MaxLength(80)] string Name,
    [property: Required, MaxLength(40)] string Phone,
    [property: Required, EmailAddress, MaxLength(120)] string Email,
    [property: Required, MaxLength(60)] string PetName,
    [property: Required, MaxLength(40)] string PetType,
    [property: Required, MaxLength(120)] string VisitReason,
    int DurationMinutes,
    [property: MaxLength(600)] string? Message,
    DateTimeOffset? RequestedStartAt = null,
    DateTimeOffset? RequestedEndAt = null);

public sealed record AppointmentDto(int Id, string Name, string Phone, string? Email, string PetName, string PetType, string? VisitReason, int DurationMinutes, string? Message, string Status, DateTimeOffset CreatedAt, DateTimeOffset? RequestedStartAt, DateTimeOffset? RequestedEndAt, DateTimeOffset? HoldExpiresAt = null);

public sealed record AppointmentTypeInput(
    int? Id,
    [property: Required, MaxLength(120)] string Title,
    [property: Range(5, 240)] int DurationMinutes,
    int SortOrder,
    bool IsPublished);

public sealed record AppointmentTypeDto(int Id, string Title, int DurationMinutes, int SortOrder, bool IsPublished);

// Only the details needed by the holder of the private management link.
public sealed record ManagedAppointmentDto(int Id, string PetName, string? VisitReason, int DurationMinutes, DateTimeOffset? StartAt, DateTimeOffset? EndAt, string Status);
