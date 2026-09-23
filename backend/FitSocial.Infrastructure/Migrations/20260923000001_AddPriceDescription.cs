using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitSocial.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Price",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Price");
        }
    }
}
