using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddApiSourceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiBaseUrl",
                table: "SynchronizationSettings",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ApiPageSize",
                table: "SynchronizationSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "SynchronizationSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ApiBaseUrl", "ApiPageSize" },
                values: new object[] { "", 500 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiBaseUrl",
                table: "SynchronizationSettings");

            migrationBuilder.DropColumn(
                name: "ApiPageSize",
                table: "SynchronizationSettings");
        }
    }
}
