using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

public class WorkSettingsEntity : Entity<int>
{
    public DateTime? MaterialUpdateTime { get; set; }
}