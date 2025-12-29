namespace MaterialClient.Common.Api.Dtos;

/// <summary>
///     音响播报设置参数
/// </summary>
public class SettingParamsDto
{
    /// <summary>
    ///     本地IP地址
    /// </summary>
    public string LocalIP { get; set; } = string.Empty;

    /// <summary>
    ///     音响设备IP地址
    /// </summary>
    public string SoundIP { get; set; } = string.Empty;

    /// <summary>
    ///     音响设备序列号
    /// </summary>
    public string SoundSN { get; set; } = string.Empty;

    /// <summary>
    ///     音响音量（"0" 表示100）
    /// </summary>
    public string SoundVolume { get; set; } = "0";
}

/// <summary>
///     音响播报请求参数
/// </summary>
public class SpeakRequestDto
{
    /// <summary>
    ///     任务名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     设备序列号
    /// </summary>
    public string Sn { get; set; } = string.Empty;

    /// <summary>
    ///     请求类型
    /// </summary>
    public string Type { get; set; } = "req";

    /// <summary>
    ///     请求参数
    /// </summary>
    public SpeakUrlParamsDto Params { get; set; } = new();
}

/// <summary>
///     音响播报URL参数
/// </summary>
public class SpeakUrlParamsDto
{
    /// <summary>
    ///     用户ID
    /// </summary>
    public string Uid { get; set; } = "0";

    /// <summary>
    ///     音量
    /// </summary>
    public int Vol { get; set; }

    /// <summary>
    ///     URL列表
    /// </summary>
    public UrlDto[] Urls { get; set; } = Array.Empty<UrlDto>();

    /// <summary>
    ///     优先级
    /// </summary>
    public int Level { get; set; } = 10000;

    /// <summary>
    ///     任务名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     计数
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    ///     长度
    /// </summary>
    public int Length { get; set; } = 0;

    /// <summary>
    ///     类型
    /// </summary>
    public int Type { get; set; } = 0;

    /// <summary>
    ///     任务ID
    /// </summary>
    public string Tid { get; set; } = string.Empty;
}

/// <summary>
///     URL参数
/// </summary>
public class UrlDto
{
    /// <summary>
    ///     名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     是否使用UDP
    /// </summary>
    public bool Udp { get; set; } = true;

    /// <summary>
    ///     URI地址
    /// </summary>
    public string Uri { get; set; } = string.Empty;
}

/// <summary>
///     音响播报成功响应
/// </summary>
public class SpeakResponseDto
{
    /// <summary>
    ///     状态码（0表示成功）
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    ///     响应类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     响应消息
    /// </summary>
    public string Msg { get; set; } = string.Empty;

    /// <summary>
    ///     任务名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
///     音响播报失败响应
/// </summary>
public class SpeakFailResponseDto
{
    /// <summary>
    ///     结果码（-1表示失败）
    /// </summary>
    public int Result { get; set; }

    /// <summary>
    ///     错误消息
    /// </summary>
    public string Msg { get; set; } = string.Empty;
}


