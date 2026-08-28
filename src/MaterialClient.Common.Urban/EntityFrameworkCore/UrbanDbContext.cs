using MaterialClient.Common.Entities.Urban;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace MaterialClient.Common.Urban.EntityFrameworkCore;

public class UrbanDbContext : AbpDbContext<UrbanDbContext>, IUrbanDbContext
{
    public UrbanDbContext(DbContextOptions<UrbanDbContext> options)
        : base(options)
    {
    }

    public DbSet<UrbanWeighingExtension> UrbanWeighingExtensions { get; set; } = null!;
    public DbSet<UrbanSettingsRow> UrbanSettingsRows { get; set; } = null!;
    public DbSet<UrbanPassageRecord> UrbanPassageRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UrbanWeighingExtension>(entity =>
        {
            entity.ConfigureByConvention();
            entity.ToTable("UrbanWeighingExtensions");
            entity.Property(e => e.SubmitMachineCode).HasMaxLength(128);
            entity.Property(e => e.AnomalyReason).HasMaxLength(32);
            entity.HasIndex(e => e.IsAnomaly);
            entity.HasIndex(e => e.WeighingRecordId).IsUnique();
            entity.HasIndex(e => new { e.SyncStatus, e.WeighingRecordId });
        });

        modelBuilder.Entity<UrbanSettingsRow>(entity =>
        {
            entity.ToTable("UrbanSettingsRows");
            entity.Property(e => e.SettingsJson).IsRequired();
        });

        modelBuilder.Entity<UrbanPassageRecord>(entity =>
        {
            entity.ConfigureByConvention();
            entity.ToTable("UrbanPassageRecords");
            entity.Property(e => e.PlateNumber).HasMaxLength(32);
            entity.Property(e => e.PlateColor).HasMaxLength(32);
            entity.Property(e => e.VehicleType).HasMaxLength(32);
            entity.HasIndex(e => e.CapturedAt);
            entity.HasIndex(e => e.PassageSource);
        });
    }
}
