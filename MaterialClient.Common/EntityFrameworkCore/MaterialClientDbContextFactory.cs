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
        optionsBuilder.UseSqlite(connectionString);

        return new MaterialClientDbContext(optionsBuilder.Options);
    }
}

