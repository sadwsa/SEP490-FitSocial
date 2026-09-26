using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitSocial.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReactionTypeFromPostInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"PostInteractions\" DROP COLUMN IF EXISTS \"ReactionType\";");
            migrationBuilder.Sql("ALTER TABLE \"PostInteractions\" DROP COLUMN IF EXISTS \"UpdatedAt\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReactionType",
                table: "PostInteractions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Like");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PostInteractions",
                type: "timestamp without time zone",
                nullable: true);
        }
    }
}
