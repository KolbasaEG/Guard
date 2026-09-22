using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Addop4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_ParentSubdivisionId",
                table: "Subdivisions");

            migrationBuilder.AlterColumn<double>(
                name: "StaffCount",
                table: "Subdivisions",
                type: "double precision",
                nullable: false,
                comment: "Снимок штатной численности (может устаревать)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Снимок штатной численности (может устаревать)");

            migrationBuilder.AlterColumn<long>(
                name: "ParentSubdivisionId",
                table: "Subdivisions",
                type: "bigint",
                nullable: false,
                comment: "идентификатор родителя",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "Уникальный автоинкрементный идентификатор родителя")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_ParentSubdivisionId",
                table: "Subdivisions",
                column: "ParentSubdivisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_ParentSubdivisionId",
                table: "Subdivisions");

            migrationBuilder.AlterColumn<int>(
                name: "StaffCount",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                comment: "Снимок штатной численности (может устаревать)",
                oldClrType: typeof(double),
                oldType: "double precision",
                oldComment: "Снимок штатной численности (может устаревать)");

            migrationBuilder.AlterColumn<long>(
                name: "ParentSubdivisionId",
                table: "Subdivisions",
                type: "bigint",
                nullable: false,
                comment: "Уникальный автоинкрементный идентификатор родителя",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "идентификатор родителя")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_ParentSubdivisionId",
                table: "Subdivisions",
                column: "ParentSubdivisionId",
                unique: true);
        }
    }
}
