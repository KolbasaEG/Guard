using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations;

public partial class AddIpCanonicalKeys : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>("CanonicalAddress", "IpAddresses", type: "character varying(50)", maxLength: 50, nullable: true);
    migrationBuilder.AddColumn<string>("CanonicalSource", "IpAddresses", type: "character varying(50)", maxLength: 50, nullable: true);
    migrationBuilder.AddCheckConstraint("CK_IpAddresses_CanonicalSource", "IpAddresses",
      "(\"CanonicalAddress\" IS NULL AND \"CanonicalSource\" IS NULL) OR " +
      "(\"CanonicalAddress\" IS NOT NULL AND \"CanonicalSource\" IS NOT NULL AND \"CanonicalSource\" = \"Address\")");
    migrationBuilder.CreateIndex("IX_IpAddresses_CanonicalAddress", "IpAddresses", "CanonicalAddress");
    migrationBuilder.CreateIndex("IX_IpAddresses_MissingCanonicalAddress", "IpAddresses", "Id", filter: "\"CanonicalAddress\" IS NULL");
  }

  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropCheckConstraint("CK_IpAddresses_CanonicalSource", "IpAddresses");
    migrationBuilder.DropIndex("IX_IpAddresses_CanonicalAddress", "IpAddresses");
    migrationBuilder.DropIndex("IX_IpAddresses_MissingCanonicalAddress", "IpAddresses");
    migrationBuilder.DropColumn("CanonicalAddress", "IpAddresses");
    migrationBuilder.DropColumn("CanonicalSource", "IpAddresses");
  }
}
