using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Mig15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Subdivisions_SubdivisionId1",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_SubdivisionId1",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "SubdivisionId1",
                table: "Personals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubdivisionId1",
                table: "Personals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personals_SubdivisionId1",
                table: "Personals",
                column: "SubdivisionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Subdivisions_SubdivisionId1",
                table: "Personals",
                column: "SubdivisionId1",
                principalTable: "Subdivisions",
                principalColumn: "Id");
        }
    }
}
