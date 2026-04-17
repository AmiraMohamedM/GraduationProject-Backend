using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grad.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLessonProgressModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LessonId",
                table: "lesson_progress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_progress_CourseSessionId",
                table: "lesson_progress",
                column: "CourseSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_lesson_progress_CourseSessions_CourseSessionId",
                table: "lesson_progress",
                column: "CourseSessionId",
                principalTable: "CourseSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lesson_progress_CourseSessions_CourseSessionId",
                table: "lesson_progress");

            migrationBuilder.DropIndex(
                name: "IX_lesson_progress_CourseSessionId",
                table: "lesson_progress");

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "lesson_progress");
        }
    }
}
