using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grad.Migrations
{
    /// <inheritdoc />
    public partial class AddFileFieldsToCourseSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AttachmentUrl",
                table: "CourseSessions",
                newName: "FileUrl");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "CourseSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "CourseSessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "CourseSessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                table: "CourseSessions");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "CourseSessions");

            migrationBuilder.DropColumn(
                name: "FileType",
                table: "CourseSessions");

            migrationBuilder.RenameColumn(
                name: "FileUrl",
                table: "CourseSessions",
                newName: "AttachmentUrl");
        }
    }
}
