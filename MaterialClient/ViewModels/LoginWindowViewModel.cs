using System;
using System.Threading.Tasks;
using System.Windows.Input;
using MaterialClient.Common.Services.Authentication;
using ReactiveUI;
using Volo.Abp;

namespace MaterialClient.ViewModels;

/// <summary>
/// 登录窗口 ViewModel
/// </summary>
public class LoginWindowViewModel : ReactiveViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _rememberMe = false;
    private bool _isLoggingIn = false;
    private string _errorMessage = string.Empty;
    private bool _hasError = false;
    private bool _showRetryButton = false;
    private bool _isLoginSuccessful = false;

    public LoginWindowViewModel()
    {
        // Design-time constructor
        _authenticationService = null!;
        LoginCommand = ReactiveCommand.Create(() => { });
        RetryCommand = ReactiveCommand.Create(() => { });
    }

    public LoginWindowViewModel(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
        
        // Create commands
        LoginCommand = ReactiveCommand.CreateFromTask(LoginAsync);
        RetryCommand = ReactiveCommand.Create(ResetErrorState);
        
        // Load saved credentials
        _ = LoadSavedCredentialsAsync();
    }

    #region Properties

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => this.RaiseAndSetIfChanged(ref _rememberMe, value);
    }

    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        set => this.RaiseAndSetIfChanged(ref _isLoggingIn, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            HasError = !string.IsNullOrEmpty(value);
        }
    }

    public bool HasError
    {
        get => _hasError;
        set => this.RaiseAndSetIfChanged(ref _hasError, value);
    }

    public bool ShowRetryButton
    {
        get => _showRetryButton;
        set => this.RaiseAndSetIfChanged(ref _showRetryButton, value);
    }

    public bool IsLoginSuccessful
    {
        get => _isLoginSuccessful;
        private set => this.RaiseAndSetIfChanged(ref _isLoginSuccessful, value);
    }

    #endregion

    #region Commands

    public ICommand LoginCommand { get; }
    public ICommand RetryCommand { get; }

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
            // Call authentication service to login
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

