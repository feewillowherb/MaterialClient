using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Add_MaterialType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Settings_Id",
                table: "Settings");

            migrationBuilder.CreateTable(
                name: "MaterialTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TypeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TypeCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CoId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpperLimit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LowerLimit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ProId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastEditUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastEditor = table.Column<string>(type: "TEXT", nullable: true),
                    CreateUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Creator = table.Column<string>(type: "TEXT", nullable: true),
                    UpdateTime = table.Column<int>(type: "INTEGER", nullable: true),
                    AddTime = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AddDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTypes_CoId",
                table: "MaterialTypes",
                column: "CoId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTypes_IsDeleted",
                table: "MaterialTypes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTypes_ParentId",
                table: "MaterialTypes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialTypes_TypeCode",
                table: "MaterialTypes",
                column: "TypeCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialTypes");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Id",
                table: "Settings",
                column: "Id",
                unique: true);
        }
    }
}
