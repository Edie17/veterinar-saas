namespace Veterinar.Domain.Users;

public static class RolePermissions
{
    public static bool CanManageClinicSettings(ClinicUserRole role) => role is ClinicUserRole.Owner or ClinicUserRole.Admin;

    public static bool CanManageCms(ClinicUserRole role) => role is ClinicUserRole.Owner or ClinicUserRole.Admin;

    public static bool CanManageAppointments(ClinicUserRole role) => role is ClinicUserRole.Owner or ClinicUserRole.Admin or ClinicUserRole.Vet or ClinicUserRole.Receptionist;

    public static bool CanViewSensitiveClientData(ClinicUserRole role) => role is ClinicUserRole.Owner or ClinicUserRole.Admin or ClinicUserRole.Vet or ClinicUserRole.Receptionist;
}
