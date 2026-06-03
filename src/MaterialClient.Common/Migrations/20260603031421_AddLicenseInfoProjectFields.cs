using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseInfoProjectFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuildLicenseNo",
                table: "LicenseInfo",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FdBuildLicenseNo",
                table: "LicenseInfo",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProName",
                table: "LicenseInfo",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildLicenseNo",
                table: "LicenseInfo");

            migrationBuilder.DropColumn(
                name: "FdBuildLicenseNo",
                table: "LicenseInfo");

            migrationBuilder.DropColumn(
                name: "ProName",
                table: "LicenseInfo");
        }
    }
}
