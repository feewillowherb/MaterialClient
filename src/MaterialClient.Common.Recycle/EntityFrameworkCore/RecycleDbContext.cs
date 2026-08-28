using MaterialClient.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace MaterialClient.Common.Recycle.EntityFrameworkCore;

public class RecycleDbContext : AbpDbContext<RecycleDbContext>
{
    public RecycleDbContext(DbContextOptions<RecycleDbContext> options)
        : base(options)
    {
    }

    public DbSet<RecycleWaybillExtension> RecycleWaybillExtensions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RecycleWaybillExtension>(entity =>
        {
            entity.ConfigureByConvention();
            entity.ToTable("RecycleWaybillExtensions");
            entity.Property(e => e.SaleContractNo).HasMaxLength(100);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 4);
            entity.HasIndex(e => e.WaybillId).IsUnique();
        });
    }
}
