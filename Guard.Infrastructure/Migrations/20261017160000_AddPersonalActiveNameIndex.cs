using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations;

public partial class AddPersonalActiveNameIndex : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder) =>
    migrationBuilder.CreateIndex("IX_Personals_ActiveName", "Personals", ["LastName", "FirstName", "Id"],
      filter: "\"Status\" IN (1, 2, 3)");

  protected override void Down(MigrationBuilder migrationBuilder) =>
    migrationBuilder.DropIndex("IX_Personals_ActiveName", "Personals");
}
