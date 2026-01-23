using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     Service interface for managing Windows auto-start functionality
/// </summary>
public interface IWindowsAutoStartService
{
    /// <summary>
    ///     Enable auto-start in Windows registry
    /// </summary>
    Task EnableAutoStartAsync();

    /// <summary>
    ///     Disable auto-start in Windows registry
    /// </summary>
    Task DisableAutoStartAsync();

    /// <summary>
    ///     Check if auto-start is currently enabled in registry
    /// </summary>
    Task<bool> IsAutoStartEnabledAsync();
}

/// <summary>
///     Service for managing Windows auto-start functionality via registry
/// </summary>
public class WindowsAutoStartService : IWindowsAutoStartService, ITransientDependency
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _registryValueName;
    private readonly string _executablePath;
    private readonly ILogger<WindowsAutoStartService>? _logger;

    public WindowsAutoStartService(ILogger<WindowsAutoStartService>? logger = null)
    {
        _logger = logger;
        _registryValueName = "MaterialClient";
        
        // Get executable path - prefer Environment.ProcessPath (available in .NET 6+)
        // Fallback to Assembly.Location for compatibility
        _executablePath = Environment.ProcessPath ?? 
            System.Reflection.Assembly.GetExecutingAssembly().Location;
    }

    /// <summary>
    ///     Enable auto-start in Windows registry
    /// </summary>
    public async Task EnableAutoStartAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null)
                {
                    _logger?.LogWarning("Failed to open registry key: {RegistryKeyPath}", RegistryKeyPath);
                    return;
                }

                key.SetValue(_registryValueName, _executablePath);
                _logger?.LogInformation("Auto-start enabled in registry. Value: {RegistryValueName}, Path: {ExecutablePath}", 
                    _registryValueName, _executablePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogWarning(ex, "Registry permission denied when enabling auto-start");
                // Don't throw - allow application to continue
            }
            catch (System.Security.SecurityException ex)
            {
                _logger?.LogWarning(ex, "Security exception when enabling auto-start");
                // Don't throw - allow application to continue
            }
            catch (System.IO.IOException ex)
            {
                _logger?.LogWarning(ex, "Registry unavailable when enabling auto-start");
                // Don't throw - allow application to continue
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error when enabling auto-start in registry");
                // Don't throw - allow application to continue
            }
        });
    }

    /// <summary>
    ///     Disable auto-start in Windows registry
    /// </summary>
    public async Task DisableAutoStartAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null)
                {
                    _logger?.LogWarning("Failed to open registry key: {RegistryKeyPath}", RegistryKeyPath);
                    return;
                }

                key.DeleteValue(_registryValueName, throwOnMissingValue: false);
                _logger?.LogInformation("Auto-start disabled in registry. Value removed: {RegistryValueName}", 
                    _registryValueName);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogWarning(ex, "Registry permission denied when disabling auto-start");
                // Don't throw - allow application to continue
            }
            catch (System.Security.SecurityException ex)
            {
                _logger?.LogWarning(ex, "Security exception when disabling auto-start");
                // Don't throw - allow application to continue
            }
            catch (System.IO.IOException ex)
            {
                _logger?.LogWarning(ex, "Registry unavailable when disabling auto-start");
                // Don't throw - allow application to continue
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error when disabling auto-start in registry");
                // Don't throw - allow application to continue
            }
        });
    }

    /// <summary>
    ///     Check if auto-start is currently enabled in registry
    /// </summary>
    public async Task<bool> IsAutoStartEnabledAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                if (key == null)
                {
                    _logger?.LogWarning("Failed to open registry key for reading: {RegistryKeyPath}", RegistryKeyPath);
                    return false; // Conservative default
                }

                var value = key.GetValue(_registryValueName);
                if (value == null)
                {
                    return false;
                }

                var registryPath = value.ToString();
                var isEnabled = registryPath == _executablePath;
                
                if (isEnabled)
                {
                    _logger?.LogDebug("Auto-start is enabled in registry. Path matches: {ExecutablePath}", _executablePath);
                }
                else if (registryPath != null)
                {
                    _logger?.LogWarning("Auto-start registry entry exists but path mismatch. Registry: {RegistryPath}, Expected: {ExecutablePath}", 
                        registryPath, _executablePath);
                }

                return isEnabled;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogWarning(ex, "Registry permission denied when checking auto-start status");
                return false; // Conservative default
            }
            catch (System.Security.SecurityException ex)
            {
                _logger?.LogWarning(ex, "Security exception when checking auto-start status");
                return false; // Conservative default
            }
            catch (System.IO.IOException ex)
            {
                _logger?.LogWarning(ex, "Registry unavailable when checking auto-start status");
                return false; // Conservative default
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error when checking auto-start status in registry");
                return false; // Conservative default
            }
        });
    }
}