using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations;

public partial class AddApiHeadSubdivision : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<long>(name: "ApiHeadSubdivisionId", table: "SynchronizationSettings", type: "bigint", nullable: true);
    migrationBuilder.AddCheckConstraint(name: "CK_SynchronizationSettings_ApiHeadSubdivisionId", table: "SynchronizationSettings",
      sql: "\"ApiHeadSubdivisionId\" IS NULL OR \"ApiHeadSubdivisionId\" > 0");
  }

  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropCheckConstraint(name: "CK_SynchronizationSettings_ApiHeadSubdivisionId", table: "SynchronizationSettings");
    migrationBuilder.DropColumn(name: "ApiHeadSubdivisionId", table: "SynchronizationSettings");
  }
}
