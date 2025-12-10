using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Configuration;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace MaterialClient.EFCore;

public class MaterialClientDbContext : AbpDbContext<MaterialClientDbContext>
{
    public MaterialClientDbContext(DbContextOptions<MaterialClientDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<Material> Materials { get; set; }
    public DbSet<MaterialType> MaterialTypes { get; set; }
    public DbSet<MaterialUnit> MaterialUnits { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Waybill> Waybills { get; set; }
    public DbSet<WeighingRecord> WeighingRecords { get; set; }
    public DbSet<AttachmentFile> AttachmentFiles { get; set; }
    public DbSet<WaybillAttachment> WaybillAttachments { get; set; }
    public DbSet<WeighingRecordAttachment> WeighingRecordAttachments { get; set; }

    // Authentication DbSets
    public DbSet<LicenseInfo> LicenseInfos { get; set; }
    public DbSet<UserCredential> UserCredentials { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }

    // Settings DbSet
    public DbSet<SettingsEntity> Settings { get; set; }
    public DbSet<WorkSettingsEntity> WorkSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore configuration classes - they are not entities, only used for JSON serialization
        modelBuilder.Ignore<CameraConfig>();
        modelBuilder.Ignore<LicensePlateRecognitionConfig>();
        modelBuilder.Ignore<ScaleSettings>();
        modelBuilder.Ignore<DocumentScannerConfig>();
        modelBuilder.Ignore<SystemSettings>();

        // Configure Material relationships
        modelBuilder.Entity<Material>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.UnitRate).IsRequired().HasDefaultValue(1);
        });

        // Configure MaterialType relationships
        modelBuilder.Entity<MaterialType>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.TypeName).HasMaxLength(200);
            entity.Property(e => e.TypeCode).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.ParentId).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.CoId).IsRequired();
            entity.Property(e => e.UpperLimit).HasPrecision(18, 2).HasDefaultValue(0);
            entity.Property(e => e.LowerLimit).HasPrecision(18, 2).HasDefaultValue(0);

            // 创建索引以提高查询性能
            entity.HasIndex(e => e.TypeCode);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.CoId);
            entity.HasIndex(e => e.IsDeleted);
        });

        // Configure MaterialUnit relationships
        modelBuilder.Entity<MaterialUnit>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.UnitName).IsRequired();
            entity.Property(e => e.Rate).IsRequired();
        });

        // Configure Provider relationships
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.ProviderName).IsRequired();
        });

        // Configure Waybill relationships
        modelBuilder.Entity<Waybill>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.OrderNo).IsRequired();

            entity.HasOne(e => e.Provider)
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure WeighingRecord relationships
        modelBuilder.Entity<WeighingRecord>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.Weight).IsRequired();
        });

        // Configure AttachmentFile relationships
        modelBuilder.Entity<AttachmentFile>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.LocalPath).IsRequired();
        });

        // Configure WaybillAttachment relationships
        modelBuilder.Entity<WaybillAttachment>(entity =>
        {
            entity.ConfigureByConvention();
            
            // Composite unique constraint
            entity.HasIndex(e => new { e.WaybillId, e.AttachmentFileId })
                .IsUnique();
        });

        // Configure WeighingRecordAttachment relationships
        modelBuilder.Entity<WeighingRecordAttachment>(entity =>
        {
            entity.ConfigureByConvention();

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

        // Configure LicenseInfo
        modelBuilder.Entity<LicenseInfo>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.AuthEndTime).IsRequired();
            entity.Property(e => e.MachineCode).IsRequired().HasMaxLength(128);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            // Only one license should exist at a time
            entity.HasIndex(e => e.ProjectId);
        });

        // Configure UserCredential
        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EncryptedPassword).IsRequired().HasMaxLength(512);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            // One credential per project
            entity.HasIndex(e => e.ProjectId).IsUnique();
        });

        // Configure UserSession
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.LicenseInfoId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TrueName).HasMaxLength(100);
            entity.Property(e => e.ClientId).IsRequired();
            entity.Property(e => e.AccessToken).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.ApiUrl).HasMaxLength(500);
            entity.Property(e => e.LoginTime).IsRequired();
            entity.Property(e => e.LastActivityTime).IsRequired();

            // One active session per project
            entity.HasIndex(e => e.ProjectId).IsUnique();
        });

        // Configure SettingsEntity
        modelBuilder.Entity<SettingsEntity>(entity =>
        {
            entity.ConfigureByConvention();
            
            entity.Property(e => e.ScaleSettingsJson).IsRequired();
            entity.Property(e => e.DocumentScannerConfigJson).IsRequired();
            entity.Property(e => e.SystemSettingsJson).IsRequired();
            entity.Property(e => e.CameraConfigsJson).IsRequired();
            entity.Property(e => e.LicensePlateRecognitionConfigsJson).IsRequired();
        });

        // Configure WorkSettingsEntity
        modelBuilder.Entity<WorkSettingsEntity>(entity =>
        {
            entity.ConfigureByConvention();
        });
    }
}
