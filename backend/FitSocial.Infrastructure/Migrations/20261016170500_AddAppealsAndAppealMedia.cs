using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitSocial.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppealsAndAppealMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Appeals",
                columns: table => new
                {
                    AppealID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ReportID = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealerID = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Appeals_pkey", x => x.AppealID);
                    table.ForeignKey(
                        name: "Appeals_AppealerID_fkey",
                        column: x => x.AppealerID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "Appeals_ReportID_fkey",
                        column: x => x.ReportID,
                        principalTable: "Reports",
                        principalColumn: "ReportID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Appeals_ReviewedBy_fkey",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AppealMedia",
                columns: table => new
                {
                    MediaID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    AppealID = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("AppealMedia_pkey", x => x.MediaID);
                    table.ForeignKey(
                        name: "AppealMedia_AppealID_fkey",
                        column: x => x.AppealID,
                        principalTable: "Appeals",
                        principalColumn: "AppealID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_AppealerID",
                table: "Appeals",
                column: "AppealerID");

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_ReportID",
                table: "Appeals",
                column: "ReportID");

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_ReviewedBy",
                table: "Appeals",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AppealMedia_AppealID",
                table: "AppealMedia",
                column: "AppealID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppealMedia");

            migrationBuilder.DropTable(
                name: "Appeals");
        }
    }
}
