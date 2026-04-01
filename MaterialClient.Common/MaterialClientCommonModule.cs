using MaterialClient.Common.Configuration;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Huaxiazhixin;
using MaterialClient.Common.Services.Vzvision;
using MaterialClient.Common.Utils;
using MaterialClient.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Yitter.IdGenerator;

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
            options.AddDefaultRepositories(true);
        });

        // Configure SQLite connection from configuration
        var connectionString = configuration.GetConnectionString("Default")
                               ?? "Data Source=MaterialClient.db";

        // FIX: Convert relative database path to absolute path based on AppContext.BaseDirectory
        // This ensures the database can be accessed when the app is launched from any working directory
        // (e.g., C:\Windows\System32\ via Task Scheduler or Registry auto-start)
        connectionString = DatabaseConnectionStringFactory.FixConnectionString(connectionString);

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(c =>
            {
                c.DbContextOptions.UseSqlite(connectionString)
                    .EnableDetailedErrors() // 启用详细的错误信息
                    .EnableSensitiveDataLogging(); // 启用敏感数据日志记录（包含参数值）
            });
        });

        var options = new IdGeneratorOptions(1);
        // 2. 保存配置并初始化
        YitIdHelper.SetIdGenerator(options);

        // Configure AliyunOss
        services.Configure<AliyunOssConfig>(
            configuration.GetSection("AliyunOss"));
    }
}