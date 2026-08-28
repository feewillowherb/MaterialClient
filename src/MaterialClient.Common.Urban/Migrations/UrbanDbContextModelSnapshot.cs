using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Urban.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Volo.Abp.EntityFrameworkCore;

#nullable disable

namespace MaterialClient.Common.Urban.Migrations;

[DbContext(typeof(UrbanDbContext))]
[Migration("20260828000000_InitialUrbanProduct")]
partial class InitialUrbanProduct
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        UrbanDbContextModelSnapshot.BuildUrbanModel(modelBuilder);
    }
}

[DbContext(typeof(UrbanDbContext))]
partial class UrbanDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => BuildUrbanModel(modelBuilder);

    internal static void BuildUrbanModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("_Abp_DatabaseProvider", EfCoreDatabaseProvider.Sqlite)
            .HasAnnotation("ProductVersion", "10.0.1");

        modelBuilder.Entity("MaterialClient.Common.Entities.Urban.UrbanWeighingExtension", b =>
        {
            b.Property<Guid>("Id").HasColumnType("TEXT");
            b.Property<string>("AnomalyReason").HasMaxLength(32).HasColumnType("TEXT");
            b.Property<string>("ExtraProperties").IsRequired().HasColumnType("TEXT").HasColumnName("ExtraProperties");
            b.Property<bool>("IsAnomaly").HasColumnType("INTEGER");
            b.Property<DateTime?>("LastErrorTime").HasColumnType("TEXT");
            b.Property<int>("RetryCount").HasColumnType("INTEGER");
            b.Property<string>("SubmitMachineCode").HasMaxLength(128).HasColumnType("TEXT");
            b.Property<int>("SyncStatus").HasColumnType("INTEGER");
            b.Property<long>("WeighingRecordId").HasColumnType("INTEGER");
            b.HasKey("Id");
            b.HasIndex("IsAnomaly");
            b.HasIndex("WeighingRecordId").IsUnique();
            b.HasIndex("SyncStatus", "WeighingRecordId");
            b.ToTable("UrbanWeighingExtensions");
        });

        modelBuilder.Entity("MaterialClient.Common.Urban.EntityFrameworkCore.UrbanSettingsRow", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<string>("SettingsJson").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("UrbanSettingsRows");
        });
    }
}
