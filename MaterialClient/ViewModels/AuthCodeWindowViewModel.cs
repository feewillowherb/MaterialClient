using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using ReactiveUI.SourceGenerators;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

/// <summary>
///     授权码输入窗口 ViewModel
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

    /// <summary>
    ///     默认称重模式（用户在授权窗口选择，验证成功后持久化）
    /// </summary>
    [Reactive] private WeighingMode _defaultWeighingMode = WeighingMode.Standard;

    /// <summary>
    ///     ComboBox 选项：标准、固废
    /// </summary>
    public static IList<WeighingMode> DefaultWeighingModeOptions { get; } =
        new[] { WeighingMode.Standard, WeighingMode.SolidWaste };

    public AuthCodeWindowViewModel(ILicenseService licenseService, ISettingsService settingsService)
    {
        _licenseService = licenseService;
        _settingsService = settingsService;
    }

    /// <summary>
    ///     从已保存的设置中加载当前默认称重模式，用于打开窗口时预填 ComboBox。
    /// </summary>
    public async Task LoadCurrentDefaultWeighingModeAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            DefaultWeighingMode = settings.SystemSettings.DefaultWeighingMode;
        }
        catch
        {
            // 保持默认值 WeighingMode.Standard
        }
    }

    #region Commands

    [ReactiveCommand]
    private async Task VerifyAuthorizationCodeAsync()
    {
        // Validate input
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
            var productCode = DefaultWeighingMode == WeighingMode.SolidWaste ? ProductCode.SolidWaste : ProductCode.Standard;
            await _licenseService.VerifyAuthorizationCodeAsync(AuthorizationCode, productCode);
            await _settingsService.SaveDefaultWeighingModeAsync(productCode);

            // Success
            IsVerified = true;
            StatusMessage = "授权成功！";
            StatusMessageColor = "#4CAF50"; // Green
            ShowRetryButton = false;

            // Window will be closed automatically by the View after detecting IsVerified = true
        }
        catch (BusinessException ex)
        {
            // Business exception from license service
            HandleVerificationError(ex.Message);
        }
        catch (Exception ex)
        {
            // Unexpected exception
            HandleVerificationError($"授权验证失败：{ex.Message}");
        }
        finally
        {
            IsVerifying = false;
        }
    }

    [ReactiveCommand]
    private void Retry()
    {
        ResetForm();
    }

    #endregion

    #region Methods

    private void HandleVerificationError(string errorMessage)
    {
        IsVerified = false;

        // Check if it's a network error
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

        StatusMessageColor = "#F44336"; // Red
    }

    private void ShowErrorMessage(string message)
    {
        StatusMessage = message;
        StatusMessageColor = "#F44336"; // Red
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
            // User closed window without completing authorization
            // Application should exit (as per FR-003)
            Environment.Exit(0);
    }

    #endregion
}