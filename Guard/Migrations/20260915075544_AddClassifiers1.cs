using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddClassifiers1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Subdivisions",
                type: "text",
                nullable: false,
                defaultValue: "",
                comment: "Путь подразделения");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_Path",
                table: "Subdivisions",
                column: "Path")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_Path",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Subdivisions");
        }
    }
}
