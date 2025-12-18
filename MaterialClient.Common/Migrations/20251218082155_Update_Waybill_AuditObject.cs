using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class Update_Waybill_AuditObject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreationTime",
                table: "Waybills");

            migrationBuilder.RenameColumn(
                name: "LastModifierId",
                table: "Waybills",
                newName: "UpdateDate");

            migrationBuilder.RenameColumn(
                name: "LastModificationTime",
                table: "Waybills",
                newName: "LastEditor");

            migrationBuilder.RenameColumn(
                name: "CreatorId",
                table: "Waybills",
                newName: "Creator");

            migrationBuilder.AddColumn<DateTime>(
                name: "AddDate",
                table: "Waybills",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddTime",
                table: "Waybills",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreateUserId",
                table: "Waybills",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastEditUserId",
                table: "Waybills",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdateTime",
                table: "Waybills",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddDate",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "AddTime",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "CreateUserId",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "LastEditUserId",
                table: "Waybills");

            migrationBuilder.DropColumn(
                name: "UpdateTime",
                table: "Waybills");

            migrationBuilder.RenameColumn(
                name: "UpdateDate",
                table: "Waybills",
                newName: "LastModifierId");

            migrationBuilder.RenameColumn(
                name: "LastEditor",
                table: "Waybills",
                newName: "LastModificationTime");

            migrationBuilder.RenameColumn(
                name: "Creator",
                table: "Waybills",
                newName: "CreatorId");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreationTime",
                table: "Waybills",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
