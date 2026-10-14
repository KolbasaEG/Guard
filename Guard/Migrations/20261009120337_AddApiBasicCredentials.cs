using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddApiBasicCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiUsername",
                table: "SynchronizationSettings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedApiPassword",
                table: "SynchronizationSettings",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SynchronizationSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ApiUsername", "ProtectedApiPassword" },
                values: new object[] { "", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiUsername",
                table: "SynchronizationSettings");

            migrationBuilder.DropColumn(
                name: "ProtectedApiPassword",
                table: "SynchronizationSettings");
        }
    }
}
