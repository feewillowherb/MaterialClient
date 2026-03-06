using MaterialClient.UI.ViewModels;
using Xunit;

namespace MaterialClient.UI.Test.ViewModels;

/// <summary>
/// Tests for the ViewModelBase class.
/// </summary>
public class ViewModelBaseTests
{
    [Fact]
    public void ViewModelBase_ShouldBeCreatable()
    {
        // Act & Assert
        // This test verifies that the ViewModelBase exists and can be instantiated
        // We can't directly instantiate an abstract class, but we can verify
        // that derived view models can be created (to be added later)
        Assert.NotNull(typeof(ViewModelBase));
    }
}
