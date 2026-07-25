using System.Text.Json.Serialization;
using MaterialClient.Urban.Dtos;
using Refit;

namespace MaterialClient.Urban.Api;

/// <summary>
///     UrbanManagement 服务端 Refit API 接口（ABP 约定路由 + multipart 上传）
/// </summary>
public interface IUrbanManagementApi
{
    /// <summary>
    ///     接收称重记录（对应 <see cref="UrbanManagement.Core.Services.IUrbanWeighingRecordAppService.ReceiveAsync"/>）
    /// </summary>
    [Headers("Content-Type: application/json")]
    [Post("/api/app/urban-weighing-record/receive")]
    Task<UrbanWeighingRecordReceiveResult> ReceiveWeighingRecordAsync(
        [Body] UrbanWeighingRecordSubmitDto dto);

    /// <summary>
    ///     Legacy Base64 JSON upload. Prefer <see cref="UploadAttachmentsMultipartAsync"/>.
    ///     Retained until all clients migrate; do not delete.
    /// </summary>
    [Headers("Content-Type: application/json")]
    [Post("/api/app/urban-attachment/upload")]
    Task<UrbanAttachmentUploadResponseDto> UploadAttachmentsAsync(
        [Body] UrbanAttachmentUploadRequestDto dto);

    /// <summary>
    ///     Preferred multipart binary upload (UrbanAttachmentController.upload-multipart).
    /// </summary>
    [Multipart]
    [Post("/api/urban-attachment/upload-multipart")]
    Task<UrbanAttachmentUploadResponseDto> UploadAttachmentsMultipartAsync(
        [AliasAs("buildLicenseNo")] string buildLicenseNo,
        [AliasAs("attachType")] short attachType,
        [AliasAs("files")] IEnumerable<StreamPart> files);

    [Headers("Content-Type: application/json")]
    [Post("/api/app/urban-weighing-record/ack-approval-sync")]
    Task AckApprovalSyncAsync([Body] AckApprovalSyncDto dto);

    [Get("/api/app/urban-weighing-record/pending-server-approval-sync")]
    Task<List<MaterialClient.Common.Models.WeighingRecordApprovedPushDto>> GetPendingServerApprovalSyncAsync(
        [Query] PendingServerApprovalSyncQueryDto input);
}

/// <summary>
///     称重记录接收结果（与 UrbanManagement UrbanWeighingRecordReceiveOutputDto 对齐）
/// </summary>
public class UrbanWeighingRecordReceiveResult
{
    [JsonPropertyName("recordId")]
    public Guid RecordId { get; set; }
}
