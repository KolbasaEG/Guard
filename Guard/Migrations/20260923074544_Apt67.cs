using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Apt67 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserIpAddresses");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PersonalId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "IpAddresses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PersonalIpAddresses",
                columns: table => new
                {
                    PersonalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddressesId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalIpAddresses", x => new { x.PersonalsId, x.IpAddressesId });
                    table.ForeignKey(
                        name: "FK_PersonalIpAddresses_IpAddresses_IpAddressesId",
                        column: x => x.IpAddressesId,
                        principalTable: "IpAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonalIpAddresses_Personals_PersonalsId",
                        column: x => x.PersonalsId,
                        principalTable: "Personals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IpAddresses_ApplicationUserId",
                table: "IpAddresses",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PersonalId",
                table: "AspNetUsers",
                column: "PersonalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalIpAddresses_IpAddressesId",
                table: "PersonalIpAddresses",
                column: "IpAddressesId");

            migrationBuilder.AddForeignKey(
                name: "FK_IpAddresses_AspNetUsers_ApplicationUserId",
                table: "IpAddresses",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IpAddresses_AspNetUsers_ApplicationUserId",
                table: "IpAddresses");

            migrationBuilder.DropTable(
                name: "PersonalIpAddresses");

            migrationBuilder.DropIndex(
                name: "IX_IpAddresses_ApplicationUserId",
                table: "IpAddresses");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PersonalId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "IpAddresses");

            migrationBuilder.CreateTable(
                name: "UserIpAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIpAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserIpAddresses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserIpAddresses_IpAddresses_IpAddressId",
                        column: x => x.IpAddressId,
                        principalTable: "IpAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Join-таблица связи многие-ко-многим между пользователями и IP-адресами. Хранит метаданные назначения (кто, когда, с какой целью назначил IP).");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PersonalId",
                table: "AspNetUsers",
                column: "PersonalId",
                unique: true,
                filter: "\"PersonalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserIpAddresses_IpAddressId",
                table: "UserIpAddresses",
                column: "IpAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_UserIpAddresses_UserId_IpAddressId",
                table: "UserIpAddresses",
                columns: new[] { "UserId", "IpAddressId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserIpAddresses_UserId_IsActive",
                table: "UserIpAddresses",
                columns: new[] { "UserId", "IsActive" });
        }
    }
}
