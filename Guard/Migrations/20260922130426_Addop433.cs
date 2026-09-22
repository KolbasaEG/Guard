using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Addop433 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_SubdivisionId",
                table: "Subdivisions");

            migrationBuilder.AlterColumn<long>(
                name: "ParentSubdivisionId",
                table: "Subdivisions",
                type: "bigint",
                nullable: true,
                comment: "идентификатор родителя",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "идентификатор родителя");

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_SubdivisionId",
                table: "Subdivisions",
                column: "SubdivisionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_SubdivisionId",
                table: "Subdivisions");

            migrationBuilder.AlterColumn<long>(
                name: "ParentSubdivisionId",
                table: "Subdivisions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "идентификатор родителя",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "идентификатор родителя");

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_SubdivisionId",
                table: "Subdivisions",
                column: "SubdivisionId");
        }
    }
}
