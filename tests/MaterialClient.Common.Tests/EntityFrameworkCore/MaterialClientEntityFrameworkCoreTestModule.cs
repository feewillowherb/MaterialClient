using MaterialClient.Common;
using MaterialClient.Common.Api;
using MaterialClient.Common.EntityFrameworkCore;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Tests.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;
using MaterialClient.Common.Recycle;
using MaterialClient.Common.Recycle.EntityFrameworkCore;
using MaterialClient.Common.Urban;
using MaterialClient.Common.Urban.EntityFrameworkCore;
using MaterialClient.EFCore;

namespace MaterialClient.Common.EntityFrameworkCore;

[DependsOn(
    typeof(MaterialClientTestBaseModule),
    typeof(MaterialClientCommonModule),
    typeof(MaterialClientEntityFrameworkCoreModule),
    typeof(MaterialClientCommonUrbanModule),
    typeof(MaterialClientCommonRecycleModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class MaterialClientEntityFrameworkCoreTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();

        ConfigureInMemorySqlite(context.Services);

        // ============================================
        // Mock API Registrations
        // ============================================
        
        // Register BasePlatformApi mock for license validation
        context.Services.AddSingleton<IBasePlatformApi>(sp =>
        {
            var mockApi = Substitute.For<IBasePlatformApi>();
            return mockApi;
        });

        // Register MaterialPlatformApi mock for authentication and material data operations
        // This mock provides default successful login response for BDD scenarios
        context.Services.AddSingleton<IMaterialPlatformApi>(sp =>
        {
            var mockApi = Substitute.For<IMaterialPlatformApi>();
            
            // Setup default login response for authentication scenarios
            mockApi.UserLoginAsync(Arg.Any<LoginRequestDto>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => Task.FromResult(new HttpResult<object>
                {
                    Success = true,
                    Code = 0,
                    Msg = "成功",
                    Data = new LoginUserDto
                    {
                        UserId = 1,
                        UserName = "testuser",
                        ClientId = Guid.NewGuid(),
                        Token = "test-access-token",
                        TrueName = "测试用户",
                        IsAdmin = false,
                        IsCompany = true,
                        ProductType = 2,
                        FromProductId = 1,
                        ProductId = 1,
                        ProductName = "测试产品",
                        CoId = 1,
                        CoName = "测试公司",
                        Url = "http://test.com",
                        AuthEndTime = DateTime.UtcNow.AddMonths(6)
                    }
                }));
            
            return mockApi;
        });

        // Register SoundDeviceApi mock for audio playback operations
        // This mock eliminates dependency on external HTTP service (localhost:8888)
        context.Services.AddSingleton<ISoundDeviceApi>(sp =>
        {
            var mockApi = Substitute.For<ISoundDeviceApi>();
            
            // Setup stub implementation for audio playback
            mockApi.PlayAudioAsync(Arg.Any<SoundDevicePlayRequestDto>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("{\"success\": true, \"message\": \"Mock audio played\"}"));
            
            return mockApi;
        });

        // ============================================
        // Test Services
        // ============================================
        
        // Register test service for test-only data persistence operations
        context.Services.AddTransient<ITestService, TestService>();
    }

    private void ConfigureInMemorySqlite(IServiceCollection services)
    {
        _sqliteConnection = CreateDatabaseAndGetConnection();

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<MaterialClientDbContext>(context =>
            {
                context.DbContextOptions.UseSqlite(_sqliteConnection)
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            });
            options.Configure<UrbanDbContext>(context =>
            {
                context.DbContextOptions.UseSqlite(_sqliteConnection)
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            });
            options.Configure<RecycleDbContext>(context =>
            {
                context.DbContextOptions.UseSqlite(_sqliteConnection)
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            });
        });
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _sqliteConnection?.Dispose();
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MaterialClientDbContext>()
            .UseSqlite(connection)
            .EnableDetailedErrors() // 启用详细的错误信息
            .EnableSensitiveDataLogging() // 启用敏感数据日志记录（包含参数值）
            .Options;

        using (var context = new MaterialClientDbContext(options))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        using (var urban = new UrbanDbContext(
                   new DbContextOptionsBuilder<UrbanDbContext>().UseSqlite(connection).Options))
        {
            urban.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        using (var recycle = new RecycleDbContext(
                   new DbContextOptionsBuilder<RecycleDbContext>().UseSqlite(connection).Options))
        {
            recycle.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}

