using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MaterialClient.Common.Services.Authentication;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.UI.ViewModels;

/// <summary>
///     登录窗口 ViewModel
/// </summary>
public partial class LoginWindowViewModel : ReactiveViewModelBase, ITransientDependency
{
    private readonly IAuthenticationService _authenticationService;

    [Reactive] private string _errorMessage = string.Empty;

    [Reactive] private bool _hasError;

    [Reactive] private bool _isLoggingIn;

    [Reactive] private bool _isLoginSuccessful;

    [Reactive] private string _password = string.Empty;

    [Reactive] private bool _rememberMe;

    [Reactive] private bool _showRetryButton;

    [Reactive] private string _username = string.Empty;

    public LoginWindowViewModel(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;

        this.WhenAnyValue(x => x.ErrorMessage)
            .Select(msg => !string.IsNullOrEmpty(msg))
            .Subscribe(hasError => HasError = hasError);

        _ = LoadSavedCredentialsAsync();
    }

    [ReactiveCommand]
    private async Task LoginAsync()
    {
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
            await _authenticationService.LoginAsync(Username, Password, RememberMe);

            IsLoginSuccessful = true;
            ErrorMessage = string.Empty;
            ShowRetryButton = false;
        }
        catch (BusinessException ex)
        {
            HandleLoginError(ex.Message);
        }
        catch (Exception ex)
        {
            HandleLoginError($"登录失败：{ex.Message}");
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    [ReactiveCommand]
    private void Retry()
    {
        ResetErrorState();
    }

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
            // ignore
        }
    }

    private void HandleLoginError(string errorMessage)
    {
        IsLoginSuccessful = false;

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

    public void ResetLoginForm()
    {
        Username = string.Empty;
        Password = string.Empty;
        RememberMe = false;
        ResetErrorState();
    }

    private void ResetErrorState()
    {
        ErrorMessage = string.Empty;
        ShowRetryButton = false;
        IsLoginSuccessful = false;
    }
}
