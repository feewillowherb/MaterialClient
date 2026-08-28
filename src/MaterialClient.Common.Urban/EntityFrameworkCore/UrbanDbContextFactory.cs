using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MaterialClient.Common.Urban.EntityFrameworkCore;

public class UrbanDbContextFactory : IDesignTimeDbContextFactory<UrbanDbContext>
{
    public UrbanDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UrbanDbContext>();
        optionsBuilder.UseSqlite(
            "Data Source=:memory:",
            sqlite => sqlite.MigrationsHistoryTable(
                MaterialClient.Common.EntityFrameworkCore.MaterialClientEfHistory.UrbanTable));
        return new UrbanDbContext(optionsBuilder.Options);
    }
}
