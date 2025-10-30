using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MaterialClient.EFCore;

public class MyDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string encryptionPassword = "Your_New_Strong_Key";

        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = "my_official_encrypted_data.db",
            Password = encryptionPassword
        }.ToString();

        optionsBuilder.UseSqlite(connectionString);
    }
}