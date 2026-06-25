using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddUrbanExtensionAndLicenseAccessCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthToken",
                table: "LicenseInfo");

            migrationBuilder.AddColumn<string>(
                name: "PlateColor",
                table: "WeighingRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleColor",
                table: "WeighingRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "WeighingRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessCode",
                table: "LicenseInfo",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestJwtToken",
                table: "LicenseInfo",
                type: "TEXT",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProName",
                table: "LicenseInfo",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UrbanWeighingExtensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    WeighingRecordId = table.Column<long>(type: "INTEGER", nullable: false),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastErrorTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsAnomaly = table.Column<bool>(type: "INTEGER", nullable: false),
                    AnomalyReason = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrbanWeighingExtensions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UrbanWeighingExtensions_IsAnomaly",
                table: "UrbanWeighingExtensions",
                column: "IsAnomaly");

            migrationBuilder.CreateIndex(
                name: "IX_UrbanWeighingExtensions_SyncStatus_WeighingRecordId",
                table: "UrbanWeighingExtensions",
                columns: new[] { "SyncStatus", "WeighingRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_UrbanWeighingExtensions_WeighingRecordId",
                table: "UrbanWeighingExtensions",
                column: "WeighingRecordId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UrbanWeighingExtensions");

            migrationBuilder.DropColumn(
                name: "PlateColor",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "VehicleColor",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "AccessCode",
                table: "LicenseInfo");

            migrationBuilder.DropColumn(
                name: "LatestJwtToken",
                table: "LicenseInfo");

            migrationBuilder.DropColumn(
                name: "ProName",
                table: "LicenseInfo");

            migrationBuilder.AddColumn<Guid>(
                name: "AuthToken",
                table: "LicenseInfo",
                type: "TEXT",
                nullable: true);
        }
    }
}
