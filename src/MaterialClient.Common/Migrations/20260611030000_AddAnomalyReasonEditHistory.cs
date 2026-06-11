using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddAnomalyReasonEditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnomalyReason",
                table: "UrbanWeighingExtensions",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditHistoryJson",
                table: "UrbanWeighingExtensions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnomalyReason",
                table: "UrbanWeighingExtensions");

            migrationBuilder.DropColumn(
                name: "EditHistoryJson",
                table: "UrbanWeighingExtensions");
        }
    }
}
