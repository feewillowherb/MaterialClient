using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Configuration;

namespace MaterialClient.EFCore;

public class MaterialClientDbContext : AbpDbContext<MaterialClientDbContext>
{
    public MaterialClientDbContext(DbContextOptions<MaterialClientDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<Material> Materials { get; set; }
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

        // Configure LicenseInfo
        modelBuilder.Entity<LicenseInfo>(entity =>
        {
            entity.HasKey(e => e.Id);
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
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EncryptedPassword).IsRequired().HasMaxLength(512);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            entity.HasOne(e => e.LicenseInfo)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // One credential per project
            entity.HasIndex(e => e.ProjectId).IsUnique();
        });

        // Configure UserSession
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
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
            
            entity.HasOne(e => e.LicenseInfo)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // One active session per project
            entity.HasIndex(e => e.ProjectId).IsUnique();
        });

        // Configure SettingsEntity
        modelBuilder.Entity<SettingsEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScaleSettingsJson).IsRequired();
            entity.Property(e => e.DocumentScannerConfigJson).IsRequired();
            entity.Property(e => e.SystemSettingsJson).IsRequired();
            entity.Property(e => e.CameraConfigsJson).IsRequired();
            entity.Property(e => e.LicensePlateRecognitionConfigsJson).IsRequired();
            
            // Only one settings record should exist
            entity.HasIndex(e => e.Id).IsUnique();
        });
    }
}
