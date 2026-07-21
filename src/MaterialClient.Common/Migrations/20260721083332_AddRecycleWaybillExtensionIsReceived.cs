using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddRecycleWaybillExtensionIsReceived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReceived",
                table: "RecycleWaybillExtensions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // 历史数据：已有收货时间的视为已提交收货
            migrationBuilder.Sql(
                """
                UPDATE RecycleWaybillExtensions
                SET IsReceived = 1
                WHERE ReceivingTime IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReceived",
                table: "RecycleWaybillExtensions");
        }
    }
}
