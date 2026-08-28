using MaterialClient.Common;
using MaterialClient.Common.Utils;
using MaterialClient.EFCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace MaterialClient.Common.EntityFrameworkCore;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(AbpEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule))]
public class MaterialClientEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = services.GetConfiguration();

        services.AddAbpDbContext<MaterialClientDbContext>(options =>
        {
            options.AddDefaultRepositories(true);
        });

        var connectionString = configuration.GetConnectionString("Default")
                               ?? "Data Source=MaterialClient.db";
        connectionString = DatabaseConnectionStringFactory.FixConnectionString(connectionString);

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<MaterialClientDbContext>(c =>
            {
                MaterialClientSqliteDbContextOptions.Apply(
                    c.DbContextOptions,
                    c.ExistingConnection,
                    connectionString,
                    MaterialClientEfHistory.KernelTable);
            });
        });
    }
}
