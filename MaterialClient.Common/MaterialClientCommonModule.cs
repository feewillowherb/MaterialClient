using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using MaterialClient.EFCore;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.Hardware;

namespace MaterialClient.Common;

[DependsOn(
    typeof(AbpEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class MaterialClientCommonModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // Register DbContext
        services.AddAbpDbContext<MaterialClientDbContext>(options =>
        {
            // DbContext options will be configured via AbpDbContextOptions
        });

        // Configure SQLite connection from configuration
        var connectionString = configuration.GetConnectionString("Default") 
            ?? "Data Source=MaterialClient.db";
        
        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context =>
            {
                context.DbContextOptions.UseSqlite(connectionString);
            });
        });

        // Register Services
        services.AddSingleton<HikvisionService>();

        // Register Hardware Services (singleton for test value persistence)
        services.AddSingleton<ITruckScaleWeightService, Services.Hardware.TruckScaleWeightService>();
        services.AddSingleton<IPlateNumberCaptureService, Services.Hardware.PlateNumberCaptureService>();
        services.AddSingleton<IVehiclePhotoService, Services.Hardware.VehiclePhotoService>();
        services.AddSingleton<IBillPhotoService, Services.Hardware.BillPhotoService>();

        // Register WeighingService (transient, registered via ITransientDependency)
        // No need to register explicitly, ABP will auto-register it

        // Repositories are automatically registered by ABP framework
        // when using IRepository<TEntity, TKey> interface
        // No manual registration needed for repositories
    }
}
