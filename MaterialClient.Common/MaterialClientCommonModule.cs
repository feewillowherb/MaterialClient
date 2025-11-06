using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.EntityFrameworkCore;
using MaterialClient.Common.EFCore;
using MaterialClient.Common.Services.Hikvision;

namespace MaterialClient.Common;

[DependsOn(
    typeof(AbpEntityFrameworkCoreModule)
)]
public class MaterialClientCommonModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // Register DbContext
        // Note: Connection string and SQLite options should be configured in the application startup module
        services.AddAbpDbContext<MaterialClientDbContext>(options =>
        {
            // DbContext options configuration will be done in the application startup
            // Example: options.UseSqlite(connectionString)
        });

        // Register Services
        services.AddSingleton<HikvisionService>();

        // Repositories are automatically registered by ABP framework
        // when using IRepository<TEntity, TKey> interface
        // No manual registration needed for repositories
    }
}
