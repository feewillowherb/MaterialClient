using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Urban.Migrations;

public partial class AddUrbanPassageRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "UrbanPassageRecords" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UrbanPassageRecords" PRIMARY KEY,
                "PassageSource" INTEGER NOT NULL,
                "PlateNumber" TEXT NULL,
                "PlateColor" TEXT NULL,
                "VehicleType" TEXT NULL,
                "CapturedAt" TEXT NOT NULL,
                "UrbanInOutType" INTEGER NOT NULL,
                "UrbanSiteType" INTEGER NOT NULL,
                "LargeImageAttachmentId" INTEGER NULL,
                "SmallImageAttachmentId" INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_UrbanPassageRecords_CapturedAt" ON "UrbanPassageRecords" ("CapturedAt");
            CREATE INDEX IF NOT EXISTS "IX_UrbanPassageRecords_PassageSource" ON "UrbanPassageRecords" ("PassageSource");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "UrbanPassageRecords";""");
    }
}
