using MaterialClient.Common.Api.Dtos;
using Refit;

namespace MaterialClient.Common.Api;

public interface IMaterialPlatformApi
{
    [Post("/api/Material/MaterialGoodList")]
    Task<HttpResult<dynamic>> GetMaterialGoodListAsync(
        [Body] dynamic request,
        CancellationToken cancellationToken = default);
}