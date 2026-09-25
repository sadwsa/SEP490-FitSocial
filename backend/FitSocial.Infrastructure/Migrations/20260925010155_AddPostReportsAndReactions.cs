using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitSocial.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostReportsAndReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Reports_ReportedPostID\";");

            migrationBuilder.Sql("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"TokenVersion\" integer NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE \"Reports\" ADD COLUMN IF NOT EXISTS \"UpdatedAt\" timestamp without time zone;");
            migrationBuilder.Sql("ALTER TABLE \"Posts\" ADD COLUMN IF NOT EXISTS \"PostType\" character varying(50) NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE \"PostInteractions\" ADD COLUMN IF NOT EXISTS \"ReactionType\" character varying(50) NOT NULL DEFAULT 'Like';");
            migrationBuilder.Sql("ALTER TABLE \"PostInteractions\" ADD COLUMN IF NOT EXISTS \"UpdatedAt\" timestamp without time zone;");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Reports_ReportedPostId_ReporterId_Status\" ON \"Reports\" (\"ReportedPostID\", \"ReporterID\", \"Status\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_ReportedPostId_ReporterId_Status",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "PostType",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ReactionType",
                table: "PostInteractions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PostInteractions");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedPostID",
                table: "Reports",
                column: "ReportedPostID");
        }
    }
}
