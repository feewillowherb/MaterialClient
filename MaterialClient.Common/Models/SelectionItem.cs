using System;
using MaterialClient.Common.Api.Dtos;
using MaterialClient.Common.Entities;

namespace MaterialClient.Common.Models;

/// <summary>
/// 统一选择项 DTO，供 SearchableSelectionBox 使用。
/// </summary>
public sealed class SelectionItem : IEquatable<SelectionItem>
{
    public int Id { get; }
    public string Name { get; }

    private SelectionItem(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public static SelectionItem FromProvider(ProviderDto p) => new(p.Id, p.ProviderName);
    public static SelectionItem FromMaterial(Material m) => new(m.Id, m.Name ?? string.Empty);
    public static SelectionItem FromStreet(string name) => new(name.GetStableHashCode(), name);

    public bool Equals(SelectionItem? other) => other is not null && Id == other.Id;
    public override bool Equals(object? obj) => Equals(obj as SelectionItem);
    public override int GetHashCode() => Id;
}
