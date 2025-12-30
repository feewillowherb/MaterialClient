using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Csv;

namespace MaterialClientToolkit.Models;

/// <summary>
/// Material_Attaches CSV数据模型
/// </summary>
public class MaterialAttachesCsv
{
    [ImporterHeader(Name = "FileId")]
    public int FileId { get; set; }

    [ImporterHeader(Name = "BizId")]
    public long BizId { get; set; }

    [ImporterHeader(Name = "BizType")]
    public int BizType { get; set; }

    [ImporterHeader(Name = "FileName")]
    public string FileName { get; set; } = string.Empty;

    [ImporterHeader(Name = "Bucket")]
    public string? Bucket { get; set; }

    [ImporterHeader(Name = "BucketKey")]
    public string? BucketKey { get; set; }

    [ImporterHeader(Name = "FileSize")]
    public long? FileSize { get; set; }

    [ImporterHeader(Name = "UploadStatus")]
    public int? UploadStatus { get; set; }

    [ImporterHeader(Name = "UploadTime")]
    public string? UploadTime { get; set; }

    [ImporterHeader(Name = "DeleteStatus")]
    public int DeleteStatus { get; set; }

    [ImporterHeader(Name = "LastEditUserId")]
    public int? LastEditUserId { get; set; }

    [ImporterHeader(Name = "LastEditor")]
    public string? LastEditor { get; set; }

    [ImporterHeader(Name = "CreateUserId")]
    public int? CreateUserId { get; set; }

    [ImporterHeader(Name = "Creator")]
    public string? Creator { get; set; }

    [ImporterHeader(Name = "UpdateTime")]
    public int? UpdateTime { get; set; }

    [ImporterHeader(Name = "AddTime")]
    public int? AddTime { get; set; }

    [ImporterHeader(Name = "UpdateDate")]
    public string? UpdateDate { get; set; }

    [ImporterHeader(Name = "AddDate")]
    public string? AddDate { get; set; }

    [ImporterHeader(Name = "LastSyncTime")]
    public string? LastSyncTime { get; set; }
}

