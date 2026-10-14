using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddApiExitEventsAndQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastChangesSynchronizedAt",
                table: "SynchronizationSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFullSynchronizedAt",
                table: "SynchronizationSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExitEvents",
                table: "SynchronizationRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "SynchronizationRuns",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAt",
                table: "SynchronizationRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatedExitEvents",
                table: "SynchronizationRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedSettingsVersion",
                table: "SynchronizationRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnknownExitEmployees",
                table: "SynchronizationRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ApiPersonnelExits",
                columns: table => new
                {
                    PersonalExternalId = table.Column<long>(type: "bigint", nullable: false),
                    EventAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiPersonnelExits", x => x.PersonalExternalId);
                });

            migrationBuilder.UpdateData(
                table: "SynchronizationSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LastChangesSynchronizedAt", "LastFullSynchronizedAt" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiPersonnelExits");

            migrationBuilder.DropColumn(
                name: "LastChangesSynchronizedAt",
                table: "SynchronizationSettings");

            migrationBuilder.DropColumn(
                name: "LastFullSynchronizedAt",
                table: "SynchronizationSettings");

            migrationBuilder.DropColumn(
                name: "ExitEvents",
                table: "SynchronizationRuns");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "SynchronizationRuns");

            migrationBuilder.DropColumn(
                name: "QueuedAt",
                table: "SynchronizationRuns");

            migrationBuilder.DropColumn(
                name: "RepeatedExitEvents",
                table: "SynchronizationRuns");

            migrationBuilder.DropColumn(
                name: "RequestedSettingsVersion",
                table: "SynchronizationRuns");

            migrationBuilder.DropColumn(
                name: "UnknownExitEmployees",
                table: "SynchronizationRuns");
        }
    }
}
