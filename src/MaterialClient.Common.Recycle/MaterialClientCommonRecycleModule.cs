using MaterialClient.Common;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Common.Utils;
using MaterialClient.Common.Recycle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace MaterialClient.Common.Recycle;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(MaterialClientEntityFrameworkCoreModule))]
public class MaterialClientCommonRecycleModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();
        var connectionString = configuration.GetConnectionString("Default")
                               ?? "Data Source=MaterialClient.db";
        connectionString = DatabaseConnectionStringFactory.FixConnectionString(connectionString);

        services.AddAbpDbContext<RecycleDbContext>(options => { options.AddDefaultRepositories(true); });

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<RecycleDbContext>(c =>
            {
                c.DbContextOptions.UseSqlite(connectionString, sqlite =>
                        sqlite.MigrationsHistoryTable(MaterialClientEfHistory.RecycleTable))
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            });
        });
    }
}
