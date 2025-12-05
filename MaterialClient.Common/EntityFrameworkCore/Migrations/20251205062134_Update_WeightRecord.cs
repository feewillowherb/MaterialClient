using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Update_WeightRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WeighingRecords_Materials_MaterialId",
                table: "WeighingRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_WeighingRecords_Providers_ProviderId",
                table: "WeighingRecords");

            migrationBuilder.DropIndex(
                name: "IX_WeighingRecords_MaterialId",
                table: "WeighingRecords");

            migrationBuilder.DropIndex(
                name: "IX_WeighingRecords_ProviderId",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "RecordType",
                table: "WeighingRecords");

            migrationBuilder.RenameColumn(
                name: "ProviderId",
                table: "WeighingRecords",
                newName: "MatchedType");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                table: "WeighingRecords",
                newName: "MatchedId");

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

            migrationBuilder.RenameColumn(
                name: "MatchedType",
                table: "WeighingRecords",
                newName: "ProviderId");

            migrationBuilder.RenameColumn(
                name: "MatchedId",
                table: "WeighingRecords",
                newName: "MaterialId");

            migrationBuilder.AddColumn<int>(
                name: "RecordType",
                table: "WeighingRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WeighingRecords_MaterialId",
                table: "WeighingRecords",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingRecords_ProviderId",
                table: "WeighingRecords",
                column: "ProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_WeighingRecords_Materials_MaterialId",
                table: "WeighingRecords",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WeighingRecords_Providers_ProviderId",
                table: "WeighingRecords",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
