using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Apt617 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IpAddresses_AspNetUsers_ApplicationUserId",
                table: "IpAddresses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PersonalIpAddresses",
                table: "PersonalIpAddresses");

            migrationBuilder.DropIndex(
                name: "IX_PersonalIpAddresses_IpAddressesId",
                table: "PersonalIpAddresses");

            migrationBuilder.DropIndex(
                name: "IX_IpAddresses_ApplicationUserId",
                table: "IpAddresses");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "IpAddresses");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "IpAddresses");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PersonalIpAddresses",
                table: "PersonalIpAddresses",
                columns: new[] { "IpAddressesId", "PersonalsId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalIpAddresses_PersonalsId",
                table: "PersonalIpAddresses",
                column: "PersonalsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PersonalIpAddresses",
                table: "PersonalIpAddresses");

            migrationBuilder.DropIndex(
                name: "IX_PersonalIpAddresses_PersonalsId",
                table: "PersonalIpAddresses");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "IpAddresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "IpAddresses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                comment: "Человекочитаемое название");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PersonalIpAddresses",
                table: "PersonalIpAddresses",
                columns: new[] { "PersonalsId", "IpAddressesId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalIpAddresses_IpAddressesId",
                table: "PersonalIpAddresses",
                column: "IpAddressesId");

            migrationBuilder.CreateIndex(
                name: "IX_IpAddresses_ApplicationUserId",
                table: "IpAddresses",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_IpAddresses_AspNetUsers_ApplicationUserId",
                table: "IpAddresses",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
