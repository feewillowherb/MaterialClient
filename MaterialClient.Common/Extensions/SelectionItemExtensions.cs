using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Extensions;

/// <summary>
/// SelectionItem 与 Provider / Material / 镇街（string）的互转扩展方法。
/// </summary>
public static class SelectionItemExtensions
{
    /// <summary>ProviderDto → SelectionItem</summary>
    public static SelectionItem ToSelectionItem(this ProviderDto dto) =>
        new() { Id = dto.Id, Name = dto.ProviderName ?? string.Empty };

    /// <summary>Provider 实体 → SelectionItem</summary>
    public static SelectionItem ToSelectionItem(this Provider entity) =>
        new() { Id = entity.Id, Name = entity.ProviderName ?? string.Empty };

    /// <summary>SelectionItem → 供应商 Id（用于查询等）</summary>
    public static int ToProviderId(this SelectionItem item) => item.Id;

    /// <summary>Material → SelectionItem</summary>
    public static SelectionItem ToSelectionItem(this Material entity) =>
        new() { Id = entity.Id, Name = entity.Name ?? string.Empty };

    /// <summary>SelectionItem → 材料 Id</summary>
    public static int ToMaterialId(this SelectionItem item) => item.Id;

    /// <summary>镇街名称 → SelectionItem（镇街无数字 Id，用 0）</summary>
    public static SelectionItem FromStreetName(string? streetName) =>
        new() { Id = 0, Name = streetName ?? string.Empty };

    /// <summary>SelectionItem → 镇街名称</summary>
    public static string ToStreetName(this SelectionItem item) => item.Name;
}
