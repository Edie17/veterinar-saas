namespace Veterinar.Application.Abstractions;

public interface IClinicContext
{
    Guid ClinicId { get; }
    bool HasClinic { get; }
}
