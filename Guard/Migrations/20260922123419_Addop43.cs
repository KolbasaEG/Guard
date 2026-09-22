using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Addop43 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_SubdivisionId",
                table: "Subdivisions");

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_SubdivisionId",
                table: "Subdivisions",
                column: "SubdivisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_SubdivisionId",
                table: "Subdivisions");

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_SubdivisionId",
                table: "Subdivisions",
                column: "SubdivisionId",
                unique: true);
        }
    }
}
