using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class RecycleEnhancementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecycleWaybillExtensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WaybillId = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    SaleContractNo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReceivingTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecycleWaybillExtensions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecycleWaybillExtensions_WaybillId",
                table: "RecycleWaybillExtensions",
                column: "WaybillId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecycleWaybillExtensions");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Providers");
        }
    }
}
