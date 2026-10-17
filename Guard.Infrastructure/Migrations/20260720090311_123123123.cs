using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class _123123123 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "UserIpAddresses",
                comment: "Join-таблица связи многие-ко-многим между пользователями и IP-адресами. Хранит метаданные назначения (кто, когда, с какой целью назначил IP).",
                oldComment: "\r\n          Join-таблица связи многие-ко-многим между пользователями и IP-адресами.\r\n          Хранит метаданные назначения (кто, когда, с какой целью назначил IP).");

            migrationBuilder.AlterTable(
                name: "IpAddresses",
                comment: "Справочник IP-адресов и CIDR-подсетей системы Guard. Центральный каталог всех известных IP-адресов и подсетей, которые могут быть назначены пользователям, подразделениям и другим сущностям.",
                oldComment: "\r\n          Справочник IP-адресов и CIDR-подсетей системы Guard.\r\n          Центральный каталог всех известных IP-адресов и подсетей,\r\n          которые могут быть назначены пользователям, подразделениям и другим сущностям.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "UserIpAddresses",
                comment: "\r\n          Join-таблица связи многие-ко-многим между пользователями и IP-адресами.\r\n          Хранит метаданные назначения (кто, когда, с какой целью назначил IP).",
                oldComment: "Join-таблица связи многие-ко-многим между пользователями и IP-адресами. Хранит метаданные назначения (кто, когда, с какой целью назначил IP).");

            migrationBuilder.AlterTable(
                name: "IpAddresses",
                comment: "\r\n          Справочник IP-адресов и CIDR-подсетей системы Guard.\r\n          Центральный каталог всех известных IP-адресов и подсетей,\r\n          которые могут быть назначены пользователям, подразделениям и другим сущностям.",
                oldComment: "Справочник IP-адресов и CIDR-подсетей системы Guard. Центральный каталог всех известных IP-адресов и подсетей, которые могут быть назначены пользователям, подразделениям и другим сущностям.");
        }
    }
}
