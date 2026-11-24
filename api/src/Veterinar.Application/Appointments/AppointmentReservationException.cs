using Veterinar.Application.Common;
namespace Veterinar.Application.Appointments;
public sealed class AppointmentReservationException(string message) : RequestException(message, 409);
