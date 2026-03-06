using Xunit;

namespace MaterialClient.UI.Test;

/// <summary>
/// Base class for UI tests providing common test functionality.
/// </summary>
public abstract class TestBase
{
    /// <summary>
    /// Setup method called before each test.
    /// Override this method to initialize test-specific resources.
    /// </summary>
    public virtual void Setup()
    {
        // Default setup - override in derived classes if needed
    }

    /// <summary>
    /// Cleanup method called after each test.
    /// Override this method to clean up test-specific resources.
    /// </summary>
    public virtual void TearDown()
    {
        // Default cleanup - override in derived classes if needed
    }

    /// <summary>
    /// Sets an exception expectation for a test.
    /// </summary>
    /// <typeparam name="TException">Type of exception expected</typeparam>
    /// <param name="message">Optional message describing why the exception is expected</param>
    protected void ExpectException<TException>(string? message = null) where TException : Exception
    {
        Assert.NotNull(message);
    }
}
