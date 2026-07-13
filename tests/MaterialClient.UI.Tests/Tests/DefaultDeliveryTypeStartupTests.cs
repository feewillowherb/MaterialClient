using System.Reactive.Subjects;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace MaterialClient.UI.Tests.Tests;

/// <summary>
///     Unit tests for 4.4: AttendedWeighingViewModel.InitializeOnFirstLoadAsync
///     calls SetDeliveryType with the saved default.
/// </summary>
public class AttendedWeighingViewModelStartupTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    private static (AttendedWeighingViewModel vm, IAttendedWeighingService attendedWeighingService)
        CreateViewModel(
            DeliveryType persistedDefault,
            out ISettingsService settingsService)
    {
        var attendedWeighingService = Substitute.For<IAttendedWeighingService>();

        // Simulate WeighingStateManager: BehaviorSubject seeded with Receiving
        var deliveryTypeSubject = new BehaviorSubject<DeliveryType>(DeliveryType.Receiving);
        attendedWeighingService.CurrentDeliveryType.Returns(deliveryTypeSubject.Value);
        attendedWeighingService
            .When(a => a.SetDeliveryType(Arg.Any<DeliveryType>()))
            .Do(call =>
            {
                var dt = call.Arg<DeliveryType>();
                deliveryTypeSubject.OnNext(dt);
                attendedWeighingService.CurrentDeliveryType.Returns(dt);
            });

        settingsService = Substitute.For<ISettingsService>();
        settingsService.GetSettingsAsync().Returns(new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings { DefaultDeliveryType = persistedDefault },
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings()));

        var testEventBus = new TestLocalEventBus();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<AttendedWeighingViewModel>>(
            _ => Substitute.For<ILogger<AttendedWeighingViewModel>>());
        var serviceProvider = services.BuildServiceProvider();

        var vm = new AttendedWeighingViewModel(
            Substitute.For<IWeighingMatchingService>(),
            serviceProvider,
            Substitute.For<ITruckScaleWeightService>(),
            attendedWeighingService,
            Substitute.For<IAuthenticationService>(),
            Substitute.For<ISoundDeviceService>(),
            settingsService,
            Substitute.For<ILprDeviceOnlineStatusService>(),
            Substitute.For<ISyncMaterialService>(),
            Substitute.For<IAttachmentService>(),
            testEventBus);

        return (vm, attendedWeighingService);
    }

    [Fact]
    public async Task InitializeOnFirstLoadAsync_CallsSetDeliveryType_WithSavedSending()
    {
        // Arrange
        var (vm, attendedWeighingService) = CreateViewModel(
            DeliveryType.Sending, out _);

        // Allow constructor's fire-and-forget InitializeOnFirstLoadAsync to complete
        await Task.Delay(300);

        // Assert — SetDeliveryType was called with Sending
        attendedWeighingService.Received().SetDeliveryType(DeliveryType.Sending);
        Assert.Equal(DeliveryType.Sending, attendedWeighingService.CurrentDeliveryType);
    }

    [Fact]
    public async Task InitializeOnFirstLoadAsync_CallsSetDeliveryType_WithSavedReceiving()
    {
        // Arrange
        var (vm, attendedWeighingService) = CreateViewModel(
            DeliveryType.Receiving, out _);

        await Task.Delay(300);

        // Assert — SetDeliveryType was still called (even though it's a no-op for Receiving)
        attendedWeighingService.Received().SetDeliveryType(DeliveryType.Receiving);
    }

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
    }
}
