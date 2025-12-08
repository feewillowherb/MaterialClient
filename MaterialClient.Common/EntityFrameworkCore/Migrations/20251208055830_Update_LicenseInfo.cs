using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Update_LicenseInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_LicenseInfo_ProjectId",
                table: "UserSessions");

            migrationBuilder.AddColumn<Guid>(
                name: "LicenseInfoId",
                table: "UserSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LicenseInfoId",
                table: "UserCredentials",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenseInfoId",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "LicenseInfoId",
                table: "UserCredentials");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_LicenseInfo_ProjectId",
                table: "UserSessions",
                column: "ProjectId",
                principalTable: "LicenseInfo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
