using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddIpAddressCatalogWithManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IpAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "IP-адрес или CIDR-нотация"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Человекочитаемое название"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpAddresses", x => x.Id);
                },
                comment: "\r\n          Справочник IP-адресов и CIDR-подсетей системы Guard.\r\n          Центральный каталог всех известных IP-адресов и подсетей,\r\n          которые могут быть назначены пользователям, подразделениям и другим сущностям.");

            migrationBuilder.CreateTable(
                name: "UserIpAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IpAddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
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
                comment: "\r\n          Join-таблица связи многие-ко-многим между пользователями и IP-адресами.\r\n          Хранит метаданные назначения (кто, когда, с какой целью назначил IP).");

            migrationBuilder.CreateIndex(
                name: "IX_IpAddresses_Address",
                table: "IpAddresses",
                column: "Address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpAddresses_IsActive",
                table: "IpAddresses",
                column: "IsActive");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserIpAddresses");

            migrationBuilder.DropTable(
                name: "IpAddresses");
        }
    }
}
