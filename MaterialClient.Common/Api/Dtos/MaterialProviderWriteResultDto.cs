using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Api.Dtos;

public class MaterialWriteResultDto
{
    public int? Id { get; set; }
    public string? MaterialName { get; set; }
    public string? Unit { get; set; }
    public int? GoodsId { get; set; }
    public string? Name { get; set; }
    public string? GoodsName { get; set; }
    public int? CoId { get; set; }
    public string? UnitName { get; set; }
    public decimal? UnitRate { get; set; }

    public Material ToEntity()
    {
        var materialId = Id ?? GoodsId ?? 0;
        var materialName = MaterialName ?? Name ?? GoodsName ?? string.Empty;
        var material = materialId > 0
            ? new Material(materialId, materialName, CoId ?? 0)
            : new Material(materialName, CoId ?? 0);

        material.UnitName = Unit ?? UnitName;
        material.UnitRate = UnitRate ?? 1m;
        material.IsDeleted = false;
        material.AddDate = DateTime.Now;
        material.AddTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds();

        return material;
    }
}

public class ProviderWriteResultDto
{
    public int? Id { get; set; }
    public int? ProviderId { get; set; }
    public int? Version { get; set; }
    public int? ProviderType { get; set; }
    public string? ProviderName { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContectName { get; set; }
    public string? ContectPhone { get; set; }

    public Provider ToEntity()
    {
        var providerId = Id ?? ProviderId ?? 0;
        var provider = providerId > 0
            ? new Provider(providerId, ProviderType ?? 0, ProviderName ?? string.Empty)
            : new Provider(ProviderType ?? 0, ProviderName ?? string.Empty);

        provider.ContectName = ContactName ?? ContectName;
        provider.ContectPhone = ContactPhone ?? ContectPhone;
        provider.IsDeleted = false;
        provider.AddDate = DateTime.Now;
        provider.AddTime = (int)DateTimeOffset.Now.ToUnixTimeSeconds();

        return provider;
    }

    public ProviderDto ToProviderDto()
    {
        return new ProviderDto
        {
            Id = Id ?? ProviderId ?? 0,
            ProviderType = ProviderType ?? 0,
            ProviderName = ProviderName ?? string.Empty,
            ContactName = ContactName ?? ContectName,
            ContactPhone = ContactPhone ?? ContectPhone
        };
    }
}
