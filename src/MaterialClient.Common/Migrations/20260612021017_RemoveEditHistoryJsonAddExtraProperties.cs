using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEditHistoryJsonAddExtraProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditHistoryJson",
                table: "UrbanWeighingExtensions");

            migrationBuilder.AddColumn<string>(
                name: "ExtraProperties",
                table: "UrbanWeighingExtensions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraProperties",
                table: "UrbanWeighingExtensions");

            migrationBuilder.AddColumn<string>(
                name: "EditHistoryJson",
                table: "UrbanWeighingExtensions",
                type: "TEXT",
                nullable: true);
        }
    }
}
