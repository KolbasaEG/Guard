using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Mig13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "SubdivisionId",
                table: "Subdivisions",
                type: "bigint",
                nullable: false,
                comment: "Уникальный автоинкрементный идентификатор для Path",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Уникальный автоинкрементный идентификатор для Path")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDepartment",
                table: "Subdivisions",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "Признак: является ли подразделение отделом",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "PersonalId",
                table: "Personals",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "Идентификатор сотрудника АИС Личное дело")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<Guid>(
                name: "SubdivisionId1",
                table: "Personals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personals_SubdivisionId1",
                table: "Personals",
                column: "SubdivisionId1");

            migrationBuilder.CreateIndex(
                name: "UX_Personals_PersonalId",
                table: "Personals",
                column: "PersonalId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Subdivisions_SubdivisionId1",
                table: "Personals",
                column: "SubdivisionId1",
                principalTable: "Subdivisions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Subdivisions_SubdivisionId1",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_SubdivisionId1",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "UX_Personals_PersonalId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "PersonalId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "SubdivisionId1",
                table: "Personals");

            migrationBuilder.AlterColumn<int>(
                name: "SubdivisionId",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                comment: "Уникальный автоинкрементный идентификатор для Path",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "Уникальный автоинкрементный идентификатор для Path")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDepartment",
                table: "Subdivisions",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false,
                oldComment: "Признак: является ли подразделение отделом");
        }
    }
}
