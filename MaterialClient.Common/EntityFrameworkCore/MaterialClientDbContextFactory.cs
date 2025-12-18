using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Data.Sqlite;

namespace MaterialClient.EFCore;

public class MaterialClientDbContextFactory : IDesignTimeDbContextFactory<MaterialClientDbContext>
{
    public MaterialClientDbContext CreateDbContext(string[] args)
    {
        // 设计时：尝试从环境变量获取密码，如果没有则使用默认连接
        // 如果设置了 SQLCIPHER_PASSWORD 环境变量，使用加密连接
        var password = Environment.GetEnvironmentVariable("SQLCIPHER_PASSWORD");
        var dataSource = Environment.GetEnvironmentVariable("SQLCIPHER_DATASOURCE") ?? "MaterialClient.db";

        string connectionString;
        if (!string.IsNullOrEmpty(password))
        {
            // 使用 SQLCipher 加密连接
            var connectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Password = password
            };
            connectionString = connectionStringBuilder.ToString();
        }
        else
        {
            // 使用默认连接（向后兼容）
            connectionString = $"Data Source={dataSource}";
        }

        var optionsBuilder = new DbContextOptionsBuilder<MaterialClientDbContext>();
        optionsBuilder.UseSqlite(connectionString)
            .EnableDetailedErrors() // 启用详细的错误信息
            .EnableSensitiveDataLogging(); // 启用敏感数据日志记录（包含参数值）

        return new MaterialClientDbContext(optionsBuilder.Options);
    }
}

