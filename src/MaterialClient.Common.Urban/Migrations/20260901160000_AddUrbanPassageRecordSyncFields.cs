using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Urban.Migrations;

public partial class AddUrbanPassageRecordSyncFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UrbanPassageRecords" ADD COLUMN "SyncStatus" INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE "UrbanPassageRecords" ADD COLUMN "RetryCount" INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE "UrbanPassageRecords" ADD COLUMN "LastErrorTime" TEXT NULL;
            ALTER TABLE "UrbanPassageRecords" ADD COLUMN "SubmitMachineCode" TEXT NULL;
            CREATE INDEX IF NOT EXISTS "IX_UrbanPassageRecords_SyncStatus" ON "UrbanPassageRecords" ("SyncStatus");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_UrbanPassageRecords_SyncStatus";
            ALTER TABLE "UrbanPassageRecords" DROP COLUMN "SubmitMachineCode";
            ALTER TABLE "UrbanPassageRecords" DROP COLUMN "LastErrorTime";
            ALTER TABLE "UrbanPassageRecords" DROP COLUMN "RetryCount";
            ALTER TABLE "UrbanPassageRecords" DROP COLUMN "SyncStatus";
            """);
    }
}
