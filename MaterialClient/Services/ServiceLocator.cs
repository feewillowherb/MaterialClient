using System;
using Microsoft.Extensions.DependencyInjection;

namespace MaterialClient.Services;

/// <summary>
/// Service locator for accessing ABP Autofac services from Avalonia UI layer
/// The IServiceProvider is wrapped by Autofac when using Volo.Abp.Autofac
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initialize with Autofac-wrapped service provider from ABP
    /// </summary>
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

    /// <summary>
    /// Get service by type from Autofac container
    /// </summary>
    public static object? GetService(Type serviceType)
    {
        if (_serviceProvider == null)
        {
            return null;
        }

        return _serviceProvider.GetService(serviceType);
    }

    /// <summary>
    /// Get required service by type from Autofac container
    /// </summary>
    public static object GetRequiredService(Type serviceType)
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize first.");
        }

        return _serviceProvider.GetRequiredService(serviceType);
    }
}

