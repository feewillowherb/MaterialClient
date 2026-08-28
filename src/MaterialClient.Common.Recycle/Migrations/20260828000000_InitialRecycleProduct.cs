using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Recycle.Migrations;

public partial class InitialRecycleProduct : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "RecycleWaybillExtensions" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RecycleWaybillExtensions" PRIMARY KEY,
                "WaybillId" INTEGER NOT NULL,
                "UnitPrice" TEXT NULL,
                "SaleContractNo" TEXT NULL,
                "ReceivingTime" TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecycleWaybillExtensions_WaybillId"
                ON "RecycleWaybillExtensions" ("WaybillId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
