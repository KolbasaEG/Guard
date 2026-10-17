using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddSubdivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subdivision_CreatedBy",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivision_InsertedDate",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivision_IsActive",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivision_Level",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivision_ModifiedBy",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivision_ParentId_IsActive",
                table: "Subdivisions");

            migrationBuilder.DropIndex(
                name: "IX_Subdivision_Path",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Subdivisions");

            migrationBuilder.RenameIndex(
                name: "IX_Subdivision_ParentId",
                table: "Subdivisions",
                newName: "IX_Subdivisions_ParentId");

            migrationBuilder.AlterTable(
                name: "Subdivisions",
                comment: "Иерархический справочник подразделений / отделов / органов организационной структуры системы Guard.",
                oldComment: "Справочник организационной структуры (подразделения). Иерархия реализована через Materialized Path (строка) + ParentId.");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Текущий статус жизненного цикла записи. Значение устанавливается в конструкторе BaseEntity, а не на уровне БД.");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentId",
                table: "Subdivisions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "Родительское подразделение (null = корневое подразделение)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Subdivisions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                comment: "Полное наименование подразделения",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "Название подразделения");

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

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Subdivisions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "Адрес");

            migrationBuilder.AddColumn<string>(
                name: "Fax",
                table: "Subdivisions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "Факс");

            migrationBuilder.AddColumn<bool>(
                name: "IsDepartment",
                table: "Subdivisions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LevelOrder",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Порядковый номер внутри одного уровня иерархии");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Subdivisions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "Телефон");

            migrationBuilder.AddColumn<string>(
                name: "PositionFormationName",
                table: "Subdivisions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Наименование для формирования должности");

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Subdivisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                comment: "Почтовый индекс");

            migrationBuilder.AddColumn<int>(
                name: "StaffCount",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Снимок штатной численности (может устаревать)");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_Status",
                table: "Subdivisions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subdivisions_Status",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "Fax",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "IsDepartment",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "LevelOrder",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "PositionFormationName",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Subdivisions");

            migrationBuilder.DropColumn(
                name: "StaffCount",
                table: "Subdivisions");

            migrationBuilder.RenameIndex(
                name: "IX_Subdivisions_ParentId",
                table: "Subdivisions",
                newName: "IX_Subdivision_ParentId");

            migrationBuilder.AlterTable(
                name: "Subdivisions",
                comment: "Справочник организационной структуры (подразделения). Иерархия реализована через Materialized Path (строка) + ParentId.",
                oldComment: "Иерархический справочник подразделений / отделов / органов организационной структуры системы Guard.");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                comment: "Текущий статус жизненного цикла записи. Значение устанавливается в конструкторе BaseEntity, а не на уровне БД.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentId",
                table: "Subdivisions",
                type: "uuid",
                nullable: true,
                comment: "Родительское подразделение (null = корневое подразделение)",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Subdivisions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "Название подразделения",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldComment: "Полное наименование подразделения");

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

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Subdivisions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "Код подразделения (опционально, уникальный в рамках организации)");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Subdivisions",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "Активно ли подразделение для работы");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Subdivisions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Уровень вложенности в иерархии (0 = корень)");

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Subdivisions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                comment: "Материализованный путь иерархии в виде строки (например: \"1.4.12\"). Используется для быстрых запросов поддеревьев через StartsWith / LIKE.");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivision_CreatedBy",
                table: "Subdivisions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivision_InsertedDate",
                table: "Subdivisions",
                column: "InsertedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivision_IsActive",
                table: "Subdivisions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivision_Level",
                table: "Subdivisions",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivision_ModifiedBy",
                table: "Subdivisions",
                column: "ModifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Subdivision_ParentId_IsActive",
                table: "Subdivisions",
                columns: new[] { "ParentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Subdivision_Path",
                table: "Subdivisions",
                column: "Path");
        }
    }
}
