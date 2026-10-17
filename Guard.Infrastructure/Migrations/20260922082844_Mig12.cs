using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Mig12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_PersonnelCategoryCodeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_PersonnelCategoryTypeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_PositionCodeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_PositionTypeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_SpecialRankCodeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_SpecialRankTypeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_StatusCodeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_StatusTypeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_WorkerCategoryCodeId",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_WorkerCategoryTypeId",
                table: "Personals");

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

            migrationBuilder.DropIndex(
                name: "IX_Personals_PersonnelCategoryCodeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_PersonnelCategoryTypeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_PositionCodeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_PositionTypeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_SpecialRankCodeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_SpecialRankTypeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_StatusCodeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_StatusTypeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_WorkerCategoryCodeId",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_WorkerCategoryTypeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "OrganTypeCodeId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "StatusCodeId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "StatusTypeId",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "PersonnelCategoryCodeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "PersonnelCategoryTypeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "PositionCodeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "PositionTypeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "SpecialRankCodeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "SpecialRankTypeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "StatusCodeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "StatusTypeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "WorkerCategoryCodeId",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "WorkerCategoryTypeId",
                table: "Personals");

            migrationBuilder.AlterTable(
                name: "Subdivisions",
                comment: "Иерархический справочник подразделений организационной структуры системы Guard.",
                oldComment: "Иерархический справочник подразделений / отделов / органов организационной структуры системы Guard.");

            migrationBuilder.AlterColumn<int>(
                name: "SubdivisionId",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                comment: "Уникальный автоинкрементный идентификатор для Path",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "Уникальный автоинкрементный идентификатор для Path")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                comment: "Текущий статус жизненного цикла записи.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "OrganTypeId",
                table: "Subdivisions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "Subdivisions",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                comment: "Идентификатор пользователя (string), последним изменившего запись",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                table: "Subdivisions",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата и время последнего изменения записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                table: "Subdivisions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                comment: "Дата и время создания записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Subdivisions",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Subdivisions",
                type: "uuid",
                nullable: false,
                comment: "Уникальный идентификатор записи (UUIDv7)",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "OrganTypeCode",
                table: "Subdivisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "Subdivisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusType",
                table: "Subdivisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonnelCategoryCode",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonnelCategoryType",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionCode",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionType",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecialRankCode",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecialRankType",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusType",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkerCategoryCode",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkerCategoryType",
                table: "Personals",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "Classifiers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                comment: "Отображаемое значение элемента",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldComment: "Отображаемое значение элемента (например: 'Паспорт', 'Водительское удостоверение')");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Classifiers",
                type: "integer",
                nullable: false,
                comment: "Тип классификатора (числовой код)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Тип классификатора (enum ClassifierType)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Classifiers",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "Флаг актуальности записи",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true,
                oldComment: "Флаг актуальности записи (true — активен, false — неактивен)");

            migrationBuilder.AlterColumn<int>(
                name: "Code",
                table: "Classifiers",
                type: "integer",
                nullable: false,
                comment: "Код значения внутри конкретного классификатора",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Код значения внутри конкретного классификатора (1, 2, 3...)");

            migrationBuilder.AlterColumn<string>(
                name: "ClassifierName",
                table: "Classifiers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                comment: "Наименование группы классификатора",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldComment: "Наименование группы классификатора (например: 'Тип документа')");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Classifiers",
                type: "integer",
                nullable: false,
                comment: "Уникальный идентификатор записи классификатора",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "Уникальный идентификатор записи классификатора")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Classifiers_Type_Code",
                table: "Classifiers",
                columns: new[] { "Type", "Code" });

            migrationBuilder.CreateTable(
                name: "OrganTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Идентификатор типа органа"),
                    ClassifierType = table.Column<int>(type: "integer", nullable: false, defaultValue: 906, comment: "Тип классификатора (906)"),
                    Code = table.Column<int>(type: "integer", nullable: false, comment: "Код элемента классификатора"),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Наименование типа органа")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganTypes", x => x.Id);
                    table.UniqueConstraint("AK_OrganTypes_Id_Code", x => new { x.Id, x.Code });
                    table.ForeignKey(
                        name: "FK_OrganTypes_Classifiers_ClassifierType_Code",
                        columns: x => new { x.ClassifierType, x.Code },
                        principalTable: "Classifiers",
                        principalColumns: new[] { "Type", "Code" },
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Справочник типов органов");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_OrganTypeId_OrganTypeCode",
                table: "Subdivisions",
                columns: new[] { "OrganTypeId", "OrganTypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_StatusType_StatusCode",
                table: "Subdivisions",
                columns: new[] { "StatusType", "StatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Personals_PersonnelCategoryType_PersonnelCategoryCode",
                table: "Personals",
                columns: new[] { "PersonnelCategoryType", "PersonnelCategoryCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Personals_PositionType_PositionCode",
                table: "Personals",
                columns: new[] { "PositionType", "PositionCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Personals_SpecialRankType_SpecialRankCode",
                table: "Personals",
                columns: new[] { "SpecialRankType", "SpecialRankCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Personals_StatusType_StatusCode",
                table: "Personals",
                columns: new[] { "StatusType", "StatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Personals_WorkerCategoryType_WorkerCategoryCode",
                table: "Personals",
                columns: new[] { "WorkerCategoryType", "WorkerCategoryCode" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganTypes_ClassifierType_Code",
                table: "OrganTypes",
                columns: new[] { "ClassifierType", "Code" });

            migrationBuilder.CreateIndex(
                name: "UQ_OrganTypes_Id_Code",
                table: "OrganTypes",
                columns: new[] { "Id", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_PersonnelCategoryType_PersonnelCatego~",
                table: "Personals",
                columns: new[] { "PersonnelCategoryType", "PersonnelCategoryCode" },
                principalTable: "Classifiers",
                principalColumns: new[] { "Type", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_PositionType_PositionCode",
                table: "Personals",
                columns: new[] { "PositionType", "PositionCode" },
                principalTable: "Classifiers",
                principalColumns: new[] { "Type", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_SpecialRankType_SpecialRankCode",
                table: "Personals",
                columns: new[] { "SpecialRankType", "SpecialRankCode" },
                principalTable: "Classifiers",
                principalColumns: new[] { "Type", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_StatusType_StatusCode",
                table: "Personals",
                columns: new[] { "StatusType", "StatusCode" },
                principalTable: "Classifiers",
                principalColumns: new[] { "Type", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_WorkerCategoryType_WorkerCategoryCode",
                table: "Personals",
                columns: new[] { "WorkerCategoryType", "WorkerCategoryCode" },
                principalTable: "Classifiers",
                principalColumns: new[] { "Type", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subdivisions_Classifiers_StatusType_StatusCode",
                table: "Subdivisions",
                columns: new[] { "StatusType", "StatusCode" },
                principalTable: "Classifiers",
                principalColumns: new[] { "Type", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subdivisions_OrganTypes_OrganTypeId_OrganTypeCode",
                table: "Subdivisions",
                columns: new[] { "OrganTypeId", "OrganTypeCode" },
                principalTable: "OrganTypes",
                principalColumns: new[] { "Id", "Code" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_PersonnelCategoryType_PersonnelCatego~",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_PositionType_PositionCode",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_SpecialRankType_SpecialRankCode",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_StatusType_StatusCode",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Personals_Classifiers_WorkerCategoryType_WorkerCategoryCode",
                table: "Personals");

            migrationBuilder.DropForeignKey(
                name: "FK_Subdivisions_Classifiers_StatusType_StatusCode",
                table: "Subdivisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subdivisions_OrganTypes_OrganTypeId_OrganTypeCode",
                table: "Subdivisions");

            migrationBuilder.DropTable(
                name: "OrganTypes");

            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_OrganTypeId_OrganTypeCode",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_StatusType_StatusCode",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Personals_PersonnelCategoryType_PersonnelCategoryCode",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_PositionType_PositionCode",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_SpecialRankType_SpecialRankCode",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_StatusType_StatusCode",
                table: "Personals");

            migrationBuilder.DropIndex(
                name: "IX_Personals_WorkerCategoryType_WorkerCategoryCode",
                table: "Personals");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Classifiers_Type_Code",
                table: "Classifiers");

            migrationBuilder.DropColumn(
                name: "OrganTypeCode",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "StatusType",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "PersonnelCategoryCode",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "PersonnelCategoryType",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "PositionCode",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "PositionType",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "SpecialRankCode",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "SpecialRankType",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "StatusType",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "WorkerCategoryCode",
                table: "Personals");

            migrationBuilder.DropColumn(
                name: "WorkerCategoryType",
                table: "Personals");

            migrationBuilder.AlterTable(
                name: "Subdivisions",
                comment: "Иерархический справочник подразделений / отделов / органов организационной структуры системы Guard.",
                oldComment: "Иерархический справочник подразделений организационной структуры системы Guard.");

            migrationBuilder.AlterColumn<long>(
                name: "SubdivisionId",
                table: "Subdivisions",
                type: "bigint",
                nullable: false,
                comment: "Уникальный автоинкрементный идентификатор для Path",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Уникальный автоинкрементный идентификатор для Path")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Текущий статус жизненного цикла записи.");

            migrationBuilder.AlterColumn<long>(
                name: "OrganTypeId",
                table: "Subdivisions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                table: "Subdivisions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true,
                oldComment: "Идентификатор пользователя (string), последним изменившего запись");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                table: "Subdivisions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Дата и время последнего изменения записи (UTC)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                table: "Subdivisions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                oldComment: "Дата и время создания записи (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "Subdivisions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldComment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Subdivisions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Уникальный идентификатор записи (UUIDv7)");

            migrationBuilder.AddColumn<long>(
                name: "OrganTypeCodeId",
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
                name: "PersonnelCategoryCodeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PersonnelCategoryTypeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PositionCodeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PositionTypeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SpecialRankCodeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SpecialRankTypeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StatusCodeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StatusTypeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkerCategoryCodeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkerCategoryTypeId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "Classifiers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                comment: "Отображаемое значение элемента (например: 'Паспорт', 'Водительское удостоверение')",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldComment: "Отображаемое значение элемента");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Classifiers",
                type: "integer",
                nullable: false,
                comment: "Тип классификатора (enum ClassifierType)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Тип классификатора (числовой код)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Classifiers",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "Флаг актуальности записи (true — активен, false — неактивен)",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true,
                oldComment: "Флаг актуальности записи");

            migrationBuilder.AlterColumn<int>(
                name: "Code",
                table: "Classifiers",
                type: "integer",
                nullable: false,
                comment: "Код значения внутри конкретного классификатора (1, 2, 3...)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Код значения внутри конкретного классификатора");

            migrationBuilder.AlterColumn<string>(
                name: "ClassifierName",
                table: "Classifiers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                comment: "Наименование группы классификатора (например: 'Тип документа')",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldComment: "Наименование группы классификатора");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Classifiers",
                type: "bigint",
                nullable: false,
                comment: "Уникальный идентификатор записи классификатора",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Уникальный идентификатор записи классификатора")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

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
                name: "IX_Personals_PersonnelCategoryCodeId",
                table: "Personals",
                column: "PersonnelCategoryCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_PersonnelCategoryTypeId",
                table: "Personals",
                column: "PersonnelCategoryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_PositionCodeId",
                table: "Personals",
                column: "PositionCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_PositionTypeId",
                table: "Personals",
                column: "PositionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_SpecialRankCodeId",
                table: "Personals",
                column: "SpecialRankCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_SpecialRankTypeId",
                table: "Personals",
                column: "SpecialRankTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_StatusCodeId",
                table: "Personals",
                column: "StatusCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_StatusTypeId",
                table: "Personals",
                column: "StatusTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_WorkerCategoryCodeId",
                table: "Personals",
                column: "WorkerCategoryCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personals_WorkerCategoryTypeId",
                table: "Personals",
                column: "WorkerCategoryTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_PersonnelCategoryCodeId",
                table: "Personals",
                column: "PersonnelCategoryCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_PersonnelCategoryTypeId",
                table: "Personals",
                column: "PersonnelCategoryTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_PositionCodeId",
                table: "Personals",
                column: "PositionCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_PositionTypeId",
                table: "Personals",
                column: "PositionTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_SpecialRankCodeId",
                table: "Personals",
                column: "SpecialRankCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_SpecialRankTypeId",
                table: "Personals",
                column: "SpecialRankTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_StatusCodeId",
                table: "Personals",
                column: "StatusCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_StatusTypeId",
                table: "Personals",
                column: "StatusTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_WorkerCategoryCodeId",
                table: "Personals",
                column: "WorkerCategoryCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personals_Classifiers_WorkerCategoryTypeId",
                table: "Personals",
                column: "WorkerCategoryTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
    }
}
