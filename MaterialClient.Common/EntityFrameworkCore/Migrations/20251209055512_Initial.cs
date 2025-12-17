using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaterialClient.Common.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttachmentFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                    OssFullPath = table.Column<string>(type: "TEXT", nullable: true),
                    AttachType = table.Column<short>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicenseInfo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthToken = table.Column<Guid>(type: "TEXT", nullable: true),
                    AuthEndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MachineCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseInfo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    Size = table.Column<string>(type: "TEXT", nullable: true),
                    UpperLimit = table.Column<decimal>(type: "TEXT", nullable: true),
                    LowerLimit = table.Column<decimal>(type: "TEXT", nullable: true),
                    BasicUnit = table.Column<string>(type: "TEXT", nullable: true),
                    Code = table.Column<string>(type: "TEXT", nullable: true),
                    CoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Specifications = table.Column<string>(type: "TEXT", nullable: true),
                    ProId = table.Column<string>(type: "TEXT", nullable: true),
                    UnitName = table.Column<string>(type: "TEXT", nullable: true),
                    UnitRate = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 1m),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastEditUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastEditor = table.Column<string>(type: "TEXT", nullable: true),
                    CreateUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Creator = table.Column<string>(type: "TEXT", nullable: true),
                    UpdateTime = table.Column<int>(type: "INTEGER", nullable: true),
                    AddTime = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AddDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderType = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", nullable: false),
                    ContactName = table.Column<string>(type: "TEXT", nullable: true),
                    ContactPhone = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "UserCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LicenseInfoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LicenseInfoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TrueName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCompany = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProductType = table.Column<int>(type: "INTEGER", nullable: false),
                    FromProductId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ApiUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AuthEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LoginTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeighingRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Weight = table.Column<decimal>(type: "TEXT", nullable: false),
                    PlateNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: true),
                    MaterialUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    WaybillQuantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    MatchedId = table.Column<long>(type: "INTEGER", nullable: true),
                    MatchedType = table.Column<int>(type: "INTEGER", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeighingRecords", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "MaterialUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitName = table.Column<string>(type: "TEXT", nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: true),
                    RateName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialUnits_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialUnits_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Waybills",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderNo = table.Column<string>(type: "TEXT", nullable: false),
                    OrderType = table.Column<int>(type: "INTEGER", nullable: true),
                    DeliveryType = table.Column<int>(type: "INTEGER", nullable: true),
                    PlateNumber = table.Column<string>(type: "TEXT", nullable: true),
                    JoinTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OutTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Remark = table.Column<string>(type: "TEXT", nullable: true),
                    OrderPlanOnWeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    OrderPlanOnPcs = table.Column<decimal>(type: "TEXT", nullable: false),
                    OrderPcs = table.Column<decimal>(type: "TEXT", nullable: false),
                    OrderTotalWeight = table.Column<decimal>(type: "TEXT", nullable: true),
                    OrderTruckWeight = table.Column<decimal>(type: "TEXT", nullable: true),
                    OrderGoodsWeight = table.Column<decimal>(type: "TEXT", nullable: true),
                    LastSyncTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsEarlyWarn = table.Column<bool>(type: "INTEGER", nullable: false),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AbortReason = table.Column<string>(type: "TEXT", nullable: true),
                    OffsetResult = table.Column<short>(type: "INTEGER", nullable: false),
                    EarlyWarnType = table.Column<string>(type: "TEXT", nullable: true),
                    OrderSource = table.Column<short>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waybills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Waybills_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeighingRecordAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WeighingRecordId = table.Column<long>(type: "INTEGER", nullable: false),
                    AttachmentFileId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeighingRecordAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeighingRecordAttachments_AttachmentFiles_AttachmentFileId",
                        column: x => x.AttachmentFileId,
                        principalTable: "AttachmentFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WeighingRecordAttachments_WeighingRecords_WeighingRecordId",
                        column: x => x.WeighingRecordId,
                        principalTable: "WeighingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaybillAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WaybillId = table.Column<long>(type: "INTEGER", nullable: false),
                    AttachmentFileId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaybillAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaybillAttachments_AttachmentFiles_AttachmentFileId",
                        column: x => x.AttachmentFileId,
                        principalTable: "AttachmentFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WaybillAttachments_Waybills_WaybillId",
                        column: x => x.WaybillId,
                        principalTable: "Waybills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseInfo_ProjectId",
                table: "LicenseInfo",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialUnits_MaterialId",
                table: "MaterialUnits",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialUnits_ProviderId",
                table: "MaterialUnits",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Id",
                table: "Settings",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCredentials_ProjectId",
                table: "UserCredentials",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_ProjectId",
                table: "UserSessions",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaybillAttachments_AttachmentFileId",
                table: "WaybillAttachments",
                column: "AttachmentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_WaybillAttachments_WaybillId_AttachmentFileId",
                table: "WaybillAttachments",
                columns: new[] { "WaybillId", "AttachmentFileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Waybills_ProviderId",
                table: "Waybills",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingRecordAttachments_AttachmentFileId",
                table: "WeighingRecordAttachments",
                column: "AttachmentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingRecordAttachments_WeighingRecordId_AttachmentFileId",
                table: "WeighingRecordAttachments",
                columns: new[] { "WeighingRecordId", "AttachmentFileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenseInfo");

            migrationBuilder.DropTable(
                name: "MaterialUnits");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "UserCredentials");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "WaybillAttachments");

            migrationBuilder.DropTable(
                name: "WeighingRecordAttachments");

            migrationBuilder.DropTable(
                name: "WorkSettings");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "Waybills");

            migrationBuilder.DropTable(
                name: "AttachmentFiles");

            migrationBuilder.DropTable(
                name: "WeighingRecords");

            migrationBuilder.DropTable(
                name: "Providers");
        }
    }
}
