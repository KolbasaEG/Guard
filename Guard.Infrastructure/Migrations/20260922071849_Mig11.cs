using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Mig11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PersonalId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PersonalId",
                table: "AspNetUsers",
                column: "PersonalId",
                unique: true,
                filter: "\"PersonalId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Personals_PersonalId",
                table: "AspNetUsers",
                column: "PersonalId",
                principalTable: "Personals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Personals_PersonalId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PersonalId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalId",
                table: "AspNetUsers");
        }
    }
}
