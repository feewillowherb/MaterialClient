using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Update_WeighingRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCredentials_LicenseInfo_ProjectId",
                table: "UserCredentials");

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "WeighingRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "WeighingRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WaybillQuantity",
                table: "WeighingRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "WaybillQuantity",
                table: "WeighingRecords");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCredentials_LicenseInfo_ProjectId",
                table: "UserCredentials",
                column: "ProjectId",
                principalTable: "LicenseInfo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
