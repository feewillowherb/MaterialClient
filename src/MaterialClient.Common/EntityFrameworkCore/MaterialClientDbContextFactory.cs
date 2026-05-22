using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MaterialClient.EFCore;

public class MaterialClientDbContextFactory : IDesignTimeDbContextFactory<MaterialClientDbContext>
{
    public MaterialClientDbContext CreateDbContext(string[] args)
    {
        // Use a simple connection string for design-time (migrations don't need actual database)
        // The connection string is only used to build the model, not to connect to a database
        var connectionString = "Data Source=:memory:";

        var optionsBuilder = new DbContextOptionsBuilder<MaterialClientDbContext>();
        optionsBuilder.UseSqlite(connectionString)
            .EnableDetailedErrors() // 启用详细的错误信息
            .EnableSensitiveDataLogging(); // 启用敏感数据日志记录（包含参数值）

        return new MaterialClientDbContext(optionsBuilder.Options);
    }
}