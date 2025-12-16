using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Update_MaterialId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "OrderPlanOnWeight",
                table: "Waybills",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "OrderPlanOnPcs",
                table: "Waybills",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "OrderPcs",
                table: "Waybills",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "Waybills",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialUnitId",
                table: "Waybills",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialUnitRate",
                table: "Waybills",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OffsetRate",
                table: "Waybills",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WaybillMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WaybillId = table.Column<long>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", nullable: false),
                    Specifications = table.Column<string>(type: "TEXT", nullable: true),
                    MaterialUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    GoodsPlanOnWeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    GoodsPlanOnPcs = table.Column<decimal>(type: "TEXT", nullable: false),
                    GoodsPcs = table.Column<decimal>(type: "TEXT", nullable: false),
                    GoodsWeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    GoodsTakeWeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    OffsetResult = table.Column<short>(type: "INTEGER", nullable: false),
                    OffsetWeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    OffsetCount = table.Column<decimal>(type: "TEXT", nullable: false),
                    OffsetRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    LastEditUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastEditor = table.Column<string>(type: "TEXT", nullable: true),
                    CreateUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Creator = table.Column<string>(type: "TEXT", nullable: true),
                    UpdateTime = table.Column<int>(type: "INTEGER", nullable: true),
                    AddTime = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AddDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaybillMaterials", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaybillMaterials");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "MaterialUnitId",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "MaterialUnitRate",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "OffsetRate",
                table: "Waybills");

            migrationBuilder.AlterColumn<decimal>(
                name: "OrderPlanOnWeight",
                table: "Waybills",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OrderPlanOnPcs",
                table: "Waybills",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OrderPcs",
                table: "Waybills",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
