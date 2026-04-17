using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grad.Migrations
{
    /// <inheritdoc />
    public partial class UpdateActivityLogToUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activity_logs_Users_admin_id",
                table: "activity_logs");

            migrationBuilder.RenameColumn(
                name: "admin_id",
                table: "activity_logs",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_activity_logs_admin_id",
                table: "activity_logs",
                newName: "IX_activity_logs_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_activity_logs_Users_UserId",
                table: "activity_logs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_activity_logs_Users_UserId",
                table: "activity_logs");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "activity_logs",
                newName: "admin_id");

            migrationBuilder.RenameIndex(
                name: "IX_activity_logs_UserId",
                table: "activity_logs",
                newName: "IX_activity_logs_admin_id");

            migrationBuilder.AddForeignKey(
                name: "FK_activity_logs_Users_admin_id",
                table: "activity_logs",
                column: "admin_id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
