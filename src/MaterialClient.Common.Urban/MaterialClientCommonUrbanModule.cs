using MaterialClient.Common;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Common.Utils;
using MaterialClient.Common.Urban.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace MaterialClient.Common.Urban;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(MaterialClientEntityFrameworkCoreModule))]
public class MaterialClientCommonUrbanModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();
        var connectionString = configuration.GetConnectionString("Default")
                               ?? "Data Source=MaterialClient.db";
        connectionString = DatabaseConnectionStringFactory.FixConnectionString(connectionString);

        services.AddAbpDbContext<UrbanDbContext>(options => { options.AddDefaultRepositories(true); });

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<UrbanDbContext>(c =>
            {
                MaterialClientSqliteDbContextOptions.Apply(
                    c.DbContextOptions,
                    c.ExistingConnection,
                    connectionString,
                    MaterialClientEfHistory.UrbanTable);
            });
        });
    }
}
