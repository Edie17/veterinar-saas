using Veterinar.Application.Abstractions;
using Veterinar.Domain.Clinics;

namespace Veterinar.Api.Tenancy;

public sealed class SingleClinicContext : IClinicContext
{
    public Guid ClinicId => ClinicDefaults.DefaultClinicId;
    public bool HasClinic => true;
}
