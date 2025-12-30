using MaterialClientToolkit.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace MaterialClientToolkit;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        IAbpApplicationWithInternalServiceProvider? abpApplication = null;

        try
        {
            // 1. 读取配置
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // 2. 检查数据库连接字符串
            var connectionString = configuration.GetConnectionString("Default")
                                   ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                                   ?? "Data Source=MaterialClient.db";

            if (!connectionString.Contains("Data Source=MaterialClient.db", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("错误: 未检测到 Data Source=MaterialClient.db 连接字符串");
                Console.WriteLine($"当前连接字符串: {connectionString}");
                return 1; // 退出码1表示错误
            }

            Console.WriteLine($"数据库连接字符串: {connectionString}");
            Console.WriteLine("初始化ABP框架...");

            // 3. 创建并初始化ABP应用
            abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientToolkitModule>(options =>
            {
                options.Services.ReplaceConfiguration(configuration);
                options.UseAutofac();
            });

            await abpApplication.InitializeAsync();

            Console.WriteLine("开始CSV数据迁移...");

            // 4. 从ABP容器获取服务
            var csvReaderService = abpApplication.ServiceProvider.GetRequiredService<CsvReaderService>();
            var csvMapperService = abpApplication.ServiceProvider.GetRequiredService<CsvMapperService>();
            var dbContext = abpApplication.ServiceProvider.GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var logger = abpApplication.ServiceProvider.GetService<ILogger<CsvMigrationService>>();

            var migrationService = new CsvMigrationService(
                csvReaderService,
                csvMapperService,
                dbContext,
                logger);

            // 5. 执行迁移
            var csvDirectory = AppContext.BaseDirectory;
            var exitCode = await migrationService.MigrateAsync(csvDirectory);

            if (exitCode == 0)
            {
                Console.WriteLine("数据迁移成功完成！");
            }
            else
            {
                Console.WriteLine($"数据迁移失败，退出码: {exitCode}");
            }

            return exitCode;
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"错误: 文件未找到 - {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            return 1;
        }
        finally
        {
            // 清理ABP应用
            if (abpApplication != null)
            {
                await abpApplication.ShutdownAsync();
                abpApplication.Dispose();
            }
        }
    }
}