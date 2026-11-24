using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veterinar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleClinicBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite >= 3.35 supports native transactional column removal; avoid EF's table rebuild.
            migrationBuilder.Sql("ALTER TABLE Clinics DROP COLUMN Status;");
            migrationBuilder.Sql("ALTER TABLE Clinics DROP COLUMN SubscriptionPlan;");

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationTokenHash",
                table: "AppointmentRequests",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedUtcSeconds",
                table: "AppointmentRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "EndUtcSeconds",
                table: "AppointmentRequests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HoldExpiresUtcSeconds",
                table: "AppointmentRequests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublicBooking",
                table: "AppointmentRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "StartUtcSeconds",
                table: "AppointmentRequests",
                type: "INTEGER",
                nullable: true);

            // Preserve legacy states and normalize instants for indexed range queries.
            migrationBuilder.Sql("""
                UPDATE AppointmentRequests SET
                    CreatedUtcSeconds = COALESCE(CAST(strftime('%s', CreatedAt) AS INTEGER), 0),
                    StartUtcSeconds = CAST(strftime('%s', RequestedStartAt) AS INTEGER),
                    EndUtcSeconds = COALESCE(CAST(strftime('%s', RequestedEndAt) AS INTEGER),
                        CAST(strftime('%s', RequestedStartAt) AS INTEGER) + CASE WHEN DurationMinutes > 0 THEN DurationMinutes ELSE 30 END * 60);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRequests_ClinicId_CreatedUtcSeconds",
                table: "AppointmentRequests",
                columns: new[] { "ClinicId", "CreatedUtcSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRequests_ClinicId_StartUtcSeconds_EndUtcSeconds",
                table: "AppointmentRequests",
                columns: new[] { "ClinicId", "StartUtcSeconds", "EndUtcSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentRequests_ConfirmationTokenHash",
                table: "AppointmentRequests",
                column: "ConfirmationTokenHash",
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentRequests_ClinicId_CreatedUtcSeconds",
                table: "AppointmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentRequests_ClinicId_StartUtcSeconds_EndUtcSeconds",
                table: "AppointmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentRequests_ConfirmationTokenHash",
                table: "AppointmentRequests");

            migrationBuilder.DropColumn(
                name: "ConfirmationTokenHash",
                table: "AppointmentRequests");

            migrationBuilder.DropColumn(
                name: "CreatedUtcSeconds",
                table: "AppointmentRequests");

            migrationBuilder.DropColumn(
                name: "EndUtcSeconds",
                table: "AppointmentRequests");

            migrationBuilder.DropColumn(
                name: "HoldExpiresUtcSeconds",
                table: "AppointmentRequests");

            migrationBuilder.DropColumn(
                name: "IsPublicBooking",
                table: "AppointmentRequests");

            migrationBuilder.DropColumn(
                name: "StartUtcSeconds",
                table: "AppointmentRequests");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Clinics",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionPlan",
                table: "Clinics",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }
    }
}
