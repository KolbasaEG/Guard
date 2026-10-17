using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountBlockReason",
                table: "AspNetUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActivityAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordChangedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnblockedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"CreatedAtUtc\" = CURRENT_TIMESTAMP;");

            migrationBuilder.CreateTable(
                name: "AccountPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    PasswordExpirationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordDays = table.Column<int>(type: "integer", nullable: false),
                    PasswordEnabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InactivityEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InactivityDays = table.Column<int>(type: "integer", nullable: false),
                    InactivityEnabledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MinimumLength = table.Column<int>(type: "integer", nullable: false),
                    UniqueCharacters = table.Column<int>(type: "integer", nullable: false),
                    RequireDigit = table.Column<bool>(type: "boolean", nullable: false),
                    RequireLowercase = table.Column<bool>(type: "boolean", nullable: false),
                    RequireUppercase = table.Column<bool>(type: "boolean", nullable: false),
                    RequireSymbol = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPolicies", x => x.Id);
                    table.CheckConstraint("CK_AccountPolicies_Id", "\"Id\" = 1");
                    table.CheckConstraint("CK_AccountPolicies_Ranges", "\"PasswordDays\" BETWEEN 1 AND 3650 AND \"InactivityDays\" BETWEEN 1 AND 3650 AND \"MinimumLength\" BETWEEN 6 AND 100 AND \"UniqueCharacters\" BETWEEN 1 AND \"MinimumLength\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_LastActivityAtUtc",
                table: "AspNetUsers",
                column: "LastActivityAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPolicies");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_LastActivityAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccountBlockReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastActivityAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UnblockedAtUtc",
                table: "AspNetUsers");
        }
    }
}
