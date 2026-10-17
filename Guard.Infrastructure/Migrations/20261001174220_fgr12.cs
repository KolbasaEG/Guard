using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class fgr12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceSectorMonthlySchedules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceSectorObjects",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceSectorObjectSchedules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceSectorWeeklyPatterns",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ProtectedObjects",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MaintenanceSectors",
                schema: "public");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "MaintenanceSectors",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    ResponsiblePersonalId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubdivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Дата создания участка"),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Описание участка"),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "Наименование участка обслуживания"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи.")
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
                name: "ProtectedObjects",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    SubdivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArmKey = table.Column<string>(type: "text", nullable: false),
                    Barrier = table.Column<string>(type: "text", nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    ImgPath = table.Column<string>(type: "text", nullable: false),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    LegalAddress = table.Column<string>(type: "text", nullable: false),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    ObjectId = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    WorkingHours = table.Column<string>(type: "text", nullable: false)
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
                name: "MaintenanceSectorMonthlySchedules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false, comment: "Календарная дата"),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    IsWorkDay = table.Column<bool>(type: "boolean", nullable: false, comment: "Рабочая смена участка"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Примечание к смене (праздник, сокращенный день)"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    WorkEnd = table.Column<TimeSpan>(type: "interval", nullable: true, comment: "Время окончания смены участка"),
                    WorkStart = table.Column<TimeSpan>(type: "interval", nullable: true, comment: "Время начала смены участка")
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
                name: "MaintenanceSectorWeeklyPatterns",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false, comment: "День недели (0 = Sunday, 1 = Monday...)"),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    IsWorkDay = table.Column<bool>(type: "boolean", nullable: false, comment: "Признак рабочего дня"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    WorkEnd = table.Column<TimeSpan>(type: "interval", nullable: true, comment: "Время окончания смены"),
                    WorkStart = table.Column<TimeSpan>(type: "interval", nullable: true, comment: "Время начала смены")
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

            migrationBuilder.CreateTable(
                name: "MaintenanceSectorObjects",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи.")
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
                comment: "Связь участков обслуживания и охраняемых объектов");

            migrationBuilder.CreateTable(
                name: "MaintenanceSectorObjectSchedules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    MaintenanceSectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false, comment: "Дата запланированного регламента"),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false, comment: "Отметка о фактическом выполнении"),
                    IsScheduled = table.Column<bool>(type: "boolean", nullable: false, comment: "Отметка планирования (крестик)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись"),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Вид регламента или примечание"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи.")
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

            migrationBuilder.CreateIndex(
                name: "UX_MaintenanceSectorMonthlySchedules_Sector_Date",
                schema: "public",
                table: "MaintenanceSectorMonthlySchedules",
                columns: new[] { "MaintenanceSectorId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSectorObjects_ProtectedObjectId",
                schema: "public",
                table: "MaintenanceSectorObjects",
                column: "ProtectedObjectId");

            migrationBuilder.CreateIndex(
                name: "UX_MaintenanceSectorObjects_Sector_Object",
                schema: "public",
                table: "MaintenanceSectorObjects",
                columns: new[] { "MaintenanceSectorId", "ProtectedObjectId" },
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
                name: "UX_MaintenanceSectorWeeklyPatterns_Sector_DayOfWeek",
                schema: "public",
                table: "MaintenanceSectorWeeklyPatterns",
                columns: new[] { "MaintenanceSectorId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedObjects_SubdivisionId",
                schema: "public",
                table: "ProtectedObjects",
                column: "SubdivisionId");
        }
    }
}
