using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Mig14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ParentSubdivisionId",
                table: "Subdivisions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "Уникальный автоинкрементный идентификатор родителя")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Subdivisions",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата последнего обновления информации");

            migrationBuilder.AddColumn<long>(
                name: "PersonalSubdivisionId",
                table: "Personals",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "Уникальный автоинкрементный идентификатор подразделения сотрудника в АИС Личное дело")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_ParentSubdivisionId",
                table: "Subdivisions",
                column: "ParentSubdivisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_PersonalSubdivisionId",
                table: "Personals",
                column: "PersonalSubdivisionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_ParentSubdivisionId",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_PersonalSubdivisionId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "ParentSubdivisionId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "PersonalSubdivisionId",
                table: "Personals");
        }
    }
}
