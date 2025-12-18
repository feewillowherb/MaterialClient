using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Services;

/// <summary>
/// 数据库连接服务
/// 负责管理加密数据库连接字符串
/// </summary>
public interface IDatabaseConnectionService
{
    /// <summary>
    /// 获取数据库连接字符串
    /// 如果存在 LicenseInfo，使用 ProId 作为密码创建加密连接
    /// 否则使用默认连接（向后兼容）
    /// </summary>
    /// <returns>数据库连接字符串</returns>
    string GetConnectionString();

    /// <summary>
    /// 使用指定的 ProId 获取加密连接字符串
    /// </summary>
    /// <param name="proId">项目ID，用作数据库密码</param>
    /// <returns>加密的数据库连接字符串</returns>
    string GetEncryptedConnectionString(Guid proId);

    /// <summary>
    /// 获取数据库文件路径
    /// </summary>
    /// <returns>数据库文件路径</returns>
    string GetDatabaseFilePath();
}

/// <summary>
/// 数据库连接服务实现
/// </summary>
[AutoConstructor]
public partial class DatabaseConnectionService : DomainService, IDatabaseConnectionService
{
    private readonly IRepository<LicenseInfo, Guid> _licenseRepository;
    private readonly IConfiguration _configuration;

    public string GetConnectionString()
    {
        // 尝试从 LicenseInfo 获取 ProId
        var licenseInfo = _licenseRepository.FirstOrDefaultAsync().Result;

        if (licenseInfo != null)
        {
            // 使用 ProId 作为密码创建加密连接
            return GetEncryptedConnectionString(licenseInfo.ProjectId);
        }

        // 向后兼容：如果没有 LicenseInfo，使用默认连接
        return _configuration.GetConnectionString("Default")
               ?? "Data Source=MaterialClient.db";
    }

    public string GetEncryptedConnectionString(Guid proId)
    {
        var dataSource = GetDatabaseFilePath();

        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = proId.ToString() // 使用 ProId 的字符串表示作为密码
        };

        return connectionStringBuilder.ToString();
    }

    public string GetDatabaseFilePath()
    {
        var connectionString = _configuration.GetConnectionString("Default")
                               ?? "Data Source=MaterialClient.db";

        // 从连接字符串中提取 DataSource
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        // 如果是相对路径，转换为绝对路径（基于应用程序目录）
        if (!Path.IsPathRooted(dataSource))
        {
            var appDirectory = AppContext.BaseDirectory;
            dataSource = Path.Combine(appDirectory, dataSource);
        }

        return dataSource;
    }
}