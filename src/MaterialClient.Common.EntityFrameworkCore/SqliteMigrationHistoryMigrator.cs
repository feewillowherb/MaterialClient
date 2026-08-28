using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MaterialClient.Common.EntityFrameworkCore;

public static class SqliteMigrationHistoryMigrator
{
    public static async Task RenameLegacyHistoryTableIfNeededAsync(
        DbContext dbContext,
        string targetHistoryTable,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection is not SqliteConnection sqlite)
            return;

        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            await sqlite.OpenAsync(cancellationToken);

        try
        {
            if (await TableExistsAsync(sqlite, targetHistoryTable, cancellationToken))
                return;

            if (!await TableExistsAsync(sqlite, MaterialClientEfHistory.LegacyTable, cancellationToken))
                return;

            await using var cmd = sqlite.CreateCommand();
            cmd.CommandText = $"ALTER TABLE \"{MaterialClientEfHistory.LegacyTable}\" RENAME TO \"{targetHistoryTable}\";";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (!wasOpen)
                await sqlite.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection sqlite,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        cmd.Parameters.AddWithValue("$name", tableName);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }
}
