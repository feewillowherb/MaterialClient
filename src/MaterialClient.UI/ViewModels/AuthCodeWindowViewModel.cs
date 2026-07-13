using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using ReactiveUI.SourceGenerators;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     授权码输入窗口 ViewModel（Standard/SolidWaste 主程序默认实现）
/// </summary>
public partial class AuthCodeWindowViewModel : ReactiveViewModelBase, ITransientDependency
{
    private readonly ILicenseService _licenseService;
    private readonly ISettingsService _settingsService;

    [Reactive] private string _authorizationCode = string.Empty;

    [Reactive] private bool _isVerified;

    [Reactive] private bool _isVerifying;

    [Reactive] private bool _showRetryButton;

    [Reactive] private string _statusMessage = string.Empty;

    [Reactive] private string _statusMessageColor = "#000000";

    [Reactive] private WeighingMode _defaultWeighingMode = WeighingMode.Standard;

    /// <summary>
    ///     是否显示客户端版本（称重模式）选择器；Recycle 独立客户端隐藏。
    /// </summary>
    public virtual bool IsWeighingModeSelectorVisible => true;

    public static IList<WeighingMode> DefaultWeighingModeOptions { get; } =
        new[] { WeighingMode.Standard, WeighingMode.SolidWaste };

    public AuthCodeWindowViewModel(ILicenseService licenseService, ISettingsService settingsService)
    {
        _licenseService = licenseService;
        _settingsService = settingsService;
    }

    public virtual async Task LoadCurrentDefaultWeighingModeAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            DefaultWeighingMode = settings.SystemSettings.DefaultWeighingMode;
        }
        catch
        {
            // keep default
        }
    }

    [ReactiveCommand]
    private async Task VerifyAuthorizationCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(AuthorizationCode))
        {
            ShowErrorMessage("请输入授权码");
            return;
        }

        IsVerifying = true;
        ShowRetryButton = false;
        StatusMessage = "正在验证...";
        StatusMessageColor = "#6498FE";

        try
        {
            var productCode = ResolveProductCode();
            await _licenseService.VerifyAuthorizationCodeAsync(AuthorizationCode, productCode);
            await _settingsService.SaveDefaultWeighingModeAsync(productCode);

            IsVerified = true;
            StatusMessage = "授权成功！";
            StatusMessageColor = "#4CAF50";
            ShowRetryButton = false;
        }
        catch (BusinessException ex)
        {
            HandleVerificationError(ex.Message);
        }
        catch (Exception ex)
        {
            HandleVerificationError($"授权验证失败：{ex.Message}");
        }
        finally
        {
            IsVerifying = false;
        }
    }

    protected virtual ProductCode ResolveProductCode()
        => DefaultWeighingMode == WeighingMode.SolidWaste ? ProductCode.SolidWaste : ProductCode.Standard;

    [ReactiveCommand]
    private void Retry()
    {
        ResetForm();
    }

    private void HandleVerificationError(string errorMessage)
    {
        IsVerified = false;

        if (errorMessage.Contains("网络") || errorMessage.Contains("连接"))
        {
            StatusMessage = "网络连接失败，请检查网络设置";
            ShowRetryButton = true;
        }
        else
        {
            StatusMessage = errorMessage;
            ShowRetryButton = false;
        }

        StatusMessageColor = "#F44336";
    }

    private void ShowErrorMessage(string message)
    {
        StatusMessage = message;
        StatusMessageColor = "#F44336";
        ShowRetryButton = false;
    }

    private void ResetForm()
    {
        AuthorizationCode = string.Empty;
        StatusMessage = string.Empty;
        StatusMessageColor = "#000000";
        ShowRetryButton = false;
        IsVerified = false;
    }

    public void HandleWindowClose()
    {
        if (!IsVerified)
        {
            Environment.Exit(0);
        }
    }
}
