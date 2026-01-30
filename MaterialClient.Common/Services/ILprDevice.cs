using MaterialClient.Common.Configuration;
using MaterialClient.Common.Events;
using System.Reactive;

namespace MaterialClient.Common.Services;

/// <summary>
///     统一的 LPR 设备接口,提供主动抓拍能力
/// </summary>
/// <remarks>
///     此接口定义了所有 LPR 设备类型应实现的主动抓拍标准。
///     不同厂商的设备可能有不同的支持程度:
///     - 海康威视: 支持,使用 NET_DVR_ContinuousShoot SDK 接口
///     - LprAllInOne: 支持,使用标志位轮询机制
///     - 华夏智信: 不支持,厂商限制
/// </remarks>
public interface ILprDevice
{
    /// <summary>
    ///     主动触发车牌识别抓拍
    /// </summary>
    /// <param name="config">设备配置</param>
    /// <returns>
    ///     可观察的车牌识别事件流。
    ///     如果设备不支持主动抓拍,应返回空流或抛出 NotSupportedException。
    /// </returns>
    /// <remarks>
    ///     实现应处理:
    ///     <list type="bullet">
    ///         <item>设备登录/认证</item>
    ///         <item>触发抓拍命令</item>
    ///         <item>等待识别结果(带超时)</item>
    ///         <item>错误处理(网络超时、设备离线、SDK 调用失败)</item>
    ///     </list>
    ///     <para>
    ///         返回的 IObservable 应该在以下情况下完成:
    ///         <list type="bullet">
    ///             <item>成功识别到车牌(OnNext)</item>
    ///             <item>发生错误(OnError)</item>
    ///             <item>超时或取消(OnCompleted)</item>
    ///         </list>
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///         var config = new LicensePlateRecognitionConfig
    ///         {
    ///             Name = "Hikvision-LPR-1",
    ///             Ip = "192.168.1.100",
    ///             Port = "8000",
    ///             UserName = "admin",
    ///             Password = "12345"
    ///         };
    ///
    ///         lprDevice.TriggerCaptureAsync(config)
    ///             .Timeout(TimeSpan.FromSeconds(30))
    ///             .Subscribe(
    ///                 result => Console.WriteLine($"识别到车牌: {result.PlateNumber}"),
    ///                 ex => Console.WriteLine($"抓拍失败: {ex.Message}"),
    ///                 () => Console.WriteLine("抓拍完成")
    ///             );
    ///     </code>
    /// </example>
    IObservable<LicensePlateRecognizedEvent> TriggerCaptureAsync(
        LicensePlateRecognitionConfig config);

    /// <summary>
    ///     设备是否支持主动抓拍
    /// </summary>
    /// <value>
    ///     如果设备支持通过应用触发抓拍,则为 true;
    ///     如果设备仅支持被动捕获(设备推送)或厂商限制,则为 false。
    /// </value>
    /// <remarks>
    ///     在调用 <see cref="TriggerCaptureAsync"/> 之前,应先检查此属性。
    ///     如果为 false,调用 TriggerCaptureAsync 将会抛出 NotSupportedException。
    /// </remarks>
    bool SupportsActiveCapture { get; }
}
