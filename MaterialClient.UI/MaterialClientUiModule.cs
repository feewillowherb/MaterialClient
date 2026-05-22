using MaterialClient.UI.Abstractions;
using MaterialClient.UI.Settings.Sections;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace MaterialClient.UI;

/// <summary>
///     Registers MaterialClient.UI assembly types (settings sections, device status helpers) with ABP.
/// </summary>
public class MaterialClientUiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Explicit ISettingsSection registration for reliable Autofac resolution (IEnumerable + GetServices)
        context.Services.AddTransient<ISettingsSection, ScaleSection>();
        context.Services.AddTransient<ISettingsSection, WeighingSection>();
        context.Services.AddTransient<ISettingsSection, CameraSection>();
        context.Services.AddTransient<ISettingsSection, LprSection>();
        context.Services.AddTransient<ISettingsSection, SystemSection>();
        context.Services.AddTransient<ISettingsSection, SoundDeviceSection>();
        context.Services.AddTransient<ISettingsSection, PrinterSection>();
    }
}
