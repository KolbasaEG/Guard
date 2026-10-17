using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddClassifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OrganTypeCodeId",
                table: "Subdivisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OrganTypeId",
                table: "Subdivisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StatusCodeId",
                table: "Subdivisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StatusTypeId",
                table: "Subdivisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SubdivisionId",
                table: "Subdivisions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Classifiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "Уникальный идентификатор записи классификатора")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Тип классификатора (enum ClassifierType)"),
                    Code = table.Column<int>(type: "integer", nullable: false, comment: "Код значения внутри конкретного классификатора (1, 2, 3...)"),
                    ClassifierName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "Наименование группы классификатора (например: 'Тип документа')"),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Отображаемое значение элемента (например: 'Паспорт', 'Водительское удостоверение')"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Флаг актуальности записи (true — активен, false — неактивен)"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP", comment: "Дата и время последнего обновления записи (UTC)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classifiers", x => x.Id);
                },
                comment: "Универсальный справочник классификаторов системы");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_OrganTypeCodeId",
                table: "Subdivisions",
                column: "OrganTypeCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_OrganTypeId",
                table: "Subdivisions",
                column: "OrganTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_StatusCodeId",
                table: "Subdivisions",
                column: "StatusCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_StatusTypeId",
                table: "Subdivisions",
                column: "StatusTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Classifiers_Type",
                table: "Classifiers",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Classifiers_Type_Code",
                table: "Classifiers",
                columns: new[] { "Type", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Subdivisions_Classifiers_OrganTypeCodeId",
                table: "Subdivisions",
                column: "OrganTypeCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subdivisions_Classifiers_OrganTypeId",
                table: "Subdivisions",
                column: "OrganTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subdivisions_Classifiers_StatusCodeId",
                table: "Subdivisions",
                column: "StatusCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subdivisions_Classifiers_StatusTypeId",
                table: "Subdivisions",
                column: "StatusTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subdivisions_Classifiers_OrganTypeCodeId",
                table: "Subdivisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subdivisions_Classifiers_OrganTypeId",
                table: "Subdivisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subdivisions_Classifiers_StatusCodeId",
                table: "Subdivisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subdivisions_Classifiers_StatusTypeId",
                table: "Subdivisions");

            migrationBuilder.DropTable(
                name: "Classifiers");

            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_OrganTypeCodeId",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_OrganTypeId",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_StatusCodeId",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_StatusTypeId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "OrganTypeCodeId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "OrganTypeId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "StatusCodeId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "StatusTypeId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "SubdivisionId",
                table: "Subdivisions");
        }
    }
}
