using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using MaterialClient.EFCore;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Api;
using Refit;
using Polly;

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

        // Register DbContext with default repositories
        services.AddAbpDbContext<MaterialClientDbContext>(options =>
        {
            // Enable default repositories for all entities
            options.AddDefaultRepositories(includeAllEntities: true);
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

        // Register Refit API Client with retry policy and timeout
        var basePlatformUrl = configuration["BasePlatform:BaseUrl"] 
            ?? "http://base.publicapi.findong.com";
        
        services.AddRefitClient<IBasePlatformApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(basePlatformUrl);
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddTransientHttpErrorPolicy(policy =>
                policy.WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                ));

        // Register Authentication Services
        services.AddSingleton<IMachineCodeService, MachineCodeService>();
        services.AddSingleton<IPasswordEncryptionService, PasswordEncryptionService>();
        // ILicenseService and IAuthenticationService are auto-registered by ABP (ITransientDependency)

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
