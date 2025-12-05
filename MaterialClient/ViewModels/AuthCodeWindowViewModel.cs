using System;
using System.Threading.Tasks;
using MaterialClient.Common.Services.Authentication;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volo.Abp;

namespace MaterialClient.ViewModels;

/// <summary>
/// 授权码输入窗口 ViewModel
/// </summary>
public partial class AuthCodeWindowViewModel(ILicenseService licenseService) : ReactiveViewModelBase
{
    [ObservableProperty]
    private string _authorizationCode = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessageColor = "#000000";

    [ObservableProperty]
    private bool _isVerifying;

    [ObservableProperty]
    private bool _showRetryButton;

    [ObservableProperty]
    private bool _isVerified;

    #region Commands

    [RelayCommand]
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
            // Call license service to verify
            await licenseService.VerifyAuthorizationCodeTestAsync(AuthorizationCode);

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

    [RelayCommand()]
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
        {
            // User closed window without completing authorization
            // Application should exit (as per FR-003)
            Environment.Exit(0);
        }
    }

    #endregion
}

