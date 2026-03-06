using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.UI.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReactiveUI;
using Shouldly;
using System.Reactive;
using Xunit;

namespace MaterialClient.UI.Test.ViewModels;

/// <summary>
/// Tests for SettingsWindowViewModel.
/// Note: Simplified tests due to complex dependencies and UI interactions.
/// </summary>
public class SettingsWindowViewModelTests
{
    private readonly ISettingsService _mockSettingsService;
    private readonly ITruckScaleWeightService _mockWeightService;
    private readonly IHikvisionService _mockHikvisionService;
    private readonly ITicketPrintingService _mockPrintingService;
    private readonly ILogger<SettingsWindowViewModel> _mockLogger;
    private readonly ISoundDeviceService _mockSoundService;
    private readonly ILprDeviceResolver _mockLprResolver;

    public SettingsWindowViewModelTests()
    {
        _mockSettingsService = Substitute.For<ISettingsService>();
        _mockWeightService = Substitute.For<ITruckScaleWeightService>();
        _mockHikvisionService = Substitute.For<IHikvisionService>();
        _mockPrintingService = Substitute.For<ITicketPrintingService>();
        _mockLogger = Substitute.For<ILogger<SettingsWindowViewModel>>();
        _mockSoundService = Substitute.For<ISoundDeviceService>();
        _mockLprResolver = Substitute.For<ILprDeviceResolver>();
    }

    [Fact]
    public void Constructor_ShouldInitializePropertyDefaults()
    {
        // Arrange & Act
        var viewModel = new SettingsWindowViewModel(
            _mockSettingsService,
            _mockWeightService,
            _mockHikvisionService,
            _mockPrintingService,
            _mockLogger,
            _mockSoundService,
            _mockLprResolver);

        // Assert
        viewModel.ScaleSerialPort.ShouldNotBeNull();
        viewModel.ScaleBaudRate.ShouldBe("9600");
        viewModel.ScaleCommunicationMethod.ShouldBe("TF0");
        viewModel.ScaleUnit.ShouldBe(ScaleUnit.Ton);
        viewModel.ScaleType.ShouldBe(ScaleType.Yaohua);
        viewModel.MinWeightThreshold.ShouldBe(0.5m);
        viewModel.WeightStabilityThreshold.ShouldBe(0.05m);
        viewModel.StabilityWindowMs.ShouldBe(3000);
        viewModel.StabilityCheckIntervalMs.ShouldBe(200);
        viewModel.MaxIntervalMinutes.ShouldBe(300);
        viewModel.MinWeightDiff.ShouldBe(1m);
    }

    [Fact]
    public void Constructor_ShouldInitializeOptionsCollections()
    {
        // Arrange & Act
        var viewModel = new SettingsWindowViewModel(
            _mockSettingsService,
            _mockWeightService,
            _mockHikvisionService,
            _mockPrintingService,
            _mockLogger,
            _mockSoundService,
            _mockLprResolver);

        // Assert
        viewModel.ScaleUnitOptions.Count.ShouldBe(5);
        viewModel.ScaleUnitOptions.ShouldContain(ScaleUnit.Kg);
        viewModel.ScaleUnitOptions.ShouldContain(ScaleUnit.Ton);

        viewModel.ScaleTypeOptions.Count.ShouldBe(2);
        viewModel.ScaleTypeOptions.ShouldContain(ScaleType.Yaohua);
        viewModel.ScaleTypeOptions.ShouldContain(ScaleType.DingSong);

        viewModel.StreamTypeOptions.Count.ShouldBe(2);
        viewModel.StreamTypeOptions.ShouldContain(StreamType.Substream);
        viewModel.StreamTypeOptions.ShouldContain(StreamType.Mainstream);
    }

    [Fact]
    public void ShowHikvisionLprFields_ShouldBeTrueWhenHikvision()
    {
        // Arrange & Act
        var viewModel = new SettingsWindowViewModel(
            _mockSettingsService,
            _mockWeightService,
            _mockHikvisionService,
            _mockPrintingService,
            _mockLogger,
            _mockSoundService,
            _mockLprResolver);

        // Assert
        viewModel.ShowHikvisionLprFields.ShouldBeTrue();
    }

    [Fact]
    public void ShowHikvisionLprFields_ShouldBeFalseWhenNotHikvision()
    {
        // Arrange
        var viewModel = new SettingsWindowViewModel(
            _mockSettingsService,
            _mockWeightService,
            _mockHikvisionService,
            _mockPrintingService,
            _mockLogger,
            _mockSoundService,
            _mockLprResolver);

        // Act
        viewModel.LprDeviceType = LprDeviceType.LprAllInOne;

        // Assert
        viewModel.ShowHikvisionLprFields.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_ShouldRaiseCloseRequestedEvent()
    {
        // Arrange
        var viewModel = new SettingsWindowViewModel(
            _mockSettingsService,
            _mockWeightService,
            _mockHikvisionService,
            _mockPrintingService,
            _mockLogger,
            _mockSoundService,
            _mockLprResolver);

        var closeRequestedRaised = false;
        viewModel.CloseRequested += (s, e) => closeRequestedRaised = true;

        // Act
        viewModel.CancelCommand.Execute(Unit.Default);

        // Assert
        closeRequestedRaised.ShouldBeTrue();
    }
}
