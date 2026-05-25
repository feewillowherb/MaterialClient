using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddUrbanWeighingExtension : Migration
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
                    LastErrorTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrbanWeighingExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UrbanWeighingExtensions_WeighingRecords_WeighingRecordId",
                        column: x => x.WeighingRecordId,
                        principalTable: "WeighingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UrbanWeighingExtensions_SyncStatus_WeighingRecordId",
                table: "UrbanWeighingExtensions",
                columns: new[] { "SyncStatus", "WeighingRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_UrbanWeighingExtensions_WeighingRecordId",
                table: "UrbanWeighingExtensions",
                column: "WeighingRecordId",
                unique: true);

            // Data migration: copy SyncStatus from WeighingRecords for Urban mode records (WeighingMode = 201)
            migrationBuilder.Sql(
                @"INSERT INTO UrbanWeighingExtensions (Id, WeighingRecordId, SyncStatus, RetryCount, LastErrorTime)
                  SELECT lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || '4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6))), Id, SyncStatus, 0, NULL
                  FROM WeighingRecords
                  WHERE WeighingMode = 201
                    AND NOT EXISTS (
                      SELECT 1 FROM UrbanWeighingExtensions ue WHERE ue.WeighingRecordId = WeighingRecords.Id
                    )");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UrbanWeighingExtensions");
        }
    }
}
