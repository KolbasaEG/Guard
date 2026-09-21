using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Personnel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Уникальный идентификатор записи (UUIDv7)"),
                    SubdivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonnelCategoryTypeId = table.Column<long>(type: "bigint", nullable: true),
                    PersonnelCategoryCodeId = table.Column<long>(type: "bigint", nullable: true),
                    SpecialRankTypeId = table.Column<long>(type: "bigint", nullable: true),
                    SpecialRankCodeId = table.Column<long>(type: "bigint", nullable: true),
                    PositionTypeId = table.Column<long>(type: "bigint", nullable: true),
                    PositionCodeId = table.Column<long>(type: "bigint", nullable: true),
                    WorkerCategoryTypeId = table.Column<long>(type: "bigint", nullable: true),
                    WorkerCategoryCodeId = table.Column<long>(type: "bigint", nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Фамилия"),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Имя"),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Отчество"),
                    FullName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true, comment: "Фамилия и инициалы"),
                    EnlistmentYear = table.Column<int>(type: "integer", nullable: true, comment: "Год принятия на службу"),
                    PersonalNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "Личный номер"),
                    LastNameGen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Фамилия в родительном падеже"),
                    FirstNameGen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Имя в родительном падеже"),
                    MiddleNameGen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Отчество в родительном падеже"),
                    StatusTypeId = table.Column<long>(type: "bigint", nullable: true),
                    StatusCodeId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата последнего обновления информации"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Текущий статус жизненного цикла записи."),
                    InsertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'", comment: "Дата и время создания записи (UTC)"),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Дата и время последнего изменения записи (UTC)"),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false, comment: "Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id"),
                    ModifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true, comment: "Идентификатор пользователя (string), последним изменившего запись")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personnel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_PersonnelCategoryCodeId",
                        column: x => x.PersonnelCategoryCodeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_PersonnelCategoryTypeId",
                        column: x => x.PersonnelCategoryTypeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_PositionCodeId",
                        column: x => x.PositionCodeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_PositionTypeId",
                        column: x => x.PositionTypeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_SpecialRankCodeId",
                        column: x => x.SpecialRankCodeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_SpecialRankTypeId",
                        column: x => x.SpecialRankTypeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_StatusCodeId",
                        column: x => x.StatusCodeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_StatusTypeId",
                        column: x => x.StatusTypeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_WorkerCategoryCodeId",
                        column: x => x.WorkerCategoryCodeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Classifiers_WorkerCategoryTypeId",
                        column: x => x.WorkerCategoryTypeId,
                        principalTable: "Classifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Personnel_Subdivisions_SubdivisionId",
                        column: x => x.SubdivisionId,
                        principalTable: "Subdivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Справочник персонала системы Guard.");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_PersonnelCategoryCodeId",
                table: "Personnel",
                column: "PersonnelCategoryCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_PersonnelCategoryTypeId",
                table: "Personnel",
                column: "PersonnelCategoryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_PositionCodeId",
                table: "Personnel",
                column: "PositionCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_PositionTypeId",
                table: "Personnel",
                column: "PositionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_SpecialRankCodeId",
                table: "Personnel",
                column: "SpecialRankCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_SpecialRankTypeId",
                table: "Personnel",
                column: "SpecialRankTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_StatusCodeId",
                table: "Personnel",
                column: "StatusCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_StatusTypeId",
                table: "Personnel",
                column: "StatusTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_SubdivisionId",
                table: "Personnel",
                column: "SubdivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_WorkerCategoryCodeId",
                table: "Personnel",
                column: "WorkerCategoryCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_WorkerCategoryTypeId",
                table: "Personnel",
                column: "WorkerCategoryTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Personnel");
        }
    }
}
