using Veterinar.Application.Mapping;

namespace Veterinar.Application.Tests;

public sealed class MappingProfileTests
{
    [Fact]
    public void ApplicationMappingProfile_Is_Valid()
    {
        var mapper = new DtoMapper();
        var entity = new Veterinar.Domain.Appointments.AppointmentRequest { Name = "A", Phone = "123", PetName = "B", PetType = "Pes", ConfirmationTokenHash = "secret" };
        var json = System.Text.Json.JsonSerializer.Serialize(mapper.Map<Veterinar.Application.Appointments.AppointmentDto>(entity));
        Assert.DoesNotContain("secret", json);
    }
}
