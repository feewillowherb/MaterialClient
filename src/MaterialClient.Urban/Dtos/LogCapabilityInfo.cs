namespace MaterialClient.Urban.Dtos
{
    /// <summary>
    /// 日志能力信息 DTO
    /// </summary>
    public class LogCapabilityInfo
    {
        /// <summary>
        /// 是否支持日志拉取
        /// </summary>
        public bool SupportsLogPull { get; set; } = true;

        /// <summary>
        /// 日志目录绝对路径
        /// </summary>
        public string LogDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 最大并发下载数
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 3;

        /// <summary>
        /// API 监听端口
        /// </summary>
        public int ApiPort { get; set; } = 5900;

        /// <summary>
        /// 日志格式版本
        /// </summary>
        public string LogFormatVersion { get; set; } = "1.0";
    }
}
