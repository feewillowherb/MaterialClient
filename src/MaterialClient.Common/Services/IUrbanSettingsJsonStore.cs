using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services;

/// <summary>
///     Urban aggregated settings JSON. Kernel <c>Settings.UrbanSettingsJson</c> is ignored by EF.
///     Standard host no-ops; Urban host reads/writes the Urban table.
/// </summary>
public interface IUrbanSettingsJsonStore : ITransientDependency
{
    Task<string?> GetJsonAsync();

    Task SaveJsonAsync(string json);
}

public class NullUrbanSettingsJsonStore : IUrbanSettingsJsonStore, ITransientDependency
{
    public Task<string?> GetJsonAsync() => Task.FromResult<string?>(null);

    public Task SaveJsonAsync(string json) => Task.CompletedTask;
}
