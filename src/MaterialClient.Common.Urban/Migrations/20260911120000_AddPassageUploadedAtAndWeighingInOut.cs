using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Urban.Migrations;

public partial class AddPassageUploadedAtAndWeighingInOut : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UrbanPassageRecords" ADD COLUMN "UploadedAt" TEXT NULL;
            ALTER TABLE "UrbanWeighingExtensions" ADD COLUMN "UrbanInOutType" INTEGER NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UrbanPassageRecords" DROP COLUMN "UploadedAt";
            ALTER TABLE "UrbanWeighingExtensions" DROP COLUMN "UrbanInOutType";
            """);
    }
}
