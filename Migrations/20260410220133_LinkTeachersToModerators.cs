using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace grad.Migrations
{
    /// <inheritdoc />
    public partial class LinkTeachersToModerators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ModeratorId",
                table: "teachers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_teachers_ModeratorId",
                table: "teachers",
                column: "ModeratorId");

            migrationBuilder.AddForeignKey(
                name: "FK_teachers_Users_ModeratorId",
                table: "teachers",
                column: "ModeratorId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teachers_Users_ModeratorId",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "IX_teachers_ModeratorId",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "ModeratorId",
                table: "teachers");
        }
    }
}
