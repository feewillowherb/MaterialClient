using System;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.Services;

/// <summary>
/// Service locator for accessing ABP services from Avalonia UI layer
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _serviceProvider;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static T? GetService<T>() where T : class
    {
        if (_serviceProvider == null)
        {
            return null;
        }

        return _serviceProvider.GetService<T>();
    }

    public static T GetRequiredService<T>() where T : class
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize first.");
        }

        return _serviceProvider.GetRequiredService<T>();
    }
}

