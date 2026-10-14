using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddApiSynchronization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subdivisions_OrganTypes_OrganTypeId_OrganTypeCode",
                table: "Subdivisions");

            // Сохраняем связь существующих подразделений: прежний Id органа заменяется типом классификатора.
            migrationBuilder.Sql("""
                UPDATE "Subdivisions" AS s
                SET "OrganTypeId" = o."ClassifierType"
                FROM "OrganTypes" AS o
                WHERE s."OrganTypeId" = o."Id" AND s."OrganTypeCode" = o."Code";
                """);
            migrationBuilder.DropTable(
                name: "OrganTypes");

            migrationBuilder.DropIndex(
                name: "UX_Subdivisions_PersonalSubdivisionId",
                table: "Personals");

            migrationBuilder.AddColumn<bool>(
                name: "ArchivedBySynchronization",
                table: "Subdivisions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "ExternalId",
                table: "Subdivisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExternalParentId",
                table: "Subdivisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedAt",
                table: "Subdivisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SynchronizedAt",
                table: "Subdivisions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "PersonalSubdivisionId",
                table: "Personals",
                type: "bigint",
                nullable: false,
                comment: "Уникальный автоинкрементный идентификатор подразделения сотрудника в АИС Личное дело",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "Уникальный автоинкрементный идентификатор подразделения сотрудника в АИС Личное дело")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<bool>(
                name: "ArchivedBySynchronization",
                table: "Personals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "ExternalId",
                table: "Personals",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImportedFromApi",
                table: "Personals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedAt",
                table: "Personals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImportedFromApi",
                table: "Classifiers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedAt",
                table: "Classifiers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SynchronizationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scheduled = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Added = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Archived = table.Column<int>(type: "integer", nullable: false),
                    Restored = table.Column<int>(type: "integer", nullable: false),
                    Deactivated = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SynchronizationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SynchronizationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    ScheduleEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DailyTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LastScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClassifiersSynchronizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SynchronizationSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SynchronizationSettings",
                columns: new[] { "Id", "ClassifiersSynchronizedAt", "DailyTime", "LastScheduledDate", "Mode", "ModifiedAt", "ModifiedBy", "ScheduleEnabled", "Version" },
                values: new object[] { 1, null, new TimeOnly(2, 0, 0), null, 0, null, null, false, new Guid("ed7ff5f9-f6da-4f37-807b-28b919ec26f2") });

            migrationBuilder.CreateIndex(
                name: "IX_Subdivisions_ExternalId",
                table: "Subdivisions",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personals_ExternalId",
                table: "Personals",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Subdivisions_PersonalSubdivisionId",
                table: "Personals",
                column: "PersonalSubdivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_SynchronizationRuns_PoolId_StartedAt",
                table: "SynchronizationRuns",
                columns: new[] { "PoolId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SynchronizationRuns_Status",
                table: "SynchronizationRuns",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Subdivisions_Classifiers_OrganTypeId_OrganTypeCode",
                table: "Subdivisions",
                columns: new[] { "OrganTypeId", "OrganTypeCode" },
                principalTable: "Classifiers",
                principalColumns: new[] { "Type", "Code" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Автоматический откат удаления OrganTypes и импорта не поддерживается. Для отката используйте резервную копию.");
        }
    }
}