using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;
using MaterialClient.UI.ViewModels;
using System.Reactive;

namespace MaterialClient.UI.Test.ViewModels;

/// <summary>
/// Tests for MainWindowViewModel.
/// </summary>
public class MainWindowViewModelTests
{
    private readonly IServiceProvider _mockServiceProvider;

    public MainWindowViewModelTests()
    {
        _mockServiceProvider = Substitute.For<IServiceProvider>();
    }

    [Fact]
    public void Constructor_ShouldInitializeGreeting()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel(_mockServiceProvider);

        // Assert
        viewModel.Greeting.ShouldBe("Welcome to Avalonia!");
    }

    [Fact(Skip = "NSubstitute 无法代理 AttendedWeighingWindow（存在非可见重写方法 HandleClosing）。可改为集成测试或手动 Stub。")]
    public void OpenAttendedWeighing_ShouldResolveAndShowWindow()
    {
        // Arrange
        var mockWindow = Substitute.For<MaterialClient.UI.Views.AttendedWeighing.AttendedWeighingWindow>();
        _mockServiceProvider.GetRequiredService<MaterialClient.UI.Views.AttendedWeighing.AttendedWeighingWindow>()
            .Returns(mockWindow);

        var viewModel = new MainWindowViewModel(_mockServiceProvider);

        // Act
        viewModel.OpenAttendedWeighingCommand.Execute(Unit.Default);

        // Assert
        _mockServiceProvider.Received(1).GetRequiredService<MaterialClient.UI.Views.AttendedWeighing.AttendedWeighingWindow>();
        mockWindow.Received(1).Show();
    }
}
