using Veterinar.Domain.Clinics;
using Veterinar.Domain.Users;

namespace Veterinar.Domain.Tests;

public sealed class TenantModelTests
{
    [Fact]
    public void Clinic_Should_Normalize_Slug_For_Public_Routing()
    {
        var clinic = Clinic.Create(" Veterinární Ordinace Na Tlapce ", null, "Europe/Prague");

        Assert.Equal("veterinarni-ordinace-na-tlapce", clinic.Slug);
        Assert.Equal("Europe/Prague", clinic.TimeZone);
    }

    [Theory]
    [InlineData(ClinicUserRole.Owner, true, true, true)]
    [InlineData(ClinicUserRole.Admin, true, true, true)]
    [InlineData(ClinicUserRole.Vet, false, false, true)]
    [InlineData(ClinicUserRole.Receptionist, false, false, true)]
    [InlineData(ClinicUserRole.Viewer, false, false, false)]
    public void RolePermissions_Should_Expose_Least_Privilege_Defaults(
        ClinicUserRole role,
        bool canManageSettings,
        bool canManageCms,
        bool canManageAppointments)
    {
        Assert.Equal(canManageSettings, RolePermissions.CanManageClinicSettings(role));
        Assert.Equal(canManageCms, RolePermissions.CanManageCms(role));
        Assert.Equal(canManageAppointments, RolePermissions.CanManageAppointments(role));
    }
}
