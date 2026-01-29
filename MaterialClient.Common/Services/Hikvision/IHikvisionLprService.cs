using System;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Events;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康威视车牌识别服务接口
///     支持多设备管理，可动态添加/更新设备和检查设备在线状态
/// </summary>
public interface IHikvisionLprService
{
    /// <summary>
    ///     添加或更新设备配置
    ///     如果设备已存在则更新配置，否则添加新设备
    /// </summary>
    /// <param name="config">设备配置</param>
    void AddOrUpdateDevice(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     检查设备是否在线
    ///     尝试连接设备并验证连接状态
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>设备是否在线</returns>
    bool IsOnline(LicensePlateRecognitionConfig config);

    /// <summary>
    ///     启动监听服务
    ///     使用指定的本地 IP 和端口启动监听，可接收多个海康设备的车牌识别数据
    /// </summary>
    /// <param name="listenLocalIp">监听本地 IP 地址</param>
    /// <param name="listenLocalPort">监听本地端口</param>
    /// <returns>启动是否成功</returns>
    Task<bool> StartAsync(string listenLocalIp, int listenLocalPort);

    /// <summary>
    ///     停止监听服务
    /// </summary>
    Task StopAsync();

    /// <summary>
    ///     车牌识别事件流
    /// </summary>
    IObservable<LicensePlateRecognizedEvent> PlateRecognized { get; }
}
