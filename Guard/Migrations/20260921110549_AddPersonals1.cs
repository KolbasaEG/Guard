using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonals1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_PersonnelCategoryCodeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_PersonnelCategoryTypeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_PositionCodeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_PositionTypeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_SpecialRankCodeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_SpecialRankTypeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_StatusCodeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_StatusTypeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_WorkerCategoryCodeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Classifiers_WorkerCategoryTypeId",
                table: "Personnel");

            migrationBuilder.DropForeignKey(
                name: "FK_Personnel_Subdivisions_SubdivisionId",
                table: "Personnel");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Personnel",
                table: "Personnel");

            migrationBuilder.RenameTable(
                name: "Personnel",
                newName: "Personals");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_WorkerCategoryTypeId",
                table: "Personals",
                newName: "IX_Personals_WorkerCategoryTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_WorkerCategoryCodeId",
                table: "Personals",
                newName: "IX_Personals_WorkerCategoryCodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_SubdivisionId",
                table: "Personals",
                newName: "IX_Personals_SubdivisionId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_StatusTypeId",
                table: "Personals",
                newName: "IX_Personals_StatusTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_StatusCodeId",
                table: "Personals",
                newName: "IX_Personals_StatusCodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_SpecialRankTypeId",
                table: "Personals",
                newName: "IX_Personals_SpecialRankTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_SpecialRankCodeId",
                table: "Personals",
                newName: "IX_Personals_SpecialRankCodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_PositionTypeId",
                table: "Personals",
                newName: "IX_Personals_PositionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_PositionCodeId",
                table: "Personals",
                newName: "IX_Personals_PositionCodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_PersonnelCategoryTypeId",
                table: "Personals",
                newName: "IX_Personals_PersonnelCategoryTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personnel_PersonnelCategoryCodeId",
                table: "Personals",
                newName: "IX_Personals_PersonnelCategoryCodeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Personals",
                table: "Personals",
                column: "Id");

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
                name: "FK_Personals_Subdivisions_SubdivisionId",
                table: "Personals",
                column: "SubdivisionId",
                principalTable: "Subdivisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "FK_Personals_Subdivisions_SubdivisionId",
                table: "Personals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Personals",
                table: "Personals");

            migrationBuilder.RenameTable(
                name: "Personals",
                newName: "Personnel");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_WorkerCategoryTypeId",
                table: "Personnel",
                newName: "IX_Personnel_WorkerCategoryTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_WorkerCategoryCodeId",
                table: "Personnel",
                newName: "IX_Personnel_WorkerCategoryCodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_SubdivisionId",
                table: "Personnel",
                newName: "IX_Personnel_SubdivisionId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_StatusTypeId",
                table: "Personnel",
                newName: "IX_Personnel_StatusTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_StatusCodeId",
                table: "Personnel",
                newName: "IX_Personnel_StatusCodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_SpecialRankTypeId",
                table: "Personnel",
                newName: "IX_Personnel_SpecialRankTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_SpecialRankCodeId",
                table: "Personnel",
                newName: "IX_Personnel_SpecialRankCodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_PositionTypeId",
                table: "Personnel",
                newName: "IX_Personnel_PositionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_PositionCodeId",
                table: "Personnel",
                newName: "IX_Personnel_PositionCodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_PersonnelCategoryTypeId",
                table: "Personnel",
                newName: "IX_Personnel_PersonnelCategoryTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Personals_PersonnelCategoryCodeId",
                table: "Personnel",
                newName: "IX_Personnel_PersonnelCategoryCodeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Personnel",
                table: "Personnel",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_PersonnelCategoryCodeId",
                table: "Personnel",
                column: "PersonnelCategoryCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_PersonnelCategoryTypeId",
                table: "Personnel",
                column: "PersonnelCategoryTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_PositionCodeId",
                table: "Personnel",
                column: "PositionCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_PositionTypeId",
                table: "Personnel",
                column: "PositionTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_SpecialRankCodeId",
                table: "Personnel",
                column: "SpecialRankCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_SpecialRankTypeId",
                table: "Personnel",
                column: "SpecialRankTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_StatusCodeId",
                table: "Personnel",
                column: "StatusCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_StatusTypeId",
                table: "Personnel",
                column: "StatusTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_WorkerCategoryCodeId",
                table: "Personnel",
                column: "WorkerCategoryCodeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Classifiers_WorkerCategoryTypeId",
                table: "Personnel",
                column: "WorkerCategoryTypeId",
                principalTable: "Classifiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Personnel_Subdivisions_SubdivisionId",
                table: "Personnel",
                column: "SubdivisionId",
                principalTable: "Subdivisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
