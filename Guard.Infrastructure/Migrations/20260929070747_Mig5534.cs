using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Mig5534 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_PersonalWorkSchedules_PersonalId_Date",
                schema: "public",
                table: "PersonalWorkSchedules",
                newName: "UX_PersonalWorkSchedules_Personal_Date");

            migrationBuilder.RenameIndex(
                name: "IX_MaintenanceSectorObjects_MaintenanceSectorId_ProtectedObjec~",
                schema: "public",
                table: "MaintenanceSectorObjects",
                newName: "UX_MaintenanceSectorObjects_Sector_Object");

            migrationBuilder.AlterTable(
                name: "MaintenanceSectorObjects",
                schema: "public",
                comment: "Связь участков обслуживания и охраняемых объектов",
                oldComment: "Таблица связи участков обслуживания и охраняемых объектов");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "integer",
                nullable: false,
                comment: "Текущий статус жизненного цикла записи.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                comment: "Идентификатор пользователя (string), последним изменившего запись",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата и время последнего изменения записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                comment: "Дата и время создания записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "uuid",
                nullable: false,
                comment: "Уникальный идентификатор записи (UUIDv7)",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "MaintenanceTasks",
                type: "integer",
                nullable: false,
                comment: "Текущий статус жизненного цикла записи.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "MaintenanceTasks",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                comment: "Идентификатор пользователя (string), последним изменившего запись",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "MaintenanceTasks",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата и время последнего изменения записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "MaintenanceTasks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                comment: "Дата и время создания записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "MaintenanceTasks",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "MaintenanceTasks",
                type: "uuid",
                nullable: false,
                comment: "Уникальный идентификатор записи (UUIDv7)",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "MaintenanceSectors",
                type: "integer",
                nullable: false,
                comment: "Текущий статус жизненного цикла записи.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "public",
                table: "MaintenanceSectors",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                comment: "Наименование участка обслуживания",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "MaintenanceSectors",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                comment: "Идентификатор пользователя (string), последним изменившего запись",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "MaintenanceSectors",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата и время последнего изменения записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "MaintenanceSectors",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                comment: "Дата и время создания записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "public",
                table: "MaintenanceSectors",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "Описание участка",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "MaintenanceSectors",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "public",
                table: "MaintenanceSectors",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Дата создания участка",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "MaintenanceSectors",
                type: "uuid",
                nullable: false,
                comment: "Уникальный идентификатор записи (UUIDv7)",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "integer",
                nullable: false,
                comment: "Текущий статус жизненного цикла записи.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                comment: "Идентификатор пользователя (string), последним изменившего запись",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата и время последнего изменения записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                comment: "Дата и время создания записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "uuid",
                nullable: false,
                comment: "Уникальный идентификатор записи (UUIDv7)",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "integer",
                nullable: false,
                comment: "Текущий статус жизненного цикла записи.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                comment: "Идентификатор пользователя (string), последним изменившего запись",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Дата и время последнего изменения записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                comment: "Дата и время создания записи (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "uuid",
                nullable: false,
                comment: "Уникальный идентификатор записи (UUIDv7)",
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "UX_PersonalWorkSchedules_Personal_Date",
                schema: "public",
                table: "PersonalWorkSchedules",
                newName: "IX_PersonalWorkSchedules_PersonalId_Date");

            migrationBuilder.RenameIndex(
                name: "UX_MaintenanceSectorObjects_Sector_Object",
                schema: "public",
                table: "MaintenanceSectorObjects",
                newName: "IX_MaintenanceSectorObjects_MaintenanceSectorId_ProtectedObjec~");

            migrationBuilder.AlterTable(
                name: "MaintenanceSectorObjects",
                schema: "public",
                comment: "Таблица связи участков обслуживания и охраняемых объектов",
                oldComment: "Связь участков обслуживания и охраняемых объектов");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Текущий статус жизненного цикла записи.");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true,
                oldComment: "Идентификатор пользователя (string), последним изменившего запись");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Дата и время последнего изменения записи (UTC)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                oldComment: "Дата и время создания записи (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldComment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "PersonalWorkSchedules",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Уникальный идентификатор записи (UUIDv7)");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "MaintenanceTasks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Текущий статус жизненного цикла записи.");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "MaintenanceTasks",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true,
                oldComment: "Идентификатор пользователя (string), последним изменившего запись");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "MaintenanceTasks",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Дата и время последнего изменения записи (UTC)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "MaintenanceTasks",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                oldComment: "Дата и время создания записи (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "MaintenanceTasks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldComment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "MaintenanceTasks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Уникальный идентификатор записи (UUIDv7)");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "MaintenanceSectors",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Текущий статус жизненного цикла записи.");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "public",
                table: "MaintenanceSectors",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldComment: "Наименование участка обслуживания");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "MaintenanceSectors",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true,
                oldComment: "Идентификатор пользователя (string), последним изменившего запись");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "MaintenanceSectors",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Дата и время последнего изменения записи (UTC)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "MaintenanceSectors",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                oldComment: "Дата и время создания записи (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "public",
                table: "MaintenanceSectors",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true,
                oldComment: "Описание участка");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "MaintenanceSectors",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldComment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "public",
                table: "MaintenanceSectors",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "Дата создания участка");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "MaintenanceSectors",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Уникальный идентификатор записи (UUIDv7)");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Текущий статус жизненного цикла записи.");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true,
                oldComment: "Идентификатор пользователя (string), последним изменившего запись");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Дата и время последнего изменения записи (UTC)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                oldComment: "Дата и время создания записи (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldComment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "MaintenanceSectorObjects",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Уникальный идентификатор записи (UUIDv7)");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Текущий статус жизненного цикла записи.");

            migrationBuilder.AlterColumn<string>(
                name: "ModifiedBy",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true,
                oldComment: "Идентификатор пользователя (string), последним изменившего запись");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastModifiedDate",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Дата и время последнего изменения записи (UTC)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InsertedDate",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW() AT TIME ZONE 'UTC'",
                oldComment: "Дата и время создания записи (UTC)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldComment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "public",
                table: "MaintenanceRoutines",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Уникальный идентификатор записи (UUIDv7)");
        }
    }
}
