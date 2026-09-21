using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IpAddresses_IsActive",
                table: "IpAddresses");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "IpAddresses");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "IpAddresses",
                newName: "InsertedDate");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedDate",
                table: "IpAddresses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "IpAddresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "IpAddresses",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastModifiedDate",
                table: "IpAddresses");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "IpAddresses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IpAddresses");

            migrationBuilder.RenameColumn(
                name: "InsertedDate",
                table: "IpAddresses",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Subdivisions",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                comment: "Версия строки для optimistic concurrency control. EF Core автоматически обновляет при изменении записи.");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "IpAddresses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_IpAddresses_IsActive",
                table: "IpAddresses",
                column: "IsActive");
        }
    }
}
