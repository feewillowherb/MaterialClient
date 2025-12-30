using MaterialClientToolkit.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using SQLitePCL;

namespace MaterialClientToolkit;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        // 初始化SQLCipher
        //SQLitePCLRaw.Batteries.Init();

        IAbpApplicationWithInternalServiceProvider? abpApplication = null;

        try
        {
            // 1. 读取配置
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // 2. 获取目标数据库连接字符串（MaterialClient.db）
            var targetConnectionString = configuration.GetConnectionString("Default")
                                        ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                                        ?? "Data Source=MaterialClient.db";

            Console.WriteLine($"目标数据库: {targetConnectionString}");
            Console.WriteLine("初始化ABP框架...");

            // 3. 创建并初始化ABP应用
            abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientToolkitModule>(options =>
            {
                options.Services.ReplaceConfiguration(configuration);
                options.UseAutofac();
            });

            await abpApplication.InitializeAsync();

            // 4. 提示用户输入源数据库密码
            Console.Write("请输入encrypted_material.db的密码: ");
            var password = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("错误: 密码不能为空");
                return 1;
            }

            // 5. 构建源数据库连接字符串
            var sourceConnectionString = $"Data Source=encrypted_material.db;Password={password}";

            // 6. 验证源数据库连接
            Console.WriteLine("正在验证源数据库连接...");
            var sourceReader = new SourceDatabaseReaderService(
                sourceConnectionString,
                abpApplication.ServiceProvider.GetService<ILogger<SourceDatabaseReaderService>>());

            if (!await sourceReader.TestConnectionAsync())
            {
                Console.WriteLine("错误: 无法连接到源数据库，请检查密码是否正确");
                return 1;
            }

            Console.WriteLine("源数据库连接成功！");
            Console.WriteLine("开始数据迁移...");

            // 7. 从ABP容器获取服务
            var csvMapperService = abpApplication.ServiceProvider.GetRequiredService<CsvMapperService>();
            var targetDbContext = abpApplication.ServiceProvider.GetRequiredService<MaterialClient.EFCore.MaterialClientDbContext>();
            var logger = abpApplication.ServiceProvider.GetService<ILogger<DatabaseMigrationService>>();

            var migrationService = new DatabaseMigrationService(
                sourceReader,
                csvMapperService,
                targetDbContext,
                logger);

            // 8. 执行迁移
            var exitCode = await migrationService.MigrateAsync();

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