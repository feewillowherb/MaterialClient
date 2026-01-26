// // This is the FIXED version of MaterialClientEntityFrameworkCoreTestModule.cs
// // Copy and replace the contents of:
// // MaterialClient.Common.Tests\EntityFrameworkCore\MaterialClientEntityFrameworkCoreTestModule.cs
//
// using MaterialClient.Common.Api;
// using MaterialClient.Common.Api.Dtos;
// using MaterialClient.Common.Tests.Services;
// using Microsoft.Data.Sqlite;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Infrastructure;
// using Microsoft.EntityFrameworkCore.Storage;
// using Microsoft.Extensions.DependencyInjection;
// using NSubstitute;
// using Volo.Abp;
// using Volo.Abp.EntityFrameworkCore;
// using Volo.Abp.EntityFrameworkCore.Sqlite;
// using Volo.Abp.Modularity;
// using Volo.Abp.Uow;
// using MaterialClient.EFCore;
// using System;
// using System.Threading;
// using System.Threading.Tasks;
//
// namespace MaterialClient.Common.EntityFrameworkCore;
//
// [DependsOn(
//     typeof(MaterialClientTestBaseModule),
//     typeof(MaterialClientCommonModule),
//     typeof(AbpEntityFrameworkCoreSqliteModule)
// )]
// public class MaterialClientEntityFrameworkCoreTestModule : AbpModule
// {
//     private SqliteConnection? _sqliteConnection;
//
//     public override void ConfigureServices(ServiceConfigurationContext context)
//     {
//         context.Services.AddAlwaysDisableUnitOfWorkTransaction();
//
//         ConfigureInMemorySqlite(context.Services);
//
//         // ============================================
//         // Mock API Registrations
//         // ============================================
//         
//         // Register BasePlatformApi mock
//         context.Services.AddSingleton<IBasePlatformApi>(sp =>
//         {
//             var mockApi = Substitute.For<IBasePlatformApi>();
//             return mockApi;
//         });
//
//         // Register MaterialPlatformApi mock (FIX: This was missing!)
//         context.Services.AddSingleton<IMaterialPlatformApi>(sp =>
//         {
//             var mockApi = Substitute.For<IMaterialPlatformApi>();
//             
//             // Setup default login response
//             mockApi.UserLoginAsync(Arg.Any<LoginRequestDto>(), Arg.Any<CancellationToken>())
//                 .Returns(Task.FromResult(new HttpResult<LoginUserDto>
//                 {
//                     Success = true,
//                     Code = 0,
//                     Msg = "成功",
//                     Data = new LoginUserDto
//                     {
//                         UserId = 1,
//                         UserName = "testuser",
//                         ClientId = Guid.NewGuid(),
//                         Token = "test-access-token",
//                         TrueName = "测试用户",
//                         IsAdmin = false,
//                         IsCompany = true,
//                         ProductType = 2,
//                         FromProductId = 1,
//                         ProductId = 1,
//                         ProductName = "测试产品",
//                         CoId = 1,
//                         CoName = "测试公司",
//                         Url = "http://test.com",
//                         AuthEndTime = DateTime.UtcNow.AddMonths(6)
//                     }
//                 }));
//             
//             return mockApi;
//         });
//
//         // Register SoundDeviceApi mock (FIX: This was also missing!)
//         context.Services.AddSingleton<ISoundDeviceApi>(sp =>
//         {
//             var mockApi = Substitute.For<ISoundDeviceApi>();
//             
//             mockApi.PlayAudioAsync(Arg.Any<SoundDevicePlayRequestDto>(), Arg.Any<CancellationToken>())
//                 .Returns(Task.FromResult("{\"success\": true, \"message\": \"Mock audio played\"}"));
//             
//             return mockApi;
//         });
//
//         // ============================================
//         // Test Services
//         // ============================================
//         
//         // Register test service for test-only data persistence operations
//         context.Services.AddTransient<ITestService, TestService>();
//     }
//
//     private void ConfigureInMemorySqlite(IServiceCollection services)
//     {
//         _sqliteConnection = CreateDatabaseAndGetConnection();
//
//         services.Configure<AbpDbContextOptions>(options =>
//         {
//             options.Configure(context =>
//             {
//                 context.DbContextOptions.UseSqlite(_sqliteConnection)
//                     .EnableDetailedErrors() // 启用详细的错误信息
//                     .EnableSensitiveDataLogging(); // 启用敏感数据日志记录（包含参数值）
//             });
//         });
//     }
//
//     public override void OnApplicationShutdown(ApplicationShutdownContext context)
//     {
//         _sqliteConnection?.Dispose();
//     }
//
//     private static SqliteConnection CreateDatabaseAndGetConnection()
//     {
//         var connection = new SqliteConnection("Data Source=:memory:");
//         connection.Open();
//
//         var options = new DbContextOptionsBuilder<MaterialClientDbContext>()
//             .UseSqlite(connection)
//             .EnableDetailedErrors() // 启用详细的错误信息
//             .EnableSensitiveDataLogging() // 启用敏感数据日志记录（包含参数值）
//             .Options;
//
//         using (var context = new MaterialClientDbContext(options))
//         {
//             context.GetService<IRelationalDatabaseCreator>().CreateTables();
//         }
//
//         return connection;
//     }
// }
