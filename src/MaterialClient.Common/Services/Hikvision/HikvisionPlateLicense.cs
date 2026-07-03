namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康 <c>sLicense</c> 解析结果：纯车牌号与车牌颜色。
/// </summary>
/// <param name="PlateNumber">已去除颜色前缀的车牌号</param>
/// <param name="PlateColor">车牌颜色（不含「色」字）；优先来自 sLicense 前缀，否则为 byColor 映射结果</param>
public record HikvisionPlateLicense(string PlateNumber, string? PlateColor);
