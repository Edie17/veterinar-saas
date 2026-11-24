using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veterinar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260701193000_AddAppointmentSlotReservationIndex")]
    public partial class AddAppointmentSlotReservationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRequests_ClinicId_RequestedStartAt",
                table: "AppointmentRequests",
                columns: new[] { "ClinicId", "RequestedStartAt" },
                unique: true,
                filter: "RequestedStartAt IS NOT NULL AND Status <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentRequests_ClinicId_RequestedStartAt",
                table: "AppointmentRequests");
        }
    }
}
