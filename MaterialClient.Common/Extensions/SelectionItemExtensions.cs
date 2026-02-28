using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Models;

namespace MaterialClient.Common.Extensions;

/// <summary>
/// 实体扩展方法，提供SelectionItem转换
/// </summary>
public static class SelectionItemExtensions
{
    /// <summary>
    /// Provider 转为 SelectionItem
    /// </summary>
    public static SelectionItem ToSelectionItem(this Provider provider)
    {
        return new SelectionItem(provider.Id, provider.ProviderName);
    }

    /// <summary>
    /// Material 转为 SelectionItem
    /// </summary>
    public static SelectionItem ToSelectionItem(this Material material)
    {
        return new SelectionItem(material.Id, material.Name);
    }

    /// <summary>
    /// ProviderDto 转为 SelectionItem
    /// </summary>
    public static SelectionItem ToSelectionItem(this ProviderDto dto)
    {
        return new SelectionItem(dto.Id, dto.ProviderName);
    }

    /// <summary>
    /// 镇街（string）转为 SelectionItem
    /// </summary>
    public static SelectionItem ToSelectionItem(this string street)
    {
        return new SelectionItem(street.GetHashCode(), street);
    }
}
