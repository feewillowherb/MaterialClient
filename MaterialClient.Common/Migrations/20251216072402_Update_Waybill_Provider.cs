using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class Update_Waybill_Provider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Waybills_Providers_ProviderId",
                table: "Waybills");

            migrationBuilder.DropIndex(
                name: "IX_Waybills_ProviderId",
                table: "Waybills");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderId",
                table: "Waybills",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ProviderId",
                table: "Waybills",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_ProviderId",
                table: "Waybills",
                column: "ProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Waybills_Providers_ProviderId",
                table: "Waybills",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
