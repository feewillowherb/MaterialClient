using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;

namespace MaterialClient.Common.Entities.Urban;

/// <summary>
///     Local aligned cache of Xiaoshan upload config from UrbanManagement (server is authority).
/// </summary>
[Table("XiaoshanUploadConfigCaches")]
public class XiaoshanUploadConfigCache : AuditedEntity<Guid>
{
    private XiaoshanUploadConfigCache()
    {
    }

    public XiaoshanUploadConfigCache(Guid id, Guid projectId) : base(id)
    {
        ProjectId = projectId;
    }

    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    ///     Server config entity id when aligned; empty when only a local draft exists.
    /// </summary>
    public Guid ServerConfigId { get; set; }

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(1000)]
    public string? Remark { get; set; }

    [Required]
    [MaxLength(8000)]
    public string ModesJson { get; set; } = "{}";

    [Required]
    [MaxLength(8000)]
    public string SettingsJson { get; set; } = "{}";

    /// <summary>
    ///     True when local cache matches last successful server get/write response.
    /// </summary>
    public bool IsAlignedWithServer { get; set; }

    /// <summary>
    ///     Mirror of server ConfigVersion when aligned; draft may carry stale value when not aligned.
    /// </summary>
    public long ConfigVersion { get; set; }
}
