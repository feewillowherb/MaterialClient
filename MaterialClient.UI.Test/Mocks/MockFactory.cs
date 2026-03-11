using NSubstitute;

namespace MaterialClient.UI.Test.Mocks;

/// <summary>
/// Factory class for creating mock objects for testing.
/// Note: Specific service mocks should be added after UI code migration.
/// </summary>
public static class MockFactory
{
    /// <summary>
    /// Creates a mock object of the specified type.
    /// </summary>
    /// <typeparam name="T">Type of object to mock</typeparam>
    /// <returns>Mock T instance</returns>
    public static T Create<T>() where T : class
    {
        return Substitute.For<T>();
    }
}
