using System;
using System.Threading.Tasks;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Services;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

public partial class ProviderEditWindowViewModel : ViewModelBase, ITransientDependency
{
    private readonly IProviderService _providerService;

    public ProviderEditWindowViewModel(IProviderService providerService)
    {
        _providerService = providerService;
    }

    public ProviderDto? Result { get; private set; }

    [Reactive] public int Id { get; private set; }
    [Reactive] public string ProviderName { get; set; } = string.Empty;
    [Reactive] public string? ContectName { get; set; }
    [Reactive] public string? ContectPhone { get; set; }
    /// <summary>收货地址（本地专用，§2.2 consigneeAddress 数据源）</summary>
    [Reactive] public string? Address { get; set; }

    public void Initialize(ProviderDto provider)
    {
        Id = provider.Id;
        ProviderName = provider.ProviderName;
        ContectName = provider.ContactName;
        ContectPhone = provider.ContactPhone;
        Address = provider.Address;
        Result = null;
    }

    [ReactiveCommand]
    private async Task SaveAsync()
    {
        Result = await _providerService.UpdateProviderAsync(
            Id,
            ProviderName,
            ContectName,
            ContectPhone,
            Address);
    }

    [ReactiveCommand]
    private void Cancel()
    {
        Result = null;
    }
}

