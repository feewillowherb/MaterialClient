using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class Add_UrbanWeighingExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UrbanWeighingExtensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeighingRecordId = table.Column<long>(type: "INTEGER", nullable: false),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastErrorTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsAnomaly = table.Column<bool>(type: "INTEGER", nullable: false)
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
        }
    }
}
