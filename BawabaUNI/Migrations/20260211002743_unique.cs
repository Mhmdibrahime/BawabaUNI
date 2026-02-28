using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BawabaUNI.Migrations
{
    /// <inheritdoc />
    public partial class unique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Universities_NameEnglish",
                table: "Universities");

            migrationBuilder.DropIndex(
                name: "IX_StudyPlanSections_StudyPlanYearId_Name",
                table: "StudyPlanSections");

            migrationBuilder.DropIndex(
                name: "IX_Faculties_NameEnglish_UniversityId",
                table: "Faculties");

            migrationBuilder.DropIndex(
                name: "IX_Courses_NameEnglish",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_AcademicMaterials_Code",
                table: "AcademicMaterials");

            migrationBuilder.CreateIndex(
                name: "IX_Universities_NameEnglish",
                table: "Universities",
                column: "NameEnglish");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanSections_StudyPlanYearId_Name",
                table: "StudyPlanSections",
                columns: new[] { "StudyPlanYearId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Faculties_NameEnglish_UniversityId",
                table: "Faculties",
                columns: new[] { "NameEnglish", "UniversityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_NameEnglish",
                table: "Courses",
                column: "NameEnglish");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicMaterials_Code",
                table: "AcademicMaterials",
                column: "Code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Universities_NameEnglish",
                table: "Universities");

            migrationBuilder.DropIndex(
                name: "IX_StudyPlanSections_StudyPlanYearId_Name",
                table: "StudyPlanSections");

            migrationBuilder.DropIndex(
                name: "IX_Faculties_NameEnglish_UniversityId",
                table: "Faculties");

            migrationBuilder.DropIndex(
                name: "IX_Courses_NameEnglish",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_AcademicMaterials_Code",
                table: "AcademicMaterials");

            migrationBuilder.CreateIndex(
                name: "IX_Universities_NameEnglish",
                table: "Universities",
                column: "NameEnglish",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlanSections_StudyPlanYearId_Name",
                table: "StudyPlanSections",
                columns: new[] { "StudyPlanYearId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faculties_NameEnglish_UniversityId",
                table: "Faculties",
                columns: new[] { "NameEnglish", "UniversityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_NameEnglish",
                table: "Courses",
                column: "NameEnglish",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicMaterials_Code",
                table: "AcademicMaterials",
                column: "Code",
                unique: true);
        }
    }
}
