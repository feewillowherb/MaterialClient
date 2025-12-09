using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Add_WorkSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AddDate",
                table: "Materials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddTime",
                table: "Materials",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreateUserId",
                table: "Materials",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Creator",
                table: "Materials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "Materials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "Materials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Materials",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LastEditUserId",
                table: "Materials",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEditor",
                table: "Materials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateDate",
                table: "Materials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdateTime",
                table: "Materials",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaterialUpdateTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkSettings");

            migrationBuilder.DropColumn(
                name: "AddDate",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "AddTime",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "CreateUserId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Creator",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "LastEditUserId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "LastEditor",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "UpdateDate",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "UpdateTime",
                table: "Materials");
        }
    }
}
