using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.Migrations
{
    /// <inheritdoc />
    public partial class Update_WeighingRecord_Materials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WaybillAttachments_AttachmentFiles_AttachmentFileId",
                table: "WaybillAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_WaybillAttachments_Waybills_WaybillId",
                table: "WaybillAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_WeighingRecordAttachments_AttachmentFiles_AttachmentFileId",
                table: "WeighingRecordAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_WeighingRecordAttachments_WeighingRecords_WeighingRecordId",
                table: "WeighingRecordAttachments");

            migrationBuilder.DropIndex(
                name: "IX_WeighingRecordAttachments_AttachmentFileId",
                table: "WeighingRecordAttachments");

            migrationBuilder.DropIndex(
                name: "IX_WaybillAttachments_AttachmentFileId",
                table: "WaybillAttachments");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "WeighingRecords");

            migrationBuilder.DropColumn(
                name: "MaterialUnitId",
                table: "WeighingRecords");

            migrationBuilder.RenameColumn(
                name: "Weight",
                table: "WeighingRecords",
                newName: "TotalWeight");

            migrationBuilder.RenameColumn(
                name: "WaybillQuantity",
                table: "WeighingRecords",
                newName: "MaterialsJson");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalWeight",
                table: "WeighingRecords",
                newName: "Weight");

            migrationBuilder.RenameColumn(
                name: "MaterialsJson",
                table: "WeighingRecords",
                newName: "WaybillQuantity");

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "WeighingRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialUnitId",
                table: "WeighingRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeighingRecordAttachments_AttachmentFileId",
                table: "WeighingRecordAttachments",
                column: "AttachmentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_WaybillAttachments_AttachmentFileId",
                table: "WaybillAttachments",
                column: "AttachmentFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_WaybillAttachments_AttachmentFiles_AttachmentFileId",
                table: "WaybillAttachments",
                column: "AttachmentFileId",
                principalTable: "AttachmentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WaybillAttachments_Waybills_WaybillId",
                table: "WaybillAttachments",
                column: "WaybillId",
                principalTable: "Waybills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WeighingRecordAttachments_AttachmentFiles_AttachmentFileId",
                table: "WeighingRecordAttachments",
                column: "AttachmentFileId",
                principalTable: "AttachmentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WeighingRecordAttachments_WeighingRecords_WeighingRecordId",
                table: "WeighingRecordAttachments",
                column: "WeighingRecordId",
                principalTable: "WeighingRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
