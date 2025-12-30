using MaterialClientToolkit.Services;
using MaterialClient.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace MaterialClientToolkit;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        SQLitePCL.Batteries_V2.Init();

        Console.WriteLine(SQLitePCL.raw.sqlite3_libversion().utf8_to_string());
        
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

            // 从连接字符串中提取数据库文件路径
            var dbPath = ExtractDatabasePath(targetConnectionString);
            var dbExists = !string.IsNullOrEmpty(dbPath) && File.Exists(dbPath);

            Console.WriteLine($"目标数据库: {targetConnectionString}");
            if (!dbExists)
            {
                Console.WriteLine($"数据库文件不存在，将在初始化时创建: {dbPath ?? "未知路径"}");
            }

            Console.WriteLine("初始化ABP框架...");

            // 3. 创建并初始化ABP应用
            abpApplication = await AbpApplicationFactory.CreateAsync<MaterialClientToolkitModule>(options =>
            {
                options.Services.ReplaceConfiguration(configuration);
                options.UseAutofac();
            });

            await abpApplication.InitializeAsync();

            // 3.5. 如果数据库不存在，执行 Code First 迁移创建数据库和表结构
            if (!dbExists)
            {
                Console.WriteLine("数据库不存在，正在创建数据库和表结构...");
                try
                {
                    var unitOfWorkManager = abpApplication.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
                    var dbContextProvider = abpApplication.ServiceProvider
                        .GetRequiredService<IDbContextProvider<MaterialClientDbContext>>();

                    using var uow = unitOfWorkManager.Begin(true, false);
                    await using var dbContext = await dbContextProvider.GetDbContextAsync();
                    await dbContext.Database.MigrateAsync();
                    await uow.CompleteAsync();
                    Console.WriteLine("数据库和表结构创建成功！");
                }
                catch (Exception ex)
                {
                    var dbLogger = abpApplication.ServiceProvider.GetService<ILogger<Program>>();
                    dbLogger?.LogError(ex, "创建数据库和表结构失败");
                    Console.WriteLine($"错误: 创建数据库失败 - {ex.Message}");
                    return 1;
                }
            }

            // 4. 提示用户输入源数据库密码
            Console.Write("请输入encrypted_material.db的密码: ");
            var password = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("错误: 密码不能为空");
                return 1;
            }

            // 5. 构建源数据库连接字符串（不包含密码）
            var sourceConnectionString = "Data Source=encrypted_material.db";

            // 6. 验证源数据库连接
            Console.WriteLine("正在验证源数据库连接...");
            var sourceReader = new SourceDatabaseReaderService(
                sourceConnectionString,
                password,
                abpApplication.ServiceProvider.GetService<ILogger<SourceDatabaseReaderService>>());

            try
            {
                if (!await sourceReader.TestConnectionAsync())
                {
                    Console.WriteLine("错误: 无法连接到源数据库，请检查密码是否正确");
                    return 1;
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("密码错误") || ex.Message.Contains("密码"))
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine("提示: 请确认输入的密码是否正确");
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

    /// <summary>
    /// 从连接字符串中提取数据库文件路径
    /// </summary>
    private static string? ExtractDatabasePath(string connectionString)
    {
        // 解析 "Data Source=MaterialClient.db" 格式的连接字符串
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2 && keyValue[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                var path = keyValue[1].Trim();
                // 如果是相对路径，转换为绝对路径
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(Directory.GetCurrentDirectory(), path);
                }
                return path;
            }
        }
        return null;
    }
}