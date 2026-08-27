using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddXiaoshanUploadConfigCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "XiaoshanUploadConfigCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ModesJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    IsAlignedWithServer = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XiaoshanUploadConfigCaches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XiaoshanUploadConfigCaches_ProjectId",
                table: "XiaoshanUploadConfigCaches",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XiaoshanUploadConfigCaches");
        }
    }
}
