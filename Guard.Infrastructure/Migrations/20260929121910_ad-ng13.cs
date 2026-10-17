using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class adng13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
                name: "WorkStart",
                schema: "public",
                table: "MaintenanceSectorMonthlySchedules",
                type: "interval",
                nullable: true,
                comment: "Время начала смены участка",
                oldClrType: typeof(TimeSpan),
                oldType: "interval",
                oldNullable: true);

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "WorkEnd",
                schema: "public",
                table: "MaintenanceSectorMonthlySchedules",
                type: "interval",
                nullable: true,
                comment: "Время окончания смены участка",
                oldClrType: typeof(TimeSpan),
                oldType: "interval",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
                name: "WorkStart",
                schema: "public",
                table: "MaintenanceSectorMonthlySchedules",
                type: "interval",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "interval",
                oldNullable: true,
                oldComment: "Время начала смены участка");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "WorkEnd",
                schema: "public",
                table: "MaintenanceSectorMonthlySchedules",
                type: "interval",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "interval",
                oldNullable: true,
                oldComment: "Время окончания смены участка");
        }
    }
}
