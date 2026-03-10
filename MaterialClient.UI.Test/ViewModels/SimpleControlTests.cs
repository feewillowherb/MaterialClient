using Avalonia.Controls;
using Xunit;

namespace MaterialClient.UI.Test.ViewModels;

/// <summary>
/// Simple tests to verify the headless test infrastructure is working.
/// </summary>
public class SimpleControlTests
{
    [Fact]
    public void Button_ShouldBeCreatable()
    {
        // Arrange & Act
        var button = new Button();

        // Assert
        Assert.NotNull(button);
        Assert.True(button.Content == null || (button.Content as string) == "");
    }

    [Fact]
    public void TextBlock_ShouldBeCreatable()
    {
        // Arrange & Act
        var textBlock = new TextBlock { Text = "Test" };

        // Assert
        Assert.NotNull(textBlock);
        Assert.Equal("Test", textBlock.Text);
    }
}
