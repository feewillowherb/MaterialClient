using System;
using System.Threading.Tasks;
using MaterialClient.Common.Services.Authentication;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Volo.Abp;

namespace MaterialClient.ViewModels;

/// <summary>
/// 登录窗口 ViewModel
/// </summary>
public partial class LoginWindowViewModel : ReactiveViewModelBase
{
    private readonly IAuthenticationService _authenticationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private bool _isLoggingIn;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _showRetryButton;

    [ObservableProperty]
    private bool _isLoginSuccessful;

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                HasError = !string.IsNullOrEmpty(value);
            }
        }
    }

    public LoginWindowViewModel(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;

        // Load saved credentials
        _ = LoadSavedCredentialsAsync();
    }

    #region Commands

    [RelayCommand]
    private async Task LoginAsync()
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowError("请输入用户名");
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ShowError("请输入密码");
            return;
        }

        IsLoggingIn = true;
        ErrorMessage = string.Empty;
        ShowRetryButton = false;

        try
        {
            // Call authentication service for login operation
            await _authenticationService.LoginAsync(Username, Password, RememberMe);

            // Success
            IsLoginSuccessful = true;
            ErrorMessage = string.Empty;
            ShowRetryButton = false;

            // Window will be closed by the caller
        }
        catch (BusinessException ex)
        {
            // Business exception from authentication service
            HandleLoginError(ex.Message);
        }
        catch (Exception ex)
        {
            // Unexpected exception
            HandleLoginError($"登录失败：{ex.Message}");
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    [RelayCommand]
    private void Retry()
    {
        ResetErrorState();
    }

    #endregion

    #region Methods

    private async Task LoadSavedCredentialsAsync()
    {
        try
        {
            var savedCredential = await _authenticationService.GetSavedCredentialAsync();
            if (savedCredential.HasValue)
            {
                Username = savedCredential.Value.username;
                Password = savedCredential.Value.password;
                RememberMe = true;
            }
        }
        catch
        {
            // Ignore errors when loading saved credentials
        }
    }

    private void HandleLoginError(string errorMessage)
    {
        IsLoginSuccessful = false;

        // Check if it's a network error
        if (errorMessage.Contains("网络") || errorMessage.Contains("连接"))
        {
            ErrorMessage = "网络连接失败，请检查网络设置";
            ShowRetryButton = true;
        }
        else
        {
            ErrorMessage = errorMessage;
            ShowRetryButton = false;
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        ShowRetryButton = false;
    }

    private void ResetErrorState()
    {
        ErrorMessage = string.Empty;
        ShowRetryButton = false;
        IsLoginSuccessful = false;
    }

    #endregion
}

