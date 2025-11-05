using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;

namespace MaterialClient.EFCore;

public class MaterialClientDbContext : AbpDbContext<MaterialClientDbContext>
{
    public MaterialClientDbContext(DbContextOptions<MaterialClientDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<MaterialDefinition> MaterialDefinitions { get; set; }
    public DbSet<MaterialUnit> MaterialUnits { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Waybill> Waybills { get; set; }
    public DbSet<WeighingRecord> WeighingRecords { get; set; }
    public DbSet<AttachmentFile> AttachmentFiles { get; set; }
    public DbSet<WaybillAttachment> WaybillAttachments { get; set; }
    public DbSet<WeighingRecordAttachment> WeighingRecordAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure MaterialDefinition relationships
        modelBuilder.Entity<MaterialDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.UnitRate).IsRequired().HasDefaultValue(1);
        });

        // Configure MaterialUnit relationships
        modelBuilder.Entity<MaterialUnit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitName).IsRequired();
            entity.Property(e => e.Rate).IsRequired();
            
            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Provider relationships
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProviderName).IsRequired();
        });

        // Configure Waybill relationships
        modelBuilder.Entity<Waybill>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNo).IsRequired();
            
            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure WeighingRecord relationships
        modelBuilder.Entity<WeighingRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Weight).IsRequired();
            
            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Material)
                .WithMany()
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure AttachmentFile relationships
        modelBuilder.Entity<AttachmentFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.LocalPath).IsRequired();
        });

        // Configure WaybillAttachment relationships
        modelBuilder.Entity<WaybillAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Waybill)
                .WithMany()
                .HasForeignKey(e => e.WaybillId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.AttachmentFile)
                .WithMany()
                .HasForeignKey(e => e.AttachmentFileId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Composite unique constraint
            entity.HasIndex(e => new { e.WaybillId, e.AttachmentFileId })
                .IsUnique();
        });

        // Configure WeighingRecordAttachment relationships
        modelBuilder.Entity<WeighingRecordAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.WeighingRecord)
                .WithMany()
                .HasForeignKey(e => e.WeighingRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.AttachmentFile)
                .WithMany()
                .HasForeignKey(e => e.AttachmentFileId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Composite unique constraint
            entity.HasIndex(e => new { e.WeighingRecordId, e.AttachmentFileId })
                .IsUnique();
        });
    }
}

