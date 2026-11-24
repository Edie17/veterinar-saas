using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veterinar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingSelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagementTokenHash",
                table: "AppointmentRequests",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRequests_ManagementTokenHash",
                table: "AppointmentRequests",
                column: "ManagementTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentRequests_ManagementTokenHash",
                table: "AppointmentRequests");

            migrationBuilder.DropColumn(
                name: "ManagementTokenHash",
                table: "AppointmentRequests");
        }
    }
}
