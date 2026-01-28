using System;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Events;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康威视车牌识别服务接口
/// </summary>
public interface IHikvisionLprService
{
    /// <summary>
    ///     连接到海康威视 LPR 设备
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>连接是否成功</returns>
    Task<bool> ConnectAsync(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    ///     开始监听车牌识别事件
    /// </summary>
    Task StartListeningAsync();

    /// <summary>
    ///     停止监听
    /// </summary>
    Task StopListeningAsync();

    /// <summary>
    ///     车牌识别事件流
    /// </summary>
    IObservable<LicensePlateRecognizedEvent> PlateRecognized { get; }

    /// <summary>
    ///     连接状态
    /// </summary>
    bool IsConnected { get; }
}
