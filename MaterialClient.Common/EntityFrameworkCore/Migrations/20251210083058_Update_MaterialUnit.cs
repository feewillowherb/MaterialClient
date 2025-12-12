using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Update_MaterialUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialUnits_Materials_MaterialId",
                table: "MaterialUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialUnits_Providers_ProviderId",
                table: "MaterialUnits");

            migrationBuilder.DropIndex(
                name: "IX_MaterialUnits_MaterialId",
                table: "MaterialUnits");

            migrationBuilder.DropIndex(
                name: "IX_MaterialUnits_ProviderId",
                table: "MaterialUnits");

            migrationBuilder.AddColumn<DateTime>(
                name: "AddDate",
                table: "MaterialUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddTime",
                table: "MaterialUnits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreateUserId",
                table: "MaterialUnits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Creator",
                table: "MaterialUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "MaterialUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "MaterialUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MaterialUnits",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LastEditUserId",
                table: "MaterialUnits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEditor",
                table: "MaterialUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitCalculationType",
                table: "MaterialUnits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateDate",
                table: "MaterialUnits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdateTime",
                table: "MaterialUnits",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddDate",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "AddTime",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "CreateUserId",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "Creator",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "LastEditUserId",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "LastEditor",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "UnitCalculationType",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "UpdateDate",
                table: "MaterialUnits");

            migrationBuilder.DropColumn(
                name: "UpdateTime",
                table: "MaterialUnits");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialUnits_MaterialId",
                table: "MaterialUnits",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialUnits_ProviderId",
                table: "MaterialUnits",
                column: "ProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialUnits_Materials_MaterialId",
                table: "MaterialUnits",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialUnits_Providers_ProviderId",
                table: "MaterialUnits",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
