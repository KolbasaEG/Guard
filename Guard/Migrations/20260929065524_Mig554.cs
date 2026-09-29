using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class Mig554 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "MaintenanceRoutines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PeriodicityDays = table.Column<int>(type: "integer", nullable: false),
                    TargetFrequencyPerMonth = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRoutines", x => x.Id);
                },
                comment: "Справочник видов и регламентов технического обслуживания");

            migrationBuilder.CreateTable(
                name: "MaintenanceSectors",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SubdivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsiblePersonalId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSectors_Personals_ResponsiblePersonalId",
                        column: x => x.ResponsiblePersonalId,
                        principalTable: "Personals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSectors_Subdivisions_SubdivisionId",
                        column: x => x.SubdivisionId,
                        principalTable: "Subdivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Участки обслуживания охраняемых объектов");

            migrationBuilder.CreateTable(
                name: "PersonalWorkSchedules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDayOff = table.Column<bool>(type: "boolean", nullable: false),
                    ShiftStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ShiftEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalWorkSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalWorkSchedules_Personals_PersonalId",
                        column: x => x.PersonalId,
                        principalTable: "Personals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "График работы и смен сотрудников");

            migrationBuilder.CreateTable(
                name: "ProtectedObjects",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    SubdivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    LegalAddress = table.Column<string>(type: "text", nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    ObjectId = table.Column<string>(type: "text", nullable: false),
                    ImgPath = table.Column<string>(type: "text", nullable: false),
                    ArmKey = table.Column<string>(type: "text", nullable: false),
                    Barrier = table.Column<string>(type: "text", nullable: false),
                    WorkingHours = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectedObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtectedObjects_Subdivisions_SubdivisionId",
                        column: x => x.SubdivisionId,
                        principalTable: "Subdivisions",
                        principalColumn: "Id");
                },
                comment: "Базовая таблица сущности ProtectedObject. Содержит общие поля аудита и статуса жизненного цикла.");

            migrationBuilder.CreateTable(
                name: "MaintenanceSectorObjects",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSectorObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSectorObjects_MaintenanceSectors_MaintenanceSect~",
                        column: x => x.MaintenanceSectorId,
                        principalSchema: "public",
                        principalTable: "MaintenanceSectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceSectorObjects_ProtectedObjects_ProtectedObjectId",
                        column: x => x.ProtectedObjectId,
                        principalSchema: "public",
                        principalTable: "ProtectedObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Таблица связи участков обслуживания и охраняемых объектов");

            migrationBuilder.CreateTable(
                name: "MaintenanceTasks",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceRoutineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutorPersonalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlannedEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TaskStatus = table.Column<int>(type: "integer", nullable: false),
                    CompletionNotes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
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
                name: "IX_MaintenanceSectorObjects_MaintenanceSectorId_ProtectedObjec~",
                schema: "public",
                table: "MaintenanceSectorObjects",
                columns: new[] { "MaintenanceSectorId", "ProtectedObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSectorObjects_ProtectedObjectId",
                schema: "public",
                table: "MaintenanceSectorObjects",
                column: "ProtectedObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSectors_ResponsiblePersonalId",
                schema: "public",
                table: "MaintenanceSectors",
                column: "ResponsiblePersonalId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSectors_SubdivisionId",
                schema: "public",
                table: "MaintenanceSectors",
                column: "SubdivisionId");

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

            migrationBuilder.CreateIndex(
                name: "IX_PersonalWorkSchedules_PersonalId_Date",
                schema: "public",
                table: "PersonalWorkSchedules",
                columns: new[] { "PersonalId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedObjects_SubdivisionId",
                schema: "public",
                table: "ProtectedObjects",
                column: "SubdivisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceSectorObjects",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceTasks",
                schema: "public");

            migrationBuilder.DropTable(
                name: "PersonalWorkSchedules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceRoutines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceSectors",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ProtectedObjects",
                schema: "public");
        }
    }
}
