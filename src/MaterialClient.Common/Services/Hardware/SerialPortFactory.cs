using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Hardware;

/// <summary>
///     Factory interface for creating ISerialPort instances
///     Allows dependency injection and mocking for unit testing
/// </summary>
public interface ISerialPortFactory
{
    /// <summary>
    ///     Creates a new ISerialPort instance
    /// </summary>
    /// <returns>A new ISerialPort instance</returns>
    ISerialPort Create();
}

/// <summary>
///     Factory implementation for creating ISerialPort instances
///     Registered as singleton in ABP dependency injection container
/// </summary>
public class SerialPortFactory : ISerialPortFactory, ISingletonDependency
{
    /// <inheritdoc />
    public ISerialPort Create()
    {
        return new SerialPortWrapper();
    }
}
