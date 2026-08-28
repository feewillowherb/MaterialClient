using MaterialClient.Common.Backgrounds;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Providers;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Huaxiazhixin;
using MaterialClient.Common.Services.Vzvision;
using MaterialClient.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;
using Yitter.IdGenerator;

namespace MaterialClient.Common;

[DependsOn(typeof(AbpBackgroundWorkersModule))]
public class MaterialClientCommonModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        services.AddMemoryCache();

        var options = new IdGeneratorOptions(1);
        // 2. 保存配置并初始化
        YitIdHelper.SetIdGenerator(options);

        // Configure AliyunOss
        services.Configure<AliyunOssConfig>(
            configuration.GetSection("AliyunOss"));

        services.Configure<ImageCleanupOptions>(
            configuration.GetSection(ImageCleanupOptions.SectionName));
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var enabled = configuration.GetValue($"{ImageCleanupOptions.SectionName}:Enabled", true);
        if (enabled)
        {
            await context.AddBackgroundWorkerAsync<ImageCleanupBackgroundService>();
        }
        else
        {
            var logger = context.ServiceProvider.GetService<ILogger<MaterialClientCommonModule>>();
            logger?.LogInformation(
                "ImageCleanupBackgroundService is disabled by configuration ({Section}:Enabled=false).",
                ImageCleanupOptions.SectionName);
        }
    }
}
