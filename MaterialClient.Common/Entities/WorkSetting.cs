using Volo.Abp.Domain.Entities;

namespace MaterialClient.Common.Entities;

public class WorkSetting : Entity<int>
{
    public DateTime? MaterialUpdateTime { get; set; }
}