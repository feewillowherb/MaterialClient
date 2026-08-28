using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MaterialClient.Common.Recycle.EntityFrameworkCore;

public class RecycleDbContextFactory : IDesignTimeDbContextFactory<RecycleDbContext>
{
    public RecycleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RecycleDbContext>();
        optionsBuilder.UseSqlite(
            "Data Source=:memory:",
            sqlite => sqlite.MigrationsHistoryTable(
                MaterialClient.Common.EntityFrameworkCore.MaterialClientEfHistory.RecycleTable));
        return new RecycleDbContext(optionsBuilder.Options);
    }
}
