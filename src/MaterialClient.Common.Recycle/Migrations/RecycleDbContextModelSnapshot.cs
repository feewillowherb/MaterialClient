using MaterialClient.Common.Recycle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volo.Abp.EntityFrameworkCore;

#nullable disable

namespace MaterialClient.Common.Recycle.Migrations;

[DbContext(typeof(RecycleDbContext))]
[Migration("20260828000000_InitialRecycleProduct")]
partial class InitialRecycleProduct
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        RecycleDbContextModelSnapshot.BuildRecycleModel(modelBuilder);
    }
}

[DbContext(typeof(RecycleDbContext))]
partial class RecycleDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => BuildRecycleModel(modelBuilder);

    internal static void BuildRecycleModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("_Abp_DatabaseProvider", EfCoreDatabaseProvider.Sqlite)
            .HasAnnotation("ProductVersion", "10.0.1");

        modelBuilder.Entity("MaterialClient.Common.Entities.RecycleWaybillExtension", b =>
        {
            b.Property<Guid>("Id").HasColumnType("TEXT");
            b.Property<DateTime?>("ReceivingTime").HasColumnType("TEXT");
            b.Property<string>("SaleContractNo").HasMaxLength(100).HasColumnType("TEXT");
            b.Property<decimal?>("UnitPrice").HasPrecision(18, 4).HasColumnType("TEXT");
            b.Property<long>("WaybillId").HasColumnType("INTEGER");
            b.HasKey("Id");
            b.HasIndex("WaybillId").IsUnique();
            b.ToTable("RecycleWaybillExtensions", (string)null);
        });
    }
}
