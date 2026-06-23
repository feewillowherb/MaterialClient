using System;

namespace MaterialClient.Urban.Dtos
{
    /// <summary>
    /// 日志文件信息 DTO
    /// </summary>
    public class LogFileDto
    {
        /// <summary>
        /// 文件名（如 MaterialClient-20250622.log）
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 相对路径（如 2025/06/22/）
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified { get; set; }
    }
}
