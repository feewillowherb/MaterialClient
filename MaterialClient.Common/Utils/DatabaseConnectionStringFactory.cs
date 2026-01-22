using System;
using System.IO;

namespace MaterialClient.Common.Utils;

/// <summary>
///     数据库连接字符串工厂类
///     用于将相对路径的连接字符串转换为绝对路径，确保应用从任何位置启动都能找到数据库文件
/// </summary>
public static class DatabaseConnectionStringFactory
{
    /// <summary>
    ///     修复数据库连接字符串，将相对路径转换为绝对路径
    ///     当应用通过注册表自动启动时，工作目录可能不是可执行文件目录，导致相对路径无法找到数据库文件
    /// </summary>
    /// <param name="connectionString">原始连接字符串，例如 "Data Source=MaterialClient.db"</param>
    /// <param name="baseDirectory">应用程序基础目录，通常使用 AppContext.BaseDirectory</param>
    /// <returns>修复后的连接字符串，如果输入是相对路径则返回绝对路径，否则返回原字符串</returns>
    public static string FixConnectionString(string? connectionString, string? baseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString ?? string.Empty;
        }

        // 如果连接字符串不包含 "Data Source="，直接返回
        if (!connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        // 提取数据库文件路径
        var dataSourceIndex = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
        var dataSourceValue = connectionString.Substring(dataSourceIndex + "Data Source=".Length).Trim();
        
        // 如果路径为空，返回原字符串
        if (string.IsNullOrWhiteSpace(dataSourceValue))
        {
            return connectionString;
        }

        // 如果已经是绝对路径，直接返回原字符串
        if (Path.IsPathRooted(dataSourceValue))
        {
            return connectionString;
        }

        // 使用提供的 baseDirectory 或默认使用 AppContext.BaseDirectory
        var appDirectory = baseDirectory ?? AppContext.BaseDirectory;
        
        // 将相对路径转换为绝对路径
        var dbAbsolutePath = Path.Combine(appDirectory, dataSourceValue);
        
        // 构建修复后的连接字符串
        var fixedConnectionString = connectionString.Substring(0, dataSourceIndex + "Data Source=".Length) + dbAbsolutePath;
        
        return fixedConnectionString;
    }

    /// <summary>
    ///     从连接字符串中提取数据库文件路径
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>数据库文件路径，如果无法提取则返回 null</returns>
    public static string? ExtractDatabasePath(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        if (!connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dataSourceIndex = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
        var dataSourceValue = connectionString.Substring(dataSourceIndex + "Data Source=".Length).Trim();
        
        return string.IsNullOrWhiteSpace(dataSourceValue) ? null : dataSourceValue;
    }
}