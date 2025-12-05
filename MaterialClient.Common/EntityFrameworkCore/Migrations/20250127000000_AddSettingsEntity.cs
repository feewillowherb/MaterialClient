using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScaleSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentScannerConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    SystemSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CameraConfigsJson = table.Column<string>(type: "TEXT", nullable: false),
                    LicensePlateRecognitionConfigsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Id",
                table: "Settings",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
