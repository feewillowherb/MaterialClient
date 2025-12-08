using System.Collections.Generic;
using MaterialClient.Common.Api.Dtos;
using Refit;

namespace MaterialClient.Common.Api;

public interface IMaterialPlatformApi
{
    [Post("/api/Material/MaterialGoodList")]
    Task<HttpResult<List<MaterialDto>>> GetMaterialGoodListAsync(
        [Body] GetMaterialGoodListInput request,
        CancellationToken cancellationToken = default);
}

public record GetMaterialGoodListInput(
    string ProId,
    int UpdateTime
);