using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grad.Migrations
{
    /// <inheritdoc />
    public partial class ChangeStudentLevelToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_students_AcademicLevels_academic_level_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "FK_students_ClassLevels_class_level_id",
                table: "students");

            migrationBuilder.DropIndex(
                name: "IX_students_academic_level_id",
                table: "students");

            migrationBuilder.DropIndex(
                name: "IX_students_class_level_id",
                table: "students");

            migrationBuilder.DropColumn(
                name: "academic_level_id",
                table: "students");

            migrationBuilder.DropColumn(
                name: "class_level_id",
                table: "students");

            migrationBuilder.AddColumn<string>(
                name: "AcademicLevel",
                table: "students",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AcademicYear",
                table: "students",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicLevel",
                table: "students");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "students");

            migrationBuilder.AddColumn<int>(
                name: "academic_level_id",
                table: "students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "class_level_id",
                table: "students",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_students_academic_level_id",
                table: "students",
                column: "academic_level_id");

            migrationBuilder.CreateIndex(
                name: "IX_students_class_level_id",
                table: "students",
                column: "class_level_id");

            migrationBuilder.AddForeignKey(
                name: "FK_students_AcademicLevels_academic_level_id",
                table: "students",
                column: "academic_level_id",
                principalTable: "AcademicLevels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_students_ClassLevels_class_level_id",
                table: "students",
                column: "class_level_id",
                principalTable: "ClassLevels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
