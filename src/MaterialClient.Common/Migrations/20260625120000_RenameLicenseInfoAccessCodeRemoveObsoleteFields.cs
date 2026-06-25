using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class RenameLicenseInfoAccessCodeRemoveObsoleteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BuildLicenseNo",
                table: "LicenseInfo",
                newName: "AccessCode");

            migrationBuilder.DropColumn(
                name: "AuthToken",
                table: "LicenseInfo");

            migrationBuilder.DropColumn(
                name: "FdBuildLicenseNo",
                table: "LicenseInfo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccessCode",
                table: "LicenseInfo",
                newName: "BuildLicenseNo");

            migrationBuilder.AddColumn<Guid>(
                name: "AuthToken",
                table: "LicenseInfo",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FdBuildLicenseNo",
                table: "LicenseInfo",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }
    }
}
