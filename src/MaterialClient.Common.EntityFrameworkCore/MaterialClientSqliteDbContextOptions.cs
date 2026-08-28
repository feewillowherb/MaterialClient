using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace MaterialClient.Common.EntityFrameworkCore;

public static class MaterialClientSqliteDbContextOptions
{
    public static void Apply(
        DbContextOptionsBuilder options,
        DbConnection? existingConnection,
        string connectionString,
        string migrationsHistoryTable)
    {
        var builder = existingConnection is not null
            ? options.UseSqlite(
                existingConnection,
                sqlite => sqlite.MigrationsHistoryTable(migrationsHistoryTable))
            : options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsHistoryTable(migrationsHistoryTable));

        builder.EnableDetailedErrors().EnableSensitiveDataLogging();
    }
}
