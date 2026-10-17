using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitSocial.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageTypeToTrainingPackageAndOrderDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                table: "TrainingPackages",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                defaultValue: "BOTH");

            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                table: "OrderDetails",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                defaultValue: "BOTH");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackageType",
                table: "TrainingPackages");

            migrationBuilder.DropColumn(
                name: "PackageType",
                table: "OrderDetails");
        }
    }
}
