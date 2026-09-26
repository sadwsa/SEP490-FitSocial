using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitSocial.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSportFromPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Posts_SportID_fkey",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_SportID",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "SportID",
                table: "Posts");

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PostType",
                table: "Posts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PostType",
                table: "Posts");

            migrationBuilder.AddColumn<Guid>(
                name: "SportID",
                table: "Posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Posts_SportID",
                table: "Posts",
                column: "SportID");

            migrationBuilder.AddForeignKey(
                name: "Posts_SportID_fkey",
                table: "Posts",
                column: "SportID",
                principalTable: "Sports",
                principalColumn: "SportID");
        }
    }
}
