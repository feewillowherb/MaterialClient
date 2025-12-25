using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class Update_MaterialDecimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "OffsetRate",
                table: "Waybills",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OffsetCount",
                table: "Waybills",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncTime",
                table: "AttachmentFiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OffsetCount",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "LastSyncTime",
                table: "AttachmentFiles");

            migrationBuilder.AlterColumn<decimal>(
                name: "OffsetRate",
                table: "Waybills",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT");
        }
    }
}
