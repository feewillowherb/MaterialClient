using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using MaterialClient.Common.Services.Authentication;
using ReactiveUI;
using Volo.Abp;

namespace MaterialClient.ViewModels;

/// <summary>
/// 授权码输入窗口 ViewModel
/// </summary>
public class AuthCodeWindowViewModel : ReactiveViewModelBase
{
    private readonly ILicenseService _licenseService;
    private string _authorizationCode = string.Empty;
    private string _statusMessage = string.Empty;
    private string _statusMessageColor = "#000000";
    private bool _isVerifying = false;
    private bool _showRetryButton = false;
    private bool _isVerified = false;

    public AuthCodeWindowViewModel()
    {
        // Design-time constructor
        _licenseService = null!;
        VerifyCommand = ReactiveCommand.Create(() => { });
        RetryCommand = ReactiveCommand.Create(() => { });
    }

    public AuthCodeWindowViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService;
        
        // Create commands
        VerifyCommand = ReactiveCommand.CreateFromTask(VerifyAuthorizationCodeAsync);
        RetryCommand = ReactiveCommand.Create(ResetForm);
    }

    #region Properties

    public string AuthorizationCode
    {
        get => _authorizationCode;
        set => this.RaiseAndSetIfChanged(ref _authorizationCode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string StatusMessageColor
    {
        get => _statusMessageColor;
        set => this.RaiseAndSetIfChanged(ref _statusMessageColor, value);
    }

    public bool IsVerifying
    {
        get => _isVerifying;
        set => this.RaiseAndSetIfChanged(ref _isVerifying, value);
    }

    public bool ShowRetryButton
    {
        get => _showRetryButton;
        set => this.RaiseAndSetIfChanged(ref _showRetryButton, value);
    }

    public bool IsVerified
    {
        get => _isVerified;
        private set => this.RaiseAndSetIfChanged(ref _isVerified, value);
    }

    #endregion

    #region Commands

    public ICommand VerifyCommand { get; }
    public ICommand RetryCommand { get; }

    #endregion

    #region Methods

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
            await _licenseService.VerifyAuthorizationCodeAsync(AuthorizationCode);

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

