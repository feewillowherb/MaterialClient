using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaterialClient.EFCore;

/// <summary>
///     SyncState 实体的 EF Core 配置
/// </summary>
public class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    public void Configure(EntityTypeBuilder<SyncState> builder)
    {
        // 配置表名
        builder.ToTable("SyncStates");

        // 配置主键
        builder.HasKey(x => x.Id);

        // 配置必填字段
        builder.Property(x => x.EntityType)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.LocalVersion)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.ClientRequestId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        // 配置唯一索引 (EntityType, EntityId)
        builder.HasIndex(x => new { x.EntityType, x.EntityId })
            .IsUnique();

        // 配置可选字段
        builder.Property(x => x.ServerVersion)
            .IsRequired(false);

        builder.Property(x => x.LastAttemptAt)
            .IsRequired(false);

        // 为查询性能添加索引
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.UpdatedAt);
    }
}
