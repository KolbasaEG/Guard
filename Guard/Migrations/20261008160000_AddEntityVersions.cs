using System;
using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Guard.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261008160000_AddEntityVersions")]
public partial class AddEntityVersions : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<int>(
      name: "Id", table: "OrganTypes", type: "integer", nullable: false,
      comment: "Идентификатор типа органа", oldClrType: typeof(int), oldType: "integer",
      oldComment: "Идентификатор типа органа")
      .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
    migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('\"OrganTypes\"', 'Id'), GREATEST(COALESCE((SELECT MAX(\"Id\") FROM \"OrganTypes\"), 0), 0) + 1, false);");
    foreach (var table in new[] { "Personals", "Subdivisions", "Classifiers", "OrganTypes" })
      migrationBuilder.AddColumn<Guid>("Version", table, type: "uuid", nullable: false, defaultValue: Guid.Empty);
  }

  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<int>(
      name: "Id", table: "OrganTypes", type: "integer", nullable: false,
      comment: "Идентификатор типа органа", oldClrType: typeof(int), oldType: "integer",
      oldComment: "Идентификатор типа органа")
      .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
    foreach (var table in new[] { "Personals", "Subdivisions", "Classifiers", "OrganTypes" })
      migrationBuilder.DropColumn("Version", table);
  }
}
