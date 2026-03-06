using Avalonia.Controls;
using Avalonia.Headless;

namespace MaterialClient.UI.Test;

/// <summary>
/// Helper class for creating and initializing controls in the headless test environment.
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// Creates and initializes a control in the headless environment.
    /// </summary>
    /// <typeparam name="T">Type of control to create</typeparam>
    /// <returns>Initialized control instance</returns>
    public static T CreateControl<T>() where T : Control, new()
    {
        var control = new T();
        // Ensure the control is properly initialized in the headless environment
        control.ApplyTemplate();
        return control;
    }

    /// <summary>
    /// Creates and initializes a control with the specified parameter.
    /// </summary>
    /// <typeparam name="T">Type of control to create</typeparam>
    /// <param name="parameter">Parameter to pass to the control</param>
    /// <returns>Initialized control instance</returns>
    public static T CreateControl<T, TParam>(TParam parameter) where T : Control, new()
    {
        var control = new T();
        control.ApplyTemplate();
        return control;
    }
}
