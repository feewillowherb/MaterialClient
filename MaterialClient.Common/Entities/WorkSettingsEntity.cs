using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

public class WorkSettingsEntity : Entity<int>
{
    public DateTime? MaterialUpdatedTime { get; set; }


    public DateTime? MaterialTypeUpdatedTime { get; set; }

    public DateTime? ProviderUpdatedTime { get; set; }
}