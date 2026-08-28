using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSolidWasteMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraProperties",
                table: "WeighingRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WeighingMode",
                table: "WeighingRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExtraProperties",
                table: "Waybills",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WeighingMode",
                table: "Waybills",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WeighingRecords_WeighingMode",
                table: "WeighingRecords",
                column: "WeighingMode");

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_WeighingMode",
                table: "Waybills",
                column: "WeighingMode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeighingRecords_WeighingMode",
                table: "WeighingRecords");

            migrationBuilder.DropIndex(
                name: "IX_Waybills_WeighingMode",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "ExtraProperties",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "WeighingMode",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "ExtraProperties",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "WeighingMode",
                table: "Waybills");
        }
    }
}
