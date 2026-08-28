using MaterialClient.Common.Services;
using MaterialClient.Common.Urban.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Urban.Services;

[Dependency(ReplaceServices = true)]
public class UrbanSettingsJsonStore : IUrbanSettingsJsonStore, ITransientDependency
{
    private readonly IRepository<UrbanSettingsRow, int> _repository;

    public UrbanSettingsJsonStore(IRepository<UrbanSettingsRow, int> repository)
    {
        _repository = repository;
    }

    [UnitOfWork]
    public virtual async Task<string?> GetJsonAsync()
    {
        var rows = await _repository.GetListAsync();
        return rows.FirstOrDefault()?.SettingsJson;
    }

    [UnitOfWork]
    public virtual async Task SaveJsonAsync(string json)
    {
        var rows = await _repository.GetListAsync();
        var existing = rows.FirstOrDefault();
        if (existing == null)
        {
            await _repository.InsertAsync(new UrbanSettingsRow { SettingsJson = json ?? string.Empty }, true);
            return;
        }

        existing.SettingsJson = json ?? string.Empty;
        await _repository.UpdateAsync(existing, true);
    }
}
