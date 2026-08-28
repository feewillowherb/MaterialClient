using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.UI.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace MaterialClient.UI.Tests.Tests;

/// <summary>
///     Unit tests for 4.3: SettingsWindowViewModel DefaultDeliveryType load/save.
/// </summary>
public class SettingsWindowViewModelDefaultDeliveryTypeTests
{
    private static SettingsWindowViewModel CreateViewModel(ISettingsService settingsService)
    {
        return new SettingsWindowViewModel(
            settingsService,
            Substitute.For<ITruckScaleWeightService>(),
            Substitute.For<IHikvisionService>(),
            Substitute.For<ITicketPrintingService>(),
            Substitute.For<ILogger<SettingsWindowViewModel>>(),
            Substitute.For<ISoundDeviceService>(),
            Substitute.For<ILprDeviceResolver>(),
            Substitute.For<ILocalEventBus>(),
            Substitute.For<ILicenseService>(),
            Substitute.For<IServiceProvider>());
    }

    private static SettingsEntity CreateSettingsEntity(DeliveryType deliveryType)
    {
        return new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings { DefaultDeliveryType = deliveryType },
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings());
    }

    [Fact]
    public async Task LoadAsync_PopulatesDefaultDeliveryType_Sending()
    {
        // Arrange
        var mockSettingsService = Substitute.For<ISettingsService>();
        mockSettingsService.GetSettingsAsync().Returns(CreateSettingsEntity(DeliveryType.Sending));
        mockSettingsService.GetWeighingModeAsync().Returns(WeighingMode.Standard);

        var vm = CreateViewModel(mockSettingsService);

        // Allow fire-and-forget LoadSettingsAsync in constructor to complete
        await Task.Delay(200);

        // Assert
        Assert.Equal(DeliveryType.Sending, vm.DefaultDeliveryType);
    }

    [Fact]
    public async Task LoadAsync_PopulatesDefaultDeliveryType_Receiving()
    {
        // Arrange
        var mockSettingsService = Substitute.For<ISettingsService>();
        mockSettingsService.GetSettingsAsync().Returns(CreateSettingsEntity(DeliveryType.Receiving));
        mockSettingsService.GetWeighingModeAsync().Returns(WeighingMode.Standard);

        var vm = CreateViewModel(mockSettingsService);

        await Task.Delay(200);

        // Assert
        Assert.Equal(DeliveryType.Receiving, vm.DefaultDeliveryType);
    }

    [Fact]
    public async Task SaveCommand_WritesDefaultDeliveryType_ToSettings()
    {
        // Arrange
        var savedSettings = new List<SettingsEntity>();
        var mockSettingsService = Substitute.For<ISettingsService>();

        mockSettingsService.GetSettingsAsync().Returns(CreateSettingsEntity(DeliveryType.Receiving));
        mockSettingsService.GetWeighingModeAsync().Returns(WeighingMode.Standard);

        mockSettingsService
            .SaveSettingsAsync(Arg.Do<SettingsEntity>(s => savedSettings.Add(s)))
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel(mockSettingsService);
        await Task.Delay(200);

        // Act — change to Sending and execute SaveCommand
        vm.DefaultDeliveryType = DeliveryType.Sending;
        await vm.SaveCommand.Execute().ToTask();

        // Assert
        Assert.Single(savedSettings);
        Assert.Equal(DeliveryType.Sending, savedSettings[0].SystemSettings.DefaultDeliveryType);
    }

    [Fact]
    public async Task SaveCommand_PreservesDeliveryType_WhenUnchanged()
    {
        // Arrange
        var savedSettings = new List<SettingsEntity>();
        var mockSettingsService = Substitute.For<ISettingsService>();

        mockSettingsService.GetSettingsAsync().Returns(CreateSettingsEntity(DeliveryType.Sending));
        mockSettingsService.GetWeighingModeAsync().Returns(WeighingMode.Standard);

        mockSettingsService
            .SaveSettingsAsync(Arg.Do<SettingsEntity>(s => savedSettings.Add(s)))
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel(mockSettingsService);
        await Task.Delay(200);

        // Act — save without changing (still Sending from load)
        await vm.SaveCommand.Execute().ToTask();

        // Assert
        Assert.Single(savedSettings);
        Assert.Equal(DeliveryType.Sending, savedSettings[0].SystemSettings.DefaultDeliveryType);
    }
}
