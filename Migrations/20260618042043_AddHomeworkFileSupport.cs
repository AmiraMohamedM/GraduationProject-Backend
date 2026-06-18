using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grad.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeworkFileSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HomeworkUrl",
                table: "CourseSessions",
                newName: "HomeworkFileUrl");

            migrationBuilder.AddColumn<string>(
                name: "HomeworkFileName",
                table: "CourseSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HomeworkFileSize",
                table: "CourseSessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeworkFileType",
                table: "CourseSessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeworkFileName",
                table: "CourseSessions");

            migrationBuilder.DropColumn(
                name: "HomeworkFileSize",
                table: "CourseSessions");

            migrationBuilder.DropColumn(
                name: "HomeworkFileType",
                table: "CourseSessions");

            migrationBuilder.RenameColumn(
                name: "HomeworkFileUrl",
                table: "CourseSessions",
                newName: "HomeworkUrl");
        }
    }
}
