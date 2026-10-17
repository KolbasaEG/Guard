using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_Status",
                table: "Subdivisions");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_Status_InsertedDate",
                table: "Subdivisions",
                columns: new[] { "Status", "InsertedDate" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_Status_InsertedDate",
                table: "Subdivisions");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_Status",
                table: "Subdivisions",
                column: "Status");
        }
    }
}
