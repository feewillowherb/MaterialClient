using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Urban.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Urban.Migrations;

public partial class InitialUrbanProduct : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "UrbanWeighingExtensions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UrbanWeighingExtensions" PRIMARY KEY,
                "AnomalyReason" TEXT NULL,
                "ExtraProperties" TEXT NOT NULL,
                "IsAnomaly" INTEGER NOT NULL,
                "LastErrorTime" TEXT NULL,
                "RetryCount" INTEGER NOT NULL,
                "SubmitMachineCode" TEXT NULL,
                "SyncStatus" INTEGER NOT NULL,
                "WeighingRecordId" INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_UrbanWeighingExtensions_IsAnomaly" ON "UrbanWeighingExtensions" ("IsAnomaly");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UrbanWeighingExtensions_WeighingRecordId" ON "UrbanWeighingExtensions" ("WeighingRecordId");
            CREATE INDEX IF NOT EXISTS "IX_UrbanWeighingExtensions_SyncStatus_WeighingRecordId" ON "UrbanWeighingExtensions" ("SyncStatus", "WeighingRecordId");

            CREATE TABLE IF NOT EXISTS "UrbanSettingsRows" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UrbanSettingsRows" PRIMARY KEY AUTOINCREMENT,
                "SettingsJson" TEXT NOT NULL
            );

            INSERT INTO "UrbanSettingsRows" ("SettingsJson")
            SELECT COALESCE("UrbanSettingsJson", '') FROM "Settings"
            WHERE EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='Settings')
              AND NOT EXISTS (SELECT 1 FROM "UrbanSettingsRows")
            LIMIT 1;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
