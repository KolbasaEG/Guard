using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddApiRecoveryWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RecoveryRequiredFrom",
                table: "SynchronizationSettings",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SynchronizationSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "RecoveryRequiredFrom",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryRequiredFrom",
                table: "SynchronizationSettings");
        }
    }
}
