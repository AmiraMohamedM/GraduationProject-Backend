using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grad.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionIdToNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                table: "Notifications",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Notifications");
        }
    }
}
