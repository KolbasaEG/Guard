using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class adng1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonalWorkSchedules",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "MaintenanceSectorMonthlySchedules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false, comment: "Календарная дата"),
                    IsWorkDay = table.Column<bool>(type: "boolean", nullable: false, comment: "Рабочая смена участка"),
                    WorkStart = table.Column<TimeSpan>(type: "interval", nullable: true),
                    WorkEnd = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Примечание к смене (праздник, сокращенный день)"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSectorMonthlySchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSectorMonthlySchedules_MaintenanceSectors_Mainte~",
                        column: x => x.MaintenanceSectorId,
                        principalSchema: "public",
                        principalTable: "MaintenanceSectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Календарный график работы участка на месяц");

            migrationBuilder.CreateTable(
                name: "MaintenanceSectorObjectSchedules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false, comment: "Дата запланированного регламента"),
                    IsScheduled = table.Column<bool>(type: "boolean", nullable: false, comment: "Отметка планирования (крестик)"),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Отметка о фактическом выполнении"),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Вид регламента или примечание"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSectorObjectSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSectorObjectSchedules_MaintenanceSectors_Mainten~",
                        column: x => x.MaintenanceSectorId,
                        principalSchema: "public",
                        principalTable: "MaintenanceSectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceSectorObjectSchedules_ProtectedObjects_Protected~",
                        column: x => x.ProtectedObjectId,
                        principalSchema: "public",
                        principalTable: "ProtectedObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "График обслуживания объектов участка (крестики по датам)");

            migrationBuilder.CreateTable(
                name: "MaintenanceSectorWeeklyPatterns",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false, comment: "День недели (0 = Sunday, 1 = Monday...)"),
                    IsWorkDay = table.Column<bool>(type: "boolean", nullable: false, comment: "Признак рабочего дня"),
                    WorkStart = table.Column<TimeSpan>(type: "interval", nullable: true, comment: "Время начала смены"),
                    WorkEnd = table.Column<TimeSpan>(type: "interval", nullable: true, comment: "Время окончания смены"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSectorWeeklyPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSectorWeeklyPatterns_MaintenanceSectors_Maintena~",
                        column: x => x.MaintenanceSectorId,
                        principalSchema: "public",
                        principalTable: "MaintenanceSectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Шаблон рабочей недели участка обслуживания (7 дней)");

            migrationBuilder.CreateIndex(
                name: "UX_MaintenanceSectorMonthlySchedules_Sector_Date",
                schema: "public",
                table: "MaintenanceSectorMonthlySchedules",
                columns: new[] { "MaintenanceSectorId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSectorObjectSchedules_ProtectedObjectId",
                schema: "public",
                table: "MaintenanceSectorObjectSchedules",
                column: "ProtectedObjectId");

            migrationBuilder.CreateIndex(
                name: "UX_MaintenanceSectorObjectSchedules_Sector_Object_Date",
                schema: "public",
                table: "MaintenanceSectorObjectSchedules",
                columns: new[] { "MaintenanceSectorId", "ProtectedObjectId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MaintenanceSectorWeeklyPatterns_Sector_DayOfWeek",
                schema: "public",
                table: "MaintenanceSectorWeeklyPatterns",
                columns: new[] { "MaintenanceSectorId", "DayOfWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceSectorMonthlySchedules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceSectorObjectSchedules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceSectorWeeklyPatterns",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "PersonalWorkSchedules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    PersonalId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    IsDayOff = table.Column<bool>(type: "boolean", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    ShiftEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ShiftStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи.")
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

            migrationBuilder.CreateIndex(
                name: "UX_PersonalWorkSchedules_Personal_Date",
                schema: "public",
                table: "PersonalWorkSchedules",
                columns: new[] { "PersonalId", "Date" },
                unique: true);
        }
    }
}
