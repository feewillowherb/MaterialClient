using MaterialClient.Common.Configuration;
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