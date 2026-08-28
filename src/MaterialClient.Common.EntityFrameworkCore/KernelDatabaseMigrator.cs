using MaterialClient.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace MaterialClient.Common.EntityFrameworkCore;

/// <summary>
///     Applies kernel migrations. Existing SQLite files may still contain Urban/Recycle tables
///     created by historical kernel migrations; those tables are not dropped.
/// </summary>
public static class KernelDatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider serviceProvider)
    {
        var unitOfWorkManager = serviceProvider.GetRequiredService<IUnitOfWorkManager>();
        var dbContextProvider = serviceProvider.GetRequiredService<IDbContextProvider<MaterialClientDbContext>>();

        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var dbContext = await dbContextProvider.GetDbContextAsync();
        await SqliteMigrationHistoryMigrator.RenameLegacyHistoryTableIfNeededAsync(
            dbContext,
            MaterialClientEfHistory.KernelTable);
        await dbContext.Database.MigrateAsync();
        await uow.CompleteAsync();
    }
}
