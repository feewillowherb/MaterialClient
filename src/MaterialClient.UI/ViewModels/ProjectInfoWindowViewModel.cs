using System;
using System.Threading.Tasks;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Extensions;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     项目信息窗口 ViewModel
/// </summary>
public partial class ProjectInfoWindowViewModel : ReactiveViewModelBase, ITransientDependency
{
    private const int ProductNameTapThreshold = 20;

    private readonly IAuthenticationService _authenticationService;
    private readonly ILicenseService _licenseService;
    private readonly ISettingsService _settingsService;

    private int _productNameTapCount;

    [Reactive] private string _projectName = string.Empty;

    [Reactive] private string _productNameDisplay = string.Empty;

    [Reactive] private string _expirationDate = string.Empty;

    [Reactive] private string _machineCode = string.Empty;

    [Reactive] private string _authCode = string.Empty;

    public ProjectInfoWindowViewModel(
        IAuthenticationService authenticationService,
        ILicenseService licenseService,
        ISettingsService settingsService)
    {
        _authenticationService = authenticationService;
        _licenseService = licenseService;
        _settingsService = settingsService;
        ProductNameTapCommand = ReactiveCommand.CreateFromTask(OnProductNameDisplayTappedAsync);
    }

    /// <summary>
    ///     点击产品名称时触发；连续点击 20 次后删除 LicenseInfo、UserSession、UserCredential 并刷新
    /// </summary>
    public IReactiveCommand ProductNameTapCommand { get; }

    /// <summary>
    ///     初始化数据
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var license = await _licenseService.GetCurrentLicenseAsync();
            if (license != null)
            {
                ProjectName = string.IsNullOrWhiteSpace(license.ProName) ? "未命名项目" : license.ProName;

                ExpirationDate = license.AuthEndTime.ToString("yyyy-MM-dd");

                MachineCode = !string.IsNullOrEmpty(license.MachineCode)
                    ? MaskCode(license.MachineCode)
                    : string.Empty;

                AuthCode = !string.IsNullOrWhiteSpace(license.AccessCode)
                    ? MaskCode(license.AccessCode)
                    : string.Empty;
            }
            else
            {
                ProjectName = "未授权";
                ExpirationDate = "未授权";
                MachineCode = string.Empty;
                AuthCode = string.Empty;
            }

            var settings = await _settingsService.GetSettingsAsync();
            ProductNameDisplay = settings.SystemSettings.DefaultWeighingMode.GetDescription();
        }
        catch (Exception)
        {
            ProjectName = "获取失败";
            ProductNameDisplay = "获取失败";
            ExpirationDate = "获取失败";
            MachineCode = string.Empty;
            AuthCode = string.Empty;
        }
    }

    private async Task OnProductNameDisplayTappedAsync()
    {
        _productNameTapCount++;
        if (_productNameTapCount < ProductNameTapThreshold)
            return;

        _productNameTapCount = 0;
        await _authenticationService.ClearAllAuthDataAsync();
        await InitializeAsync();
    }

    /// <summary>
    ///     隐藏代码中间部分（显示前4位和后4位）
    /// </summary>
    private static string MaskCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length <= 8)
            return code;

        return code.Substring(0, 4) + "****" + code.Substring(code.Length - 4);
    }
}
