using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace MaterialClient.Common.Urban.EntityFrameworkCore;

public interface IUrbanDbContext
{
    DbSet<Entities.Urban.UrbanWeighingExtension> UrbanWeighingExtensions { get; }
    DbSet<UrbanSettingsRow> UrbanSettingsRows { get; }
}

public class UrbanSettingsRow : Volo.Abp.Domain.Entities.Entity<int>
{
    public string SettingsJson { get; set; } = string.Empty;
}
