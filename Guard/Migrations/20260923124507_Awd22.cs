using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Awd22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubdivisionId",
                table: "IpAddresses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpAddresses_SubdivisionId",
                table: "IpAddresses",
                column: "SubdivisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_IpAddresses_Subdivisions_SubdivisionId",
                table: "IpAddresses",
                column: "SubdivisionId",
                principalTable: "Subdivisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IpAddresses_Subdivisions_SubdivisionId",
                table: "IpAddresses");

            migrationBuilder.DropIndex(
                name: "IX_IpAddresses_SubdivisionId",
                table: "IpAddresses");

            migrationBuilder.DropColumn(
                name: "SubdivisionId",
                table: "IpAddresses");
        }
    }
}
