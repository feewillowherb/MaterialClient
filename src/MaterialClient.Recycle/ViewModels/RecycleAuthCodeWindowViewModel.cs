using System.Threading.Tasks;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.UI.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Recycle.ViewModels;

/// <summary>
///     Recycle 授权窗口 ViewModel：固定 ProductCode 5020，隐藏称重模式选择。
/// </summary>
public class RecycleAuthCodeWindowViewModel : AuthCodeWindowViewModel, ITransientDependency
{
    public RecycleAuthCodeWindowViewModel(
        MaterialClient.Common.Services.Authentication.ILicenseService licenseService,
        MaterialClient.Common.Services.ISettingsService settingsService)
        : base(licenseService, settingsService)
    {
    }

    public override bool IsWeighingModeSelectorVisible => false;

    protected override ProductCode ResolveProductCode() => ProductCode.Recycle;

    public override async Task LoadCurrentDefaultWeighingModeAsync()
    {
        DefaultWeighingMode = WeighingMode.Recycle;
        await Task.CompletedTask;
    }
}
