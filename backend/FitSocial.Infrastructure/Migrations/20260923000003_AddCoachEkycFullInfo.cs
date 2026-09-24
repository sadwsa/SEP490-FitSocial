using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitSocial.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachEkycFullInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Birthplace", table: "CoachEkycVerifications", type: "character varying(255)", maxLength: 255, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Sex", table: "CoachEkycVerifications", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Address", table: "CoachEkycVerifications", type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Province", table: "CoachEkycVerifications", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "District", table: "CoachEkycVerifications", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Ward", table: "CoachEkycVerifications", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ProvinceCode", table: "CoachEkycVerifications", type: "character varying(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<string>(name: "DistrictCode", table: "CoachEkycVerifications", type: "character varying(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<string>(name: "WardCode", table: "CoachEkycVerifications", type: "character varying(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Street", table: "CoachEkycVerifications", type: "character varying(255)", maxLength: 255, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Nationality", table: "CoachEkycVerifications", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Religion", table: "CoachEkycVerifications", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Ethnicity", table: "CoachEkycVerifications", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Expiry", table: "CoachEkycVerifications", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Feature", table: "CoachEkycVerifications", type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(name: "IssueDate", table: "CoachEkycVerifications", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "IssueBy", table: "CoachEkycVerifications", type: "character varying(255)", maxLength: 255, nullable: true);
            migrationBuilder.AddColumn<string>(name: "DocumentType", table: "CoachEkycVerifications", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(name: "RawInformationJson", table: "CoachEkycVerifications", type: "jsonb", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Birthplace", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Sex", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Address", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Province", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "District", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Ward", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "ProvinceCode", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "DistrictCode", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "WardCode", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Street", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Nationality", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Religion", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Ethnicity", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Expiry", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "Feature", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "IssueDate", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "IssueBy", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "DocumentType", table: "CoachEkycVerifications");
            migrationBuilder.DropColumn(name: "RawInformationJson", table: "CoachEkycVerifications");
        }
    }
}
