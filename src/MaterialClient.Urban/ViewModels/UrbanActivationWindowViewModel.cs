using System;
using System.Reactive;
using System.Threading.Tasks;
using MaterialClient.Common.Services.Authentication;
using ReactiveUI;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.ViewModels;

public class UrbanActivationWindowViewModel : ReactiveObject, ITransientDependency
{
    private readonly ILicenseService _licenseService;
    private readonly IMachineCodeService _machineCodeService;

    private string _accessCode = string.Empty;
    private string _machineCode = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;

    public UrbanActivationWindowViewModel(
        ILicenseService licenseService,
        IMachineCodeService machineCodeService)
    {
        _licenseService = licenseService;
        _machineCodeService = machineCodeService;
        MachineCode = _machineCodeService.GetMachineCode();
        ActivateCommand = ReactiveCommand.CreateFromTask(ActivateAsync);
    }

    public string AccessCode
    {
        get => _accessCode;
        set => this.RaiseAndSetIfChanged(ref _accessCode, value);
    }

    public string MachineCode
    {
        get => _machineCode;
        set => this.RaiseAndSetIfChanged(ref _machineCode, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public ReactiveCommand<Unit, bool> ActivateCommand { get; }

    public event EventHandler? ActivationSucceeded;

    private async Task<bool> ActivateAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await _licenseService.ActivateUrbanAsync(AccessCode);
            ActivationSucceeded?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (BusinessException ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"激活失败: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
