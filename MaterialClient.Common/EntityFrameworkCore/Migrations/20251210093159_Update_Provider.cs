using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Update_Provider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaterialUpdateTime",
                table: "WorkSettings",
                newName: "ProviderUpdatedTime");

            migrationBuilder.RenameColumn(
                name: "ContactPhone",
                table: "Providers",
                newName: "UpdateDate");

            migrationBuilder.RenameColumn(
                name: "ContactName",
                table: "Providers",
                newName: "ProviderTypeName");

            migrationBuilder.AddColumn<DateTime>(
                name: "MaterialTypeUpdatedTime",
                table: "WorkSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MaterialUpdatedTime",
                table: "WorkSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProviderType",
                table: "Providers",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<DateTime>(
                name: "AddDate",
                table: "Providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddTime",
                table: "Providers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoId",
                table: "Providers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContectName",
                table: "Providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContectPhone",
                table: "Providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreateUserId",
                table: "Providers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Creator",
                table: "Providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "Providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "Providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Providers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LastEditUserId",
                table: "Providers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEditor",
                table: "Providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialTypeId",
                table: "Providers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdateTime",
                table: "Providers",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialTypeUpdatedTime",
                table: "WorkSettings");

            migrationBuilder.DropColumn(
                name: "MaterialUpdatedTime",
                table: "WorkSettings");

            migrationBuilder.DropColumn(
                name: "AddDate",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "AddTime",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "CoId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "ContectName",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "ContectPhone",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "CreateUserId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "Creator",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "LastEditUserId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "LastEditor",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "MaterialTypeId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "UpdateTime",
                table: "Providers");

            migrationBuilder.RenameColumn(
                name: "ProviderUpdatedTime",
                table: "WorkSettings",
                newName: "MaterialUpdateTime");

            migrationBuilder.RenameColumn(
                name: "UpdateDate",
                table: "Providers",
                newName: "ContactPhone");

            migrationBuilder.RenameColumn(
                name: "ProviderTypeName",
                table: "Providers",
                newName: "ContactName");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderType",
                table: "Providers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
