using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class adng : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceTasks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceRoutines",
                schema: "public");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceRoutines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PeriodicityDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    TargetFrequencyPerMonth = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRoutines", x => x.Id);
                },
                comment: "Справочник видов и регламентов технического обслуживания");

            migrationBuilder.CreateTable(
                name: "MaintenanceTasks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    ExecutorPersonalId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceRoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    PlannedEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlannedStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    TaskStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceTasks_MaintenanceRoutines_MaintenanceRoutineId",
                        column: x => x.MaintenanceRoutineId,
                        principalSchema: "public",
                        principalTable: "MaintenanceRoutines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTasks_MaintenanceSectors_MaintenanceSectorId",
                        column: x => x.MaintenanceSectorId,
                        principalSchema: "public",
                        principalTable: "MaintenanceSectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTasks_Personals_ExecutorPersonalId",
                        column: x => x.ExecutorPersonalId,
                        principalTable: "Personals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTasks_ProtectedObjects_ProtectedObjectId",
                        column: x => x.ProtectedObjectId,
                        principalSchema: "public",
                        principalTable: "ProtectedObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Запланированные и выполненные регламентные работы");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTasks_ExecutorPersonalId_PlannedStartTime",
                schema: "public",
                table: "MaintenanceTasks",
                columns: new[] { "ExecutorPersonalId", "PlannedStartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTasks_MaintenanceRoutineId",
                schema: "public",
                table: "MaintenanceTasks",
                column: "MaintenanceRoutineId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTasks_MaintenanceSectorId_PlannedStartTime",
                schema: "public",
                table: "MaintenanceTasks",
                columns: new[] { "MaintenanceSectorId", "PlannedStartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTasks_ProtectedObjectId",
                schema: "public",
                table: "MaintenanceTasks",
                column: "ProtectedObjectId");
        }
    }
}
