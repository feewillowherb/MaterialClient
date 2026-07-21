using MaterialClient.Recycle.Api;
using MaterialClient.Recycle.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Volo.Abp.Modularity;

namespace MaterialClient.Common.EntityFrameworkCore;

/// <summary>
///     Recycle 数据上报同步的 EF-backed 测试模块。
///     复用 <see cref="MaterialClientEntityFrameworkCoreTestModule" /> 的 in-memory SQLite 宿主与既有 Mock API，
///     额外注册：
///     <list type="bullet">
///         <item><see cref="IRecycleDataApi" />：NSubstitute mock（不触发真实 §2.2/§2.3 HTTP 调用）。</item>
///         <item><see cref="RecycleSyncOptions" />：提供测试用 <c>PointNumber</c>，避免依赖 appsettings。</item>
///     </list>
/// </summary>
[DependsOn(typeof(MaterialClientEntityFrameworkCoreTestModule))]
public class RecycleDataSyncTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IRecycleDataApi>(_ => Substitute.For<IRecycleDataApi>());

        Configure<RecycleSyncOptions>(o =>
        {
            o.Enabled = true;
            o.ApiUrl = "http://localhost";
            o.PointNumber = "P-TEST";
            o.AccessKey = "test-access-key";
            o.SecretKey = "test-secret-key";
            o.PollIntervalSeconds = 5;
            o.TimeoutSeconds = 30;
        });
    }
}
