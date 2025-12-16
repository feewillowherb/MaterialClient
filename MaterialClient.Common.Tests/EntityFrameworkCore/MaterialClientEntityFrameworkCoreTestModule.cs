using MaterialClient.Common.Api;
using MaterialClient.Common.Tests.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Security.Claims;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using MaterialClient.EFCore;

namespace MaterialClient.Common.EntityFrameworkCore;

[DependsOn(
    typeof(MaterialClientTestBaseModule),
    typeof(MaterialClientCommonModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class MaterialClientEntityFrameworkCoreTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();

        ConfigureInMemorySqlite(context.Services);

        // Register mock API for testing
        context.Services.AddSingleton<IBasePlatformApi>(sp =>
        {
            var mockApi = Substitute.For<IBasePlatformApi>();
            return mockApi;
        });

        // Register test service for test-only data persistence operations
        context.Services.AddTransient<ITestService, TestService>();
    }

    private void ConfigureInMemorySqlite(IServiceCollection services)
    {
        _sqliteConnection = CreateDatabaseAndGetConnection();

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context =>
            {
                context.DbContextOptions.UseSqlite(_sqliteConnection)
                    .EnableDetailedErrors() // 启用详细的错误信息
                    .EnableSensitiveDataLogging(); // 启用敏感数据日志记录（包含参数值）
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

        // Create a test ICurrentUser instance
        var testCurrentUser = new TestCurrentUser();

        using (var context = new MaterialClientDbContext(options, testCurrentUser))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }

    /// <summary>
    /// 测试用的 ICurrentUser 实现
    /// </summary>
    private class TestCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? Id => null;
        public string? UserName => null;
        public string? Name => null;
        public string? SurName => null;
        public string? PhoneNumber => null;
        public bool PhoneNumberVerified => false;
        public string? Email => null;
        public bool EmailVerified => false;
        public Guid? TenantId => null;
        public string[] Roles => Array.Empty<string>();

        public Claim? FindClaim(string claimType) => null;
        public Claim[] FindClaims(string claimType) => Array.Empty<Claim>();
        public Claim[] GetAllClaims() => Array.Empty<Claim>();
        public bool IsInRole(string roleName) => false;
    }
}

